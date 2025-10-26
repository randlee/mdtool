using System.CommandLine;
using System.Text.Json;
using MDTool.Commands;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Tests for ProcessCommand.
/// </summary>
public class ProcessCommandTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
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
        var command = new ProcessCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "process", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        Assert.Contains("MyApp", output.StdOut);
        Assert.Contains("8080", output.StdOut);
        Assert.Contains("# MyApp running on port 8080", output.StdOut);
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
        var outputFile = Path.GetTempFileName();
        _tempFiles.Add(outputFile);
        File.Delete(outputFile); // Delete so test can create it

        var command = new ProcessCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "process", markdownFile, "--args", argsFile, "--output", outputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
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

        var command = new ProcessCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "process", markdownFile, "--args", argsFile, "--output", outputFile });
        });

        // Assert
        Assert.Equal(1, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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

        var command = new ProcessCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "process", markdownFile, "--args", argsFile, "--output", outputFile, "--force" });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
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
        var command = new ProcessCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "process", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(1, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new ProcessCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "process", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        Assert.Contains("John Doe", output.StdOut);
        Assert.Contains("john@example.com", output.StdOut);
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
        var command = new ProcessCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "process", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        Assert.Contains("MyApp", output.StdOut);
        Assert.Contains("8080", output.StdOut);
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> CaptureOutput(Func<Task<int>> action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        Console.SetOut(outWriter);
        Console.SetError(errWriter);

        try
        {
            var exitCode = await action();
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
