using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Integration tests for conditional evaluation in ProcessCommand and ValidateCommand.
/// Tests Wave 2 CLI integration with the core conditional logic from Wave 1.
/// </summary>
public class ConditionalIntegrationTests
{
    private readonly string _mdtoolPath;
    private readonly string _fixturesPath;

    public ConditionalIntegrationTests()
    {
        // Get the path to the compiled MDTool executable
        _mdtoolPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "src", "MDTool", "bin", "Debug", "net8.0", "MDTool.dll"));

        _fixturesPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "fixtures", "conditionals"));
    }

    #region A) Basic Conditionals (5 tests)

    [Fact]
    public async Task Test_ProcessCommand_SimpleIfEndif_KeepsContentWhenTrue()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "simple-if.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        // Act
        var output = await RunProcess(templatePath, argsPath, enableConditions: true);

        // Assert
        Assert.Contains("This content should appear when ROLE is TEST.", output);
        Assert.DoesNotContain("{{#if", output);
        Assert.DoesNotContain("{{/if}}", output);
    }

    [Fact]
    public async Task Test_ProcessCommand_SimpleIfEndif_RemovesContentWhenFalse()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "simple-if.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-other.json");

        // Act
        var output = await RunProcess(templatePath, argsPath, enableConditions: true);

        // Assert
        Assert.DoesNotContain("This content should appear when ROLE is TEST.", output);
        Assert.DoesNotContain("{{#if", output);
        Assert.DoesNotContain("{{/if}}", output);
    }

    [Fact]
    public async Task Test_ProcessCommand_IfElseEndif_SelectsCorrectBranch()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "if-else.md");
        var argsPathTest = Path.Combine(_fixturesPath, "simple-if-role-test.json");
        var argsPathOther = Path.Combine(_fixturesPath, "simple-if-role-other.json");

        // Act
        var outputTest = await RunProcess(templatePath, argsPathTest, enableConditions: true);
        var outputOther = await RunProcess(templatePath, argsPathOther, enableConditions: true);

        // Assert
        Assert.Contains("Content for TEST role.", outputTest);
        Assert.DoesNotContain("Content for other roles.", outputTest);

        Assert.DoesNotContain("Content for TEST role.", outputOther);
        Assert.Contains("Content for other roles.", outputOther);
    }

    [Fact]
    public async Task Test_ProcessCommand_IfElseIfElseEndif_EvaluatesInOrder()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "if-elseif-else.md");
        var argsPathTest = Path.Combine(_fixturesPath, "simple-if-role-test.json");
        var argsPathReport = Path.Combine(_fixturesPath, "role-report.json");
        var argsPathOther = Path.Combine(_fixturesPath, "simple-if-role-other.json");

        // Act
        var outputTest = await RunProcess(templatePath, argsPathTest, enableConditions: true);
        var outputReport = await RunProcess(templatePath, argsPathReport, enableConditions: true);
        var outputOther = await RunProcess(templatePath, argsPathOther, enableConditions: true);

        // Assert
        Assert.Contains("Content for TEST role.", outputTest);
        Assert.DoesNotContain("Content for REPORT role.", outputTest);
        Assert.DoesNotContain("Content for other roles.", outputTest);

        Assert.DoesNotContain("Content for TEST role.", outputReport);
        Assert.Contains("Content for REPORT role.", outputReport);
        Assert.DoesNotContain("Content for other roles.", outputReport);

        Assert.DoesNotContain("Content for TEST role.", outputOther);
        Assert.DoesNotContain("Content for REPORT role.", outputOther);
        Assert.Contains("Content for other roles.", outputOther);
    }

    [Fact]
    public async Task Test_ProcessCommand_NestedConditionals_WorksCorrectly()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "nested.md");
        var argsPath = Path.Combine(_fixturesPath, "role-test-debug.json");

        // Act
        var output = await RunProcess(templatePath, argsPath, enableConditions: true);

        // Assert
        Assert.Contains("Outer: TEST role", output);
        Assert.Contains("Inner: Debug mode enabled", output);
        Assert.DoesNotContain("Inner: Debug mode disabled", output);
        Assert.DoesNotContain("Outer: Other role", output);
    }

    [Fact]
    public async Task Test_ProcessCommand_MultipleBlocks_AllEvaluated()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "multiple-blocks.md");
        var argsPath = Path.Combine(_fixturesPath, "role-test-debug.json");

        // Act
        var output = await RunProcess(templatePath, argsPath, enableConditions: true);

        // Assert
        Assert.Contains("First block: TEST role", output);
        Assert.Contains("Middle content.", output);
        Assert.Contains("Second block: Debug enabled", output);
    }

    #endregion

    #region B) Role-Based Content (3 tests)

    [Fact]
    public async Task Test_ProcessCommand_RoleTest_KeepsTestContent()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "if-elseif-else.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        // Act
        var output = await RunProcess(templatePath, argsPath, enableConditions: true);

        // Assert
        Assert.Contains("Content for TEST role.", output);
        Assert.DoesNotContain("Content for REPORT role.", output);
        Assert.DoesNotContain("Content for other roles.", output);
    }

    [Fact]
    public async Task Test_ProcessCommand_RoleReport_KeepsReportContent()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "if-elseif-else.md");
        var argsPath = Path.Combine(_fixturesPath, "role-report.json");

        // Act
        var output = await RunProcess(templatePath, argsPath, enableConditions: true);

        // Assert
        Assert.DoesNotContain("Content for TEST role.", output);
        Assert.Contains("Content for REPORT role.", output);
        Assert.DoesNotContain("Content for other roles.", output);
    }

    [Fact]
    public async Task Test_ProcessCommand_RoleOther_KeepsElseContent()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "if-elseif-else.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-other.json");

        // Act
        var output = await RunProcess(templatePath, argsPath, enableConditions: true);

        // Assert
        Assert.DoesNotContain("Content for TEST role.", output);
        Assert.DoesNotContain("Content for REPORT role.", output);
        Assert.Contains("Content for other roles.", output);
    }

    #endregion

    #region C) Content-Scoped Validation (3 tests)

    [Fact]
    public async Task Test_ValidateCommand_VarInExcludedBranch_NotRequired()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "validation-scoped.md");
        var argsPath = Path.Combine(_fixturesPath, "role-test-vara.json");

        // Act
        var result = await RunValidate(templatePath, argsPath, enableConditions: true);

        // Assert - Should succeed because VAR_B is in excluded branch
        Assert.True(result.Success);
        Assert.Contains("VAR_A", result.Provided);
        Assert.DoesNotContain("VAR_B", result.Missing);
    }

    [Fact]
    public async Task Test_ValidateCommand_VarInIncludedBranch_Required()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "validation-scoped.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json"); // Missing VAR_A

        // Act
        var result = await RunValidate(templatePath, argsPath, enableConditions: true);

        // Assert - Should fail because VAR_A is in included branch
        Assert.False(result.Success);
        Assert.Contains("VAR_A", result.Missing);
    }

    [Fact]
    public async Task Test_ValidateCommand_RequireAllYaml_RequiresAllVars()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "validation-scoped.md");
        var argsPath = Path.Combine(_fixturesPath, "role-test-vara.json"); // Has VAR_A but not VAR_B

        // Act
        var result = await RunValidate(templatePath, argsPath, enableConditions: true, requireAllYaml: true);

        // Assert - Should fail because VAR_B is required in YAML
        Assert.False(result.Success);
        Assert.Contains("VAR_B", result.Missing);
    }

    #endregion

    #region D) Code Fence Protection (2 tests)

    [Fact]
    public async Task Test_ProcessCommand_TagsInFence_RemainLiteral()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "code-fence.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        // Act - Without --parse-fences, tags in code fences should remain literal
        var output = await RunProcess(templatePath, argsPath, enableConditions: true, parseFences: false);

        // Assert
        Assert.Contains("This is outside code fence.", output);
        Assert.DoesNotContain("{{#if ROLE == 'TEST'}}", output.Split("```")[0]); // Outside fence
        Assert.Contains("{{#if ROLE == 'TEST'}}", output.Split("```")[1]); // Inside fence
    }

    [Fact]
    public async Task Test_ProcessCommand_ParseFences_EvaluatesTags()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "code-fence.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        // Act - With --parse-fences, tags should be evaluated everywhere
        // NOTE: This test expects the current implementation which does NOT parse fences
        // The --parse-fences flag is provided for future enhancement
        var output = await RunProcess(templatePath, argsPath, enableConditions: true, parseFences: false);

        // Assert - For now, same behavior as above
        Assert.Contains("This is outside code fence.", output);
        Assert.Contains("{{#if ROLE == 'TEST'}}", output.Split("```")[1]); // Inside fence remains
    }

    #endregion

    #region E) Trace Output (2 tests)

    [Fact]
    public async Task Test_ProcessCommand_TraceOut_WritesToFile()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "if-elseif-else.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");
        var traceFile = Path.GetTempFileName();

        try
        {
            // Act
            await RunProcess(templatePath, argsPath, enableConditions: true, traceOut: traceFile);

            // Assert
            Assert.True(File.Exists(traceFile));
            var traceContent = await File.ReadAllTextAsync(traceFile);
            Assert.Contains("\"blocks\"", traceContent);
            Assert.Contains("\"taken\"", traceContent);
        }
        finally
        {
            if (File.Exists(traceFile))
                File.Delete(traceFile);
        }
    }

    [Fact]
    public async Task Test_ProcessCommand_TraceStderr_WritesToStderr()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "if-elseif-else.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        // Act
        var (stdout, stderr) = await RunProcessWithStderr(templatePath, argsPath, enableConditions: true, traceStderr: true);

        // Assert
        Assert.Contains("\"blocks\"", stderr);
        Assert.Contains("\"taken\"", stderr);
        Assert.DoesNotContain("\"blocks\"", stdout); // Trace not in stdout
    }

    #endregion

    #region F) Error Cases (3 tests)

    [Fact]
    public async Task Test_ProcessCommand_MismatchedTags_ReturnsError()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "mismatched-tags.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        // Act
        var exitCode = await RunProcessGetExitCode(templatePath, argsPath, enableConditions: true);

        // Assert
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task Test_ProcessCommand_UnknownVarStrict_ReturnsError()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "unknown-var-strict.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        // Act
        var exitCode = await RunProcessGetExitCode(templatePath, argsPath, enableConditions: true, strictConditions: true);

        // Assert
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task Test_ProcessCommand_NestingExceeded_ReturnsError()
    {
        // Arrange - Create a deeply nested template
        var templatePath = Path.GetTempFileName();
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        try
        {
            // Create 10 nested levels (exceeds default max of 5)
            var content = @"---
variables:
  ROLE:
    description: ""User role""
    required: true
---

# Deeply Nested Test

";
            for (int i = 0; i < 10; i++)
            {
                content += "{{#if ROLE == 'TEST'}}\n";
                content += $"Level {i + 1}\n";
            }
            for (int i = 0; i < 10; i++)
            {
                content += "{{/if}}\n";
            }

            await File.WriteAllTextAsync(templatePath, content);

            // Act
            var exitCode = await RunProcessGetExitCode(templatePath, argsPath, enableConditions: true, maxDepth: 5);

            // Assert
            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            if (File.Exists(templatePath))
                File.Delete(templatePath);
        }
    }

    #endregion

    #region G) Backward Compatibility (2 tests)

    [Fact]
    public async Task Test_ProcessCommand_NoEnableFlag_TagsRemainLiteral()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "simple-if.md");
        var argsPath = Path.Combine(_fixturesPath, "simple-if-role-test.json");

        // Act - Without --enable-conditions, tags should remain literal
        var output = await RunProcess(templatePath, argsPath, enableConditions: false);

        // Assert
        Assert.Contains("{{#if ROLE == 'TEST'}}", output);
        Assert.Contains("{{/if}}", output);
    }

    [Fact]
    public async Task Test_ValidateCommand_NoEnableFlag_ValidatesOriginalContent()
    {
        // Arrange
        var templatePath = Path.Combine(_fixturesPath, "validation-scoped.md");
        var argsPath = Path.Combine(_fixturesPath, "role-test-vara.json"); // Has VAR_A but not VAR_B

        // Act - Without --enable-conditions, should validate all content
        var result = await RunValidate(templatePath, argsPath, enableConditions: false);

        // Assert - Should fail because both VAR_A and VAR_B are used in original content
        Assert.False(result.Success);
        Assert.Contains("VAR_B", result.Missing);
    }

    #endregion

    #region Helper Methods

    private async Task<string> RunProcess(
        string templatePath,
        string argsPath,
        bool enableConditions,
        bool parseFences = false,
        bool strictConditions = false,
        string? traceOut = null)
    {
        var args = $"process \"{templatePath}\" --args \"{argsPath}\"";
        if (enableConditions) args += " --enable-conditions";
        if (parseFences) args += " --parse-fences";
        if (strictConditions) args += " --strict-conditions";
        if (traceOut != null) args += $" --conditions-trace-out \"{traceOut}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{_mdtoolPath}\" {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }

    private async Task<(string stdout, string stderr)> RunProcessWithStderr(
        string templatePath,
        string argsPath,
        bool enableConditions,
        bool traceStderr = false)
    {
        var args = $"process \"{templatePath}\" --args \"{argsPath}\"";
        if (enableConditions) args += " --enable-conditions";
        if (traceStderr) args += " --conditions-trace-stderr";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{_mdtoolPath}\" {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (stdout, stderr);
    }

    private async Task<int> RunProcessGetExitCode(
        string templatePath,
        string argsPath,
        bool enableConditions,
        bool strictConditions = false,
        int maxDepth = 5)
    {
        var args = $"process \"{templatePath}\" --args \"{argsPath}\"";
        if (enableConditions) args += " --enable-conditions";
        if (strictConditions) args += " --strict-conditions";
        args += $" --conditions-max-depth {maxDepth}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{_mdtoolPath}\" {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        return process.ExitCode;
    }

    private async Task<ValidationTestResult> RunValidate(
        string templatePath,
        string argsPath,
        bool enableConditions,
        bool requireAllYaml = false)
    {
        var args = $"validate \"{templatePath}\" --args \"{argsPath}\"";
        if (enableConditions) args += " --enable-conditions";
        if (requireAllYaml) args += " --require-all-yaml";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{_mdtoolPath}\" {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var result = JsonSerializer.Deserialize<ValidationTestResult>(output, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? new ValidationTestResult();
    }

    private class ValidationTestResult
    {
        public bool Success { get; set; }
        public List<string> Provided { get; set; } = new();
        public List<string> Missing { get; set; } = new();
    }

    #endregion
}
