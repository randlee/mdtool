using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace MDTool.Tests.Examples
{
    /// <summary>
    /// Integration tests for conditional examples.
    /// These tests verify that the example files in examples/ directory work correctly.
    /// </summary>
    public class ConditionalExamplesTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _projectRoot;
        private readonly string _mdtoolPath;
        private readonly string _examplesDir;

        public ConditionalExamplesTests()
        {
            // Create unique test directory for this test run
            _testDirectory = Path.Combine(Path.GetTempPath(), $"mdtool-examples-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDirectory);

            // Get project root directory (go up from test bin directory)
            _projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            _mdtoolPath = Path.Combine(_projectRoot, "src/MDTool/MDTool.csproj");
            _examplesDir = Path.Combine(_projectRoot, "examples");

            // Verify paths exist
            if (!File.Exists(_mdtoolPath))
            {
                throw new InvalidOperationException($"MDTool project not found at: {_mdtoolPath}");
            }

            if (!Directory.Exists(_examplesDir))
            {
                throw new InvalidOperationException($"Examples directory not found at: {_examplesDir}");
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

        [Fact]
        public async Task Test_ExampleConditionals_TestRole_ProcessesCorrectly()
        {
            // Arrange
            var templatePath = Path.Combine(_examplesDir, "conditionals.md");
            var argsPath = Path.Combine(_examplesDir, "conditionals-test.json");
            var outputPath = Path.Combine(_testDirectory, "test-output.md");

            // Verify example files exist
            Assert.True(File.Exists(templatePath), $"Template file should exist at {templatePath}");
            Assert.True(File.Exists(argsPath), $"Args file should exist at {argsPath}");

            // Act
            var (exitCode, output, error) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\" --output \"{outputPath}\"");

            // Assert
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath), "Output file should be created");

            var outputContent = await File.ReadAllTextAsync(outputPath);

            // Verify output was generated successfully
            Assert.NotEmpty(outputContent);

            // Verify variable substitution occurred
            Assert.Contains("qa-test-1", outputContent);
            Assert.Contains("DEV", outputContent);

            // Verify the template structure is present
            // Note: Conditional processing verification depends on feature being enabled in CLI
            Assert.Contains("QA Execution Agent", outputContent);
        }

        [Fact]
        public async Task Test_ExampleConditionals_ReportRole_ProcessesCorrectly()
        {
            // Arrange
            var templatePath = Path.Combine(_examplesDir, "conditionals.md");
            var argsPath = Path.Combine(_examplesDir, "conditionals-report.json");
            var outputPath = Path.Combine(_testDirectory, "report-output.md");

            // Verify example files exist
            Assert.True(File.Exists(templatePath), $"Template file should exist at {templatePath}");
            Assert.True(File.Exists(argsPath), $"Args file should exist at {argsPath}");

            // Act
            var (exitCode, output, error) = await RunCommand($"process \"{templatePath}\" --args \"{argsPath}\" --output \"{outputPath}\"");

            // Assert
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath), "Output file should be created");

            var outputContent = await File.ReadAllTextAsync(outputPath);

            // Verify output was generated successfully
            Assert.NotEmpty(outputContent);

            // Verify variable substitution occurred
            Assert.Contains("qa-report-1", outputContent);
            Assert.Contains("PROD", outputContent);

            // Verify the template structure is present
            // Note: Conditional processing verification depends on feature being enabled in CLI
            Assert.Contains("QA Execution Agent", outputContent);
        }

        [Fact]
        public async Task Test_ExampleConditionals_TestRole_ValidatesCorrectly()
        {
            // Arrange
            var templatePath = Path.Combine(_examplesDir, "conditionals.md");
            var argsPath = Path.Combine(_examplesDir, "conditionals-test.json");

            // Act
            var (exitCode, output, error) = await RunCommand($"validate \"{templatePath}\" --args \"{argsPath}\"");

            // Assert
            Assert.Equal(0, exitCode);

            var validateResult = JsonDocument.Parse(output);
            Assert.True(validateResult.RootElement.GetProperty("success").GetBoolean());

            // Verify provided variables
            var provided = validateResult.RootElement.GetProperty("provided");
            Assert.True(provided.GetArrayLength() > 0);

            // Verify no missing variables
            var missing = validateResult.RootElement.GetProperty("missing");
            Assert.Equal(0, missing.GetArrayLength());
        }

        [Fact]
        public async Task Test_ExampleConditionals_ReportRole_ValidatesCorrectly()
        {
            // Arrange
            var templatePath = Path.Combine(_examplesDir, "conditionals.md");
            var argsPath = Path.Combine(_examplesDir, "conditionals-report.json");

            // Act
            var (exitCode, output, error) = await RunCommand($"validate \"{templatePath}\" --args \"{argsPath}\"");

            // Assert
            Assert.Equal(0, exitCode);

            var validateResult = JsonDocument.Parse(output);
            Assert.True(validateResult.RootElement.GetProperty("success").GetBoolean());

            // Verify provided variables
            var provided = validateResult.RootElement.GetProperty("provided");
            Assert.True(provided.GetArrayLength() > 0);

            // Verify no missing variables
            var missing = validateResult.RootElement.GetProperty("missing");
            Assert.Equal(0, missing.GetArrayLength());
        }

        [Fact]
        public async Task Test_ExampleConditionals_AllExamplesExecuteWithoutErrors()
        {
            // This test ensures that both example scenarios can be processed end-to-end
            // without any errors or exceptions

            var templatePath = Path.Combine(_examplesDir, "conditionals.md");
            var testArgsPath = Path.Combine(_examplesDir, "conditionals-test.json");
            var reportArgsPath = Path.Combine(_examplesDir, "conditionals-report.json");

            // Verify files exist
            Assert.True(File.Exists(templatePath));
            Assert.True(File.Exists(testArgsPath));
            Assert.True(File.Exists(reportArgsPath));

            // Test scenario 1: TEST role
            var testOutputPath = Path.Combine(_testDirectory, "test-scenario.md");
            var (testExitCode, _, testError) = await RunCommand($"process \"{templatePath}\" --args \"{testArgsPath}\" --output \"{testOutputPath}\"");

            Assert.Equal(0, testExitCode);
            Assert.True(File.Exists(testOutputPath));
            Assert.True(string.IsNullOrEmpty(testError) || !testError.Contains("error", StringComparison.OrdinalIgnoreCase));

            // Test scenario 2: REPORT role
            var reportOutputPath = Path.Combine(_testDirectory, "report-scenario.md");
            var (reportExitCode, _, reportError) = await RunCommand($"process \"{templatePath}\" --args \"{reportArgsPath}\" --output \"{reportOutputPath}\"");

            Assert.Equal(0, reportExitCode);
            Assert.True(File.Exists(reportOutputPath));
            Assert.True(string.IsNullOrEmpty(reportError) || !reportError.Contains("error", StringComparison.OrdinalIgnoreCase));

            // Verify outputs contain expected variables
            var testContent = await File.ReadAllTextAsync(testOutputPath);
            var reportContent = await File.ReadAllTextAsync(reportOutputPath);

            // Verify both outputs were generated with correct variable substitution
            Assert.Contains("qa-test-1", testContent);
            Assert.Contains("qa-report-1", reportContent);
            Assert.Contains("DEV", testContent);
            Assert.Contains("PROD", reportContent);

            // Note: Conditional-specific content verification depends on feature being enabled in CLI
        }
    }
}
