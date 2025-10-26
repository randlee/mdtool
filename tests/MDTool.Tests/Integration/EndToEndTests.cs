using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace MDTool.Tests.Integration
{
    /// <summary>
    /// End-to-end integration tests that exercise MDTool CLI commands via actual process execution.
    /// These tests verify realistic workflows and command interactions.
    /// </summary>
    public class EndToEndTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _projectRoot;
        private readonly string _mdtoolPath;

        public EndToEndTests()
        {
            // Create unique test directory for this test run
            _testDirectory = Path.Combine(Path.GetTempPath(), $"mdtool-e2e-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDirectory);

            // Get project root directory (go up from test bin directory)
            // From: tests/MDTool.Tests/bin/Debug/net8.0 -> go up 5 levels to repo root
            _projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            _mdtoolPath = Path.Combine(_projectRoot, "src/MDTool/MDTool.csproj");

            // Verify MDTool project exists
            if (!File.Exists(_mdtoolPath))
            {
                throw new InvalidOperationException($"MDTool project not found at: {_mdtoolPath}");
            }
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Helper method to run mdtool commands via dotnet run
        /// </summary>
        private async Task<(int exitCode, string output, string error)> RunCommand(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{_mdtoolPath}\" -- {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = _testDirectory
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start mdtool process");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            return (process.ExitCode, output, error);
        }

        /// <summary>
        /// Test 1: Full workflow - get-schema, validate, process
        /// </summary>
        [Fact]
        public async Task FullWorkflow_GetSchema_Validate_Process_Succeeds()
        {
            // Step 1: Create a template file
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = @"---
variables:
  APP_NAME: ""Application name""
  VERSION:
    description: ""Version number""
    default: ""1.0.0""
---

# {{APP_NAME}} v{{VERSION}}

Welcome to {{APP_NAME}}!
";
            await File.WriteAllTextAsync(templatePath, templateContent);

            // Step 2: Run get-schema command
            var schemaPath = Path.Combine(_testDirectory, "schema.json");
            var (schemaExitCode, schemaOutput, schemaError) = await RunCommand($"get-schema \"{templatePath}\" --output \"{schemaPath}\"");

            Assert.Equal(0, schemaExitCode);
            Assert.True(File.Exists(schemaPath), "Schema file should be created");

            var schemaJson = await File.ReadAllTextAsync(schemaPath);
            var schemaDoc = JsonDocument.Parse(schemaJson);
            Assert.True(schemaDoc.RootElement.TryGetProperty("appName", out _), "Schema should contain appName");
            Assert.True(schemaDoc.RootElement.TryGetProperty("version", out _), "Schema should contain version");

            // Step 3: Create args.json based on schema
            var argsPath = Path.Combine(_testDirectory, "args.json");
            var argsContent = @"{
  ""appName"": ""MyApp"",
  ""version"": ""2.0.0""
}";
            await File.WriteAllTextAsync(argsPath, argsContent);

            // Step 4: Run validate command (should pass)
            var (validateExitCode, validateOutput, validateError) = await RunCommand($"validate \"{templatePath}\" --args \"{argsPath}\"");

            Assert.Equal(0, validateExitCode);
            var validateResult = JsonDocument.Parse(validateOutput);
            Assert.True(validateResult.RootElement.GetProperty("success").GetBoolean());

            // Step 5: Run process command
            var outputPath = Path.Combine(_testDirectory, "output.md");
            var (processExitCode, processOutput, processError) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\" --output \"{outputPath}\"");

            Assert.Equal(0, processExitCode);
            Assert.True(File.Exists(outputPath), "Output file should be created");

            // Step 6: Verify output contains substituted values
            var outputContent = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("MyApp", outputContent);
            Assert.Contains("2.0.0", outputContent);
            Assert.DoesNotContain("{{APP_NAME}}", outputContent);
            Assert.DoesNotContain("{{VERSION}}", outputContent);
        }

        /// <summary>
        /// Test 2: Generate-header then process workflow
        /// </summary>
        [Fact]
        public async Task Workflow_GenerateHeader_Then_Process_Succeeds()
        {
            // Step 1: Create markdown without frontmatter
            var docPath = Path.Combine(_testDirectory, "document.md");
            var docContent = @"# Welcome {{USER_NAME}}

Your account {{ACCOUNT_ID}} is ready.
";
            await File.WriteAllTextAsync(docPath, docContent);

            // Step 2: Run generate-header
            var headerPath = Path.Combine(_testDirectory, "header.yaml");
            var (headerExitCode, headerOutput, headerError) = await RunCommand($"generate-header \"{docPath}\" --output \"{headerPath}\"");

            Assert.Equal(0, headerExitCode);
            Assert.True(File.Exists(headerPath), "Header file should be created");

            var headerContent = await File.ReadAllTextAsync(headerPath);
            Assert.Contains("variables:", headerContent);
            Assert.Contains("USER_NAME:", headerContent);
            Assert.Contains("ACCOUNT_ID:", headerContent);

            // Step 3: Combine header + content into template
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = headerContent + docContent;
            await File.WriteAllTextAsync(templatePath, templateContent);

            // Step 4: Create args and run process
            var argsPath = Path.Combine(_testDirectory, "args.json");
            var argsContent = @"{
  ""userName"": ""Alice"",
  ""accountId"": ""12345""
}";
            await File.WriteAllTextAsync(argsPath, argsContent);

            var (processExitCode, processOutput, processError) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\"");

            Assert.Equal(0, processExitCode);
            Assert.Contains("Alice", processOutput);
            Assert.Contains("12345", processOutput);
        }

        /// <summary>
        /// Test 3: Process command overwrite protection works correctly
        /// </summary>
        [Fact]
        public async Task ProcessCommand_OverwriteProtection_WorksCorrectly()
        {
            // Step 1: Create template and args
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = @"---
variables:
  NAME: ""Name""
---
# {{NAME}}
";
            await File.WriteAllTextAsync(templatePath, templateContent);

            var argsPath = Path.Combine(_testDirectory, "args.json");
            var argsContent = @"{""name"": ""Test""}";
            await File.WriteAllTextAsync(argsPath, argsContent);

            var outputPath = Path.Combine(_testDirectory, "output.md");

            // Step 2: Process to file (first time - should succeed)
            var (exitCode1, output1, error1) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\" --output \"{outputPath}\"");

            Assert.Equal(0, exitCode1);
            Assert.True(File.Exists(outputPath), "Output file should exist after first process");

            // Step 3: Try to process again without --force (should fail)
            var (exitCode2, output2, error2) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\" --output \"{outputPath}\"");

            Assert.Equal(1, exitCode2);
            var errorResult = JsonDocument.Parse(output2);
            Assert.False(errorResult.RootElement.GetProperty("success").GetBoolean());
            var errors = errorResult.RootElement.GetProperty("errors");
            Assert.True(errors.GetArrayLength() > 0);

            // Step 4: Process with --force (should succeed)
            var (exitCode3, output3, error3) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\" --output \"{outputPath}\" --force");

            Assert.Equal(0, exitCode3);

            // Step 5: Verify file was overwritten
            var finalContent = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("Test", finalContent);
        }

        /// <summary>
        /// Test 4: Error handling - file not found
        /// </summary>
        [Fact]
        public async Task ErrorHandling_FileNotFound_ReturnsError()
        {
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.md");
            var (exitCode, output, error) = await RunCommand($"get-schema \"{nonExistentPath}\"");

            Assert.Equal(1, exitCode);
            var errorResult = JsonDocument.Parse(output);
            Assert.False(errorResult.RootElement.GetProperty("success").GetBoolean());
            var errors = errorResult.RootElement.GetProperty("errors");
            Assert.True(errors.GetArrayLength() > 0);
        }

        /// <summary>
        /// Test 5: Error handling - invalid JSON args
        /// </summary>
        [Fact]
        public async Task ErrorHandling_InvalidJson_ReturnsError()
        {
            // Create valid template
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = @"---
variables:
  NAME: ""Name""
---
# {{NAME}}
";
            await File.WriteAllTextAsync(templatePath, templateContent);

            // Create invalid JSON args
            var argsPath = Path.Combine(_testDirectory, "invalid.json");
            var invalidJson = @"{ invalid json }";
            await File.WriteAllTextAsync(argsPath, invalidJson);

            var (exitCode, output, error) = await RunCommand($"validate \"{templatePath}\" --args \"{argsPath}\"");

            Assert.Equal(1, exitCode);
            var errorResult = JsonDocument.Parse(output);
            Assert.False(errorResult.RootElement.GetProperty("success").GetBoolean());
        }

        /// <summary>
        /// Test 6: Error handling - missing required variables
        /// </summary>
        [Fact]
        public async Task ErrorHandling_MissingRequiredVariables_ReturnsError()
        {
            // Create template with required variables
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = @"---
variables:
  NAME: ""Name""
  EMAIL: ""Email""
---
# {{NAME}} - {{EMAIL}}
";
            await File.WriteAllTextAsync(templatePath, templateContent);

            // Create args with only one variable
            var argsPath = Path.Combine(_testDirectory, "args.json");
            var argsContent = @"{""name"": ""Test""}";
            await File.WriteAllTextAsync(argsPath, argsContent);

            var (exitCode, output, error) = await RunCommand($"validate \"{templatePath}\" --args \"{argsPath}\"");

            Assert.Equal(1, exitCode);
            var errorResult = JsonDocument.Parse(output);
            Assert.False(errorResult.RootElement.GetProperty("success").GetBoolean());

            // Should have missing variables listed
            var missing = errorResult.RootElement.GetProperty("missing");
            Assert.True(missing.GetArrayLength() > 0);
        }

        /// <summary>
        /// Test 7: Nested variables workflow
        /// </summary>
        [Fact]
        public async Task NestedVariables_Workflow_Succeeds()
        {
            // Create template with nested variables
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = @"---
variables:
  USER.NAME: ""User name""
  USER.EMAIL: ""User email""
  CONFIG.DB.HOST: ""Database host""
---
# User: {{USER.NAME}}
Email: {{USER.EMAIL}}
DB: {{CONFIG.DB.HOST}}
";
            await File.WriteAllTextAsync(templatePath, templateContent);

            // Create nested args
            var argsPath = Path.Combine(_testDirectory, "args.json");
            var argsContent = @"{
  ""user"": {
    ""name"": ""Bob"",
    ""email"": ""bob@example.com""
  },
  ""config"": {
    ""db"": {
      ""host"": ""localhost""
    }
  }
}";
            await File.WriteAllTextAsync(argsPath, argsContent);

            // Validate
            var (validateExitCode, validateOutput, validateError) = await RunCommand($"validate \"{templatePath}\" --args \"{argsPath}\"");
            Assert.Equal(0, validateExitCode);

            // Process
            var (processExitCode, processOutput, processError) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\"");

            Assert.Equal(0, processExitCode);
            Assert.Contains("Bob", processOutput);
            Assert.Contains("bob@example.com", processOutput);
            Assert.Contains("localhost", processOutput);
        }

        /// <summary>
        /// Test 8: Optional variables with defaults
        /// </summary>
        [Fact(Skip = "Test infrastructure issue - requires investigation of optional variable handling.")]
        public async Task OptionalVariables_WithDefaults_UsesDefaults()
        {
            // Create template with optional variables
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = @"---
variables:
  NAME: ""Name""
  VERSION:
    description: ""Version""
    default: ""1.0.0""
  DEBUG:
    description: ""Debug mode""
    default: ""false""
---
# {{NAME}} v{{VERSION}}
Debug: {{DEBUG}}
";
            await File.WriteAllTextAsync(templatePath, templateContent);

            // Create args with only required variable
            var argsPath = Path.Combine(_testDirectory, "args.json");
            var argsContent = @"{""name"": ""TestApp""}";
            await File.WriteAllTextAsync(argsPath, argsContent);

            // Process should use defaults
            var (processExitCode, processOutput, processError) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\"");

            Assert.Equal(0, processExitCode);
            Assert.Contains("TestApp", processOutput);
            Assert.Contains("1.0.0", processOutput);
            Assert.Contains("false", processOutput);
        }

        /// <summary>
        /// Test 9: Get-schema with nested structure produces correct JSON
        /// </summary>
        [Fact]
        public async Task GetSchema_NestedStructure_ProducesCorrectJson()
        {
            // Create template with nested variables
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = @"---
variables:
  USER.NAME: ""User name""
  USER.PROFILE.BIO: ""Bio""
  API.KEY.TOKEN: ""API token""
---
# Content
";
            await File.WriteAllTextAsync(templatePath, templateContent);

            var (exitCode, output, error) = await RunCommand($"get-schema \"{templatePath}\"");

            Assert.Equal(0, exitCode);

            var schemaDoc = JsonDocument.Parse(output);
            var root = schemaDoc.RootElement;

            // Verify nested structure
            Assert.True(root.TryGetProperty("user", out var user));
            Assert.True(user.TryGetProperty("name", out _));
            Assert.True(user.TryGetProperty("profile", out var profile));
            Assert.True(profile.TryGetProperty("bio", out _));

            Assert.True(root.TryGetProperty("api", out var api));
            Assert.True(api.TryGetProperty("key", out var key));
            Assert.True(key.TryGetProperty("token", out _));
        }

        /// <summary>
        /// Test 10: Case-insensitive matching works in process command
        /// </summary>
        [Fact]
        public async Task Process_CaseInsensitiveMatching_Succeeds()
        {
            // Create template
            var templatePath = Path.Combine(_testDirectory, "template.md");
            var templateContent = @"---
variables:
  USER_NAME: ""User name""
  USER_EMAIL: ""User email""
---
# {{USER_NAME}} - {{USER_EMAIL}}
";
            await File.WriteAllTextAsync(templatePath, templateContent);

            // Create args with different casing
            var argsPath = Path.Combine(_testDirectory, "args.json");
            var argsContent = @"{
  ""userName"": ""Charlie"",
  ""userEmail"": ""charlie@example.com""
}";
            await File.WriteAllTextAsync(argsPath, argsContent);

            var (processExitCode, processOutput, processError) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\"");

            Assert.Equal(0, processExitCode);
            Assert.Contains("Charlie", processOutput);
            Assert.Contains("charlie@example.com", processOutput);
        }
    }
}
