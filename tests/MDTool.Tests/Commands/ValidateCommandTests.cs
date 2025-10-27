using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Tests for ValidateCommand.
/// </summary>
public class ValidateCommandTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly string _testDirectory;
    private readonly string _projectRoot;
    private readonly string _mdtoolPath;

    public ValidateCommandTests()
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
        // Small delay to ensure all processes have fully released file handles
        Thread.Sleep(100);

        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        // Clean up test directory with retry logic
        if (Directory.Exists(_testDirectory))
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                    break;
                }
                catch
                {
                    if (i < 2) Thread.Sleep(100);
                }
            }
        }
    }

    private string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        // Small delay to ensure file system has flushed the write
        Thread.Sleep(50);
        return path;
    }

    [Fact]
    public async Task Validate_AllVariablesProvided_Succeeds()
    {
        // Arrange
        var markdown = @"---
variables:
  NAME: ""Application name""
  PORT: ""Server port""
---

# {{NAME}} on port {{PORT}}";

        var args = @"{
  ""name"": ""MyApp"",
  ""port"": 8080
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"validate \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.True(json.RootElement.TryGetProperty("provided", out var provided));
        Assert.True(provided.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Validate_MissingRequiredVariable_Fails()
    {
        // Arrange
        var markdown = @"---
variables:
  NAME: ""Application name""
  PORT: ""Server port""
---

# {{NAME}} on port {{PORT}}";

        var args = @"{
  ""name"": ""MyApp""
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"validate \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(1, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.True(json.RootElement.TryGetProperty("missing", out var missing));
        Assert.Contains("PORT", missing.EnumerateArray().Select(e => e.GetString()).ToList());
    }

    [Fact]
    public async Task Validate_OptionalVariableWithDefault_Succeeds()
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

# {{NAME}} on port {{PORT}}";

        var args = @"{
  ""name"": ""MyApp""
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"validate \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Validate_CaseInsensitiveMatching_Succeeds()
    {
        // Arrange
        var markdown = @"---
variables:
  USER_NAME: ""User name""
---

# {{USER_NAME}}";

        var args = @"{
  ""userName"": ""John""
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"validate \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Validate_NestedVariables_Succeeds()
    {
        // Arrange
        var markdown = @"---
variables:
  USER.NAME: ""User name""
  USER.EMAIL: ""User email""
---

# {{USER.NAME}} - {{USER.EMAIL}}";

        var args = @"{
  ""user"": {
    ""name"": ""John"",
    ""email"": ""john@example.com""
  }
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"validate \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Validate_InvalidJsonArgs_ReturnsError()
    {
        // Arrange
        var markdown = @"---
variables:
  NAME: ""Application name""
---

# {{NAME}}";

        var args = @"{ invalid json }";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"validate \"{markdownFile}\" --args \"{argsFile}\"");

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

        // Small delay to ensure process is fully initialized
        await Task.Delay(50);

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        // Small delay to ensure process has fully terminated
        await Task.Delay(100);

        return (process.ExitCode, output, error);
    }
}
