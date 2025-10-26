using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Tests for GetSchemaCommand.
/// </summary>
public class GetSchemaCommandTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly string _testDirectory;
    private readonly string _projectRoot;
    private readonly string _mdtoolPath;

    public GetSchemaCommandTests()
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
        // Clean up temp files
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

    [Fact]
    public async Task GetSchema_ValidMarkdown_ReturnsSchema()
    {
        // Arrange
        var markdown = @"---
variables:
  NAME: ""Application name""
  PORT:
    description: ""Server port""
    required: false
    default: 8080
---

# {{NAME}} running on port {{PORT}}";

        var inputFile = CreateTempFile(markdown);

        // Act
        var (exitCode, output, error) = await RunCommand($"get-schema \"{inputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(output), $"Output was empty. StdErr: {error}");
        var json = JsonDocument.Parse(output);
        Assert.True(json.RootElement.TryGetProperty("name", out var nameElement));
        Assert.Equal("Application name", nameElement.GetString());
        Assert.True(json.RootElement.TryGetProperty("port", out var portElement));
        // Port is returned as string from default value
        Assert.Equal("8080", portElement.GetString());
    }

    [Fact]
    public async Task GetSchema_WithOutputFile_WritesToFile()
    {
        // Arrange
        var markdown = @"---
variables:
  TEST: ""Test variable""
---

# {{TEST}}";

        var inputFile = CreateTempFile(markdown);
        var outputFile = Path.Combine(_testDirectory, "output.json");

        // Act
        var (exitCode, output, error) = await RunCommand($"get-schema \"{inputFile}\" --output \"{outputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputFile));
        var content = File.ReadAllText(outputFile);
        var json = JsonDocument.Parse(content);
        Assert.True(json.RootElement.TryGetProperty("test", out var testElement));
        Assert.Equal("Test variable", testElement.GetString());
    }

    [Fact]
    public async Task GetSchema_FileNotFound_ReturnsError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.md");

        // Act
        var (exitCode, output, error) = await RunCommand($"get-schema \"{nonExistentFile}\"");

        // Assert
        Assert.Equal(1, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.True(json.RootElement.GetProperty("errors").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetSchema_InvalidYaml_ReturnsError()
    {
        // Arrange
        var markdown = @"---
variables:
  NAME: [invalid yaml structure
---

# Test";

        var inputFile = CreateTempFile(markdown);

        // Act
        var (exitCode, output, error) = await RunCommand($"get-schema \"{inputFile}\"");

        // Assert
        Assert.Equal(1, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task GetSchema_NestedVariables_GeneratesNestedSchema()
    {
        // Arrange
        var markdown = @"---
variables:
  USER.NAME: ""User name""
  USER.EMAIL: ""User email""
---

# {{USER.NAME}} - {{USER.EMAIL}}";

        var inputFile = CreateTempFile(markdown);

        // Act
        var (exitCode, output, error) = await RunCommand($"get-schema \"{inputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.True(json.RootElement.TryGetProperty("user", out var userElement));
        Assert.True(userElement.TryGetProperty("name", out var nameElement));
        Assert.Equal("User name", nameElement.GetString());
        Assert.True(userElement.TryGetProperty("email", out var emailElement));
        Assert.Equal("User email", emailElement.GetString());
    }

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
