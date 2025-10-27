using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Tests for ProcessCommand.
/// </summary>
public class ProcessCommandTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly string _testDirectory;
    private readonly string _projectRoot;
    private readonly string _mdtoolPath;

    public ProcessCommandTests()
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
    public async Task Process_ValidMarkdownAndArgs_ReturnsProcessedContent()
    {
        // Arrange
        var markdown = @"---
variables:
  NAME: ""Application name""
  PORT: ""Server port""
---

# {{NAME}} running on port {{PORT}}";

        var args = @"{
  ""name"": ""MyApp"",
  ""port"": 8080
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"process \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("MyApp", output);
        Assert.Contains("8080", output);
        Assert.Contains("# MyApp running on port 8080", output);
    }

    [Fact]
    public async Task Process_WithOutputFile_WritesToFile()
    {
        // Arrange
        var markdown = @"---
variables:
  TEST: ""Test variable""
---

# {{TEST}}";

        var args = @"{
  ""test"": ""Hello World""
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);
        var outputFile = Path.Combine(_testDirectory, "output.md");

        // Act
        var (exitCode, output, error) = await RunCommand($"process \"{markdownFile}\" --args \"{argsFile}\" --output \"{outputFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputFile));
        var content = File.ReadAllText(outputFile);
        Assert.Contains("# Hello World", content);
    }

    [Fact]
    public async Task Process_FileExistsWithoutForce_ReturnsError()
    {
        // Arrange
        var markdown = @"---
variables:
  TEST: ""Test variable""
---

# {{TEST}}";

        var args = @"{
  ""test"": ""Hello""
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);
        var outputFile = CreateTempFile("existing content");

        // Act
        var (exitCode, output, error) = await RunCommand($"process \"{markdownFile}\" --args \"{argsFile}\" --output \"{outputFile}\"");

        // Assert
        Assert.Equal(1, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Process_FileExistsWithForce_Overwrites()
    {
        // Arrange
        var markdown = @"---
variables:
  TEST: ""Test variable""
---

# {{TEST}}";

        var args = @"{
  ""test"": ""Overwritten""
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);
        var outputFile = CreateTempFile("existing content");

        // Act
        var (exitCode, output, error) = await RunCommand($"process \"{markdownFile}\" --args \"{argsFile}\" --output \"{outputFile}\" --force");

        // Assert
        Assert.Equal(0, exitCode);
        var content = File.ReadAllText(outputFile);
        Assert.Contains("# Overwritten", content);
    }

    [Fact]
    public async Task Process_MissingRequiredVariable_ReturnsError()
    {
        // Arrange
        var markdown = @"---
variables:
  NAME: ""Application name""
  PORT: ""Server port""
---

# {{NAME}} on {{PORT}}";

        var args = @"{
  ""name"": ""MyApp""
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"process \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(1, exitCode);
        var json = JsonDocument.Parse(output);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Process_NestedVariables_SubstitutesCorrectly()
    {
        // Arrange
        var markdown = @"---
variables:
  USER.NAME: ""User name""
  USER.EMAIL: ""User email""
---

# User: {{USER.NAME}}
Email: {{USER.EMAIL}}";

        var args = @"{
  ""user"": {
    ""name"": ""John Doe"",
    ""email"": ""john@example.com""
  }
}";

        var markdownFile = CreateTempFile(markdown);
        var argsFile = CreateTempFile(args);

        // Act
        var (exitCode, output, error) = await RunCommand($"process \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("John Doe", output);
        Assert.Contains("john@example.com", output);
    }

    [Fact]
    public async Task Process_OptionalVariableWithDefault_UsesDefault()
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
        var (exitCode, output, error) = await RunCommand($"process \"{markdownFile}\" --args \"{argsFile}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("MyApp", output);
        Assert.Contains("8080", output);
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
