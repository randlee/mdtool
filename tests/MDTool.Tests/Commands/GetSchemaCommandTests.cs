using System.CommandLine;
using System.Text.Json;
using MDTool.Commands;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Tests for GetSchemaCommand.
/// </summary>
public class GetSchemaCommandTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

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
        var command = new GetSchemaCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "get-schema", inputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(output.StdOut), $"Output was empty. StdErr: {output.StdErr}");
        var json = JsonDocument.Parse(output.StdOut);
        Assert.True(json.RootElement.TryGetProperty("name", out var nameElement));
        Assert.Equal("Application name", nameElement.GetString());
        Assert.True(json.RootElement.TryGetProperty("port", out var portElement));
        Assert.Equal(8080, portElement.GetInt32());
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
        var outputFile = Path.GetTempFileName();
        _tempFiles.Add(outputFile);
        File.Delete(outputFile); // Delete so test can create it

        var command = new GetSchemaCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "get-schema", inputFile, "--output", outputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
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
        var command = new GetSchemaCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "get-schema", "/nonexistent/file.md" });
        });

        // Assert
        Assert.Equal(1, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new GetSchemaCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "get-schema", inputFile });
        });

        // Assert
        Assert.Equal(1, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new GetSchemaCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "get-schema", inputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
        Assert.True(json.RootElement.TryGetProperty("user", out var userElement));
        Assert.True(userElement.TryGetProperty("name", out var nameElement));
        Assert.Equal("User name", nameElement.GetString());
        Assert.True(userElement.TryGetProperty("email", out var emailElement));
        Assert.Equal("User email", emailElement.GetString());
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
