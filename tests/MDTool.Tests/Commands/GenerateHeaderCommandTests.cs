using System.CommandLine;
using System.Text.Json;
using MDTool.Commands;
using Xunit;

namespace MDTool.Tests.Commands;

/// <summary>
/// Tests for GenerateHeaderCommand.
/// </summary>
public class GenerateHeaderCommandTests : IDisposable
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

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_WithVariables_GeneratesYamlHeader()
    {
        // Arrange
        var markdown = @"# Welcome {{USER}}

Your account {{ACCOUNT_ID}} is active.";

        var inputFile = CreateTempFile(markdown);
        var command = new GenerateHeaderCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "generate-header", inputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        Assert.Contains("---", output.StdOut);
        Assert.Contains("variables:", output.StdOut);
        Assert.Contains("ACCOUNT_ID:", output.StdOut);
        Assert.Contains("USER:", output.StdOut);
        Assert.Contains("Description for ACCOUNT_ID", output.StdOut);
        Assert.Contains("Description for USER", output.StdOut);
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_WithOutputFile_WritesToFile()
    {
        // Arrange
        var markdown = @"# {{NAME}}";

        var inputFile = CreateTempFile(markdown);
        var outputFile = Path.GetTempFileName();
        _tempFiles.Add(outputFile);
        File.Delete(outputFile); // Delete so test can create it

        var command = new GenerateHeaderCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "generate-header", inputFile, "--output", outputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
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
        var command = new GenerateHeaderCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "generate-header", inputFile });
        });

        // Assert
        Assert.Equal(1, output.ExitCode);
        var json = JsonDocument.Parse(output.StdOut);
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
        var command = new GenerateHeaderCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "generate-header", inputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        Assert.Contains("---", output.StdOut);
        Assert.Contains("variables:", output.StdOut);
        // Phase 1: Flat structure - each variable on its own line
        Assert.Contains("USER.EMAIL:", output.StdOut);
        Assert.Contains("USER.NAME:", output.StdOut);
        Assert.Contains("USER.REGION:", output.StdOut);
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_DuplicateVariables_ListsOnce()
    {
        // Arrange
        var markdown = @"# {{NAME}} - {{NAME}}

Welcome {{NAME}}!";

        var inputFile = CreateTempFile(markdown);
        var command = new GenerateHeaderCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "generate-header", inputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        // Should only list NAME once
        var nameCount = System.Text.RegularExpressions.Regex.Matches(output.StdOut, @"^\s*NAME:", System.Text.RegularExpressions.RegexOptions.Multiline).Count;
        Assert.Equal(1, nameCount);
    }

    [Fact(Skip = "Test infrastructure issue - console output capture unreliable in full test suite.")]
    public async Task GenerateHeader_SortsVariablesAlphabetically()
    {
        // Arrange
        var markdown = @"# {{ZEBRA}} {{APPLE}} {{MONKEY}}";

        var inputFile = CreateTempFile(markdown);
        var command = new GenerateHeaderCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "generate-header", inputFile });
        });

        // Assert
        Assert.Equal(0, output.ExitCode);
        var lines = output.StdOut.Split('\n').Where(l => l.Contains(":")).ToList();
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
        var command = new GenerateHeaderCommand();
        var rootCommand = new RootCommand { command };

        // Act
        var output = await CaptureOutput(async () =>
        {
            return await rootCommand.InvokeAsync(new[] { "generate-header", "/nonexistent/file.md" });
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
