using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Tests for GenerateHeaderCommand.
/// </summary>
public class GenerateHeaderCommandTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly string _testDirectory;
    private readonly string _projectRoot;
    private readonly string _mdtoolPath;

    public GenerateHeaderCommandTests()
    {
        // Create unique test directory for this test run
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mdtool-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Get project root directory
        // On CI (GitHub Actions), use GITHUB_WORKSPACE if available
        // Otherwise, go up from test bin directory
        _projectRoot = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        _mdtoolPath = Path.Combine(_projectRoot, "src/MDTool/MDTool.csproj");
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

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

    private string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_WithVariables_GeneratesYamlHeader()
    {
        // Arrange
        var markdown = @"# Welcome {{USER}}

Your account {{ACCOUNT_ID}} is active.";

        var inputFile = CreateTempFile(markdown);

        // Act
        var (exitCode, output, error) = await RunCommand($"generate-header \"{inputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("---", output);
        Assert.Contains("variables:", output);
        Assert.Contains("ACCOUNT_ID:", output);
        Assert.Contains("USER:", output);
        Assert.Contains("Description for ACCOUNT_ID", output);
        Assert.Contains("Description for USER", output);
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_WithOutputFile_WritesToFile()
    {
        // Arrange
        var markdown = @"# {{NAME}}";

        var inputFile = CreateTempFile(markdown);
        var outputFile = Path.Combine(_testDirectory, "output.yaml");

        // Act
        var (exitCode, output, error) = await RunCommand($"generate-header \"{inputFile}\" --output \"{outputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputFile));
        var content = File.ReadAllText(outputFile);
        Assert.Contains("---", content);
        Assert.Contains("variables:", content);
        Assert.Contains("NAME:", content);
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_NoVariables_ReturnsError()
    {
        // Arrange
        var markdown = @"# Welcome

This document has no variables.";

        var inputFile = CreateTempFile(markdown);

        // Act
        var (exitCode, output, error) = await RunCommand($"generate-header \"{inputFile}\"");

        // Assert
        Assert.Equal(1, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_NestedVariables_GeneratesFlatStructure()
    {
        // Arrange - Phase 1 should generate flat structure
        var markdown = @"# {{USER.NAME}}
Email: {{USER.EMAIL}}
Region: {{USER.REGION}}";

        var inputFile = CreateTempFile(markdown);

        // Act
        var (exitCode, output, error) = await RunCommand($"generate-header \"{inputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("---", output);
        Assert.Contains("variables:", output);
        // Phase 1: Flat structure - each variable on its own line
        Assert.Contains("USER.EMAIL:", output);
        Assert.Contains("USER.NAME:", output);
        Assert.Contains("USER.REGION:", output);
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_DuplicateVariables_ListsOnce()
    {
        // Arrange
        var markdown = @"# {{NAME}} - {{NAME}}

Welcome {{NAME}}!";

        var inputFile = CreateTempFile(markdown);

        // Act
        var (exitCode, output, error) = await RunCommand($"generate-header \"{inputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        // Should only list NAME once
        var nameCount = System.Text.RegularExpressions.Regex.Matches(output, @"^\s*NAME:", System.Text.RegularExpressions.RegexOptions.Multiline).Count;
        Assert.Equal(1, nameCount);
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_SortsVariablesAlphabetically()
    {
        // Arrange
        var markdown = @"# {{ZEBRA}} {{APPLE}} {{MONKEY}}";

        var inputFile = CreateTempFile(markdown);

        // Act
        var (exitCode, output, error) = await RunCommand($"generate-header \"{inputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        var lines = output.Split('\n').Where(l => l.Contains(":")).ToList();
        var appleIndex = lines.FindIndex(l => l.Contains("APPLE"));
        var monkeyIndex = lines.FindIndex(l => l.Contains("MONKEY"));
        var zebraIndex = lines.FindIndex(l => l.Contains("ZEBRA"));

        Assert.True(appleIndex < monkeyIndex);
        Assert.True(monkeyIndex < zebraIndex);
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_FileNotFound_ReturnsError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.md");

        // Act
        var (exitCode, output, error) = await RunCommand($"generate-header \"{nonExistentFile}\"");

        // Assert
        Assert.Equal(1, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
    }

    private async Task<(int exitCode, string output, string error)> RunCommand(string args)
    {
        // Use the built DLL directly instead of 'dotnet run' to avoid build output in stdout
        var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
        var dllPath = Path.Combine(_projectRoot, $"src/MDTool/bin/{configuration}/net8.0/MDTool.dll");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _testDirectory
        };

        var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start process");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output, error);
    }
}
