using System.CommandLine;
using System.Text.Json;
using MDTool.Commands;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Tests for ValidateCommand.
/// </summary>
public class ValidateCommandTests : IDisposable
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
        var command = new ValidateCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "validate", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new ValidateCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "validate", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(1, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new ValidateCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "validate", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new ValidateCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "validate", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new ValidateCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "validate", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new ValidateCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "validate", markdownFile, "--args", argsFile });
        });

        // Assert
        Assert.Equal(1, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
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
