using System.CommandLine;
using System.Text.Json;
using MDTool.Core;
using MDTool.Models;
using MDTool.Utilities;

namespace MDTool.Commands;

/// <summary>
/// Command to process markdown with variable substitution.
/// Usage: mdtool process <file> --args <path> [--output <path>] [--force]
/// </summary>
public class ProcessCommand : Command
{
    public ProcessCommand()
        : base("process", "Process markdown with variable substitution")
    {
        // Define arguments
        var fileArgument = new Argument<string>(
            name: "file",
            description: "Path to markdown template file");

        // Define options
        var argsOption = new Option<string>(
            name: "--args",
            description: "Path to JSON arguments file")
        {
            IsRequired = true
        };
        argsOption.AddAlias("-a");

        var outputOption = new Option<string?>(
            name: "--output",
            description: "Output file path (default: stdout)");
        outputOption.AddAlias("-o");

        var forceOption = new Option<bool>(
            name: "--force",
            description: "Overwrite existing output file");
        forceOption.AddAlias("-f");

        // Add to command
        AddArgument(fileArgument);
        AddOption(argsOption);
        AddOption(outputOption);
        AddOption(forceOption);

        // Set handler
        this.SetHandler(async (context) =>
        {
            var file = context.ParseResult.GetValueForArgument(fileArgument);
            var args = context.ParseResult.GetValueForOption(argsOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            context.ExitCode = await ExecuteAsync(file, args!, output, force);
        });
    }

    private static async Task<int> ExecuteAsync(
        string filePath,
        string argsPath,
        string? outputPath,
        bool forceOverwrite)
    {
        try
        {
            // Step 1: Read and parse markdown file
            var readResult = await FileHelper.ReadFileAsync(filePath);
            if (!readResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(readResult.Errors));
                return 1;
            }

            var parser = new MarkdownParser();
            var parseResult = parser.ParseContent(readResult.Value!);
            if (!parseResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(parseResult.Errors));
                return 1;
            }

            var document = parseResult.Value!;

            // Step 2: Load JSON arguments
            var argsResult = await LoadJsonArgsAsync(argsPath);
            if (!argsResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(argsResult.Errors));
                return 1;
            }

            var args = argsResult.Value!;

            // Step 3: Perform variable substitution
            var substitutionResult = VariableSubstitutor.Substitute(
                document.Content,
                document.Variables,
                args);

            if (!substitutionResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(substitutionResult.Errors));
                return 1;
            }

            var processedContent = substitutionResult.Value!;

            // Step 4: Check file overwrite protection
            if (!string.IsNullOrEmpty(outputPath))
            {
                if (File.Exists(outputPath) && !forceOverwrite)
                {
                    var error = ValidationError.FileExists(outputPath);
                    Console.WriteLine(JsonOutput.Failure(new List<ValidationError> { error }));
                    return 1;
                }
            }

            // Step 5: Output processed content
            if (string.IsNullOrEmpty(outputPath))
            {
                // Output to stdout (raw content, not JSON wrapped)
                Console.WriteLine(processedContent);
            }
            else
            {
                // Write to file
                var writeResult = await FileHelper.WriteFileAsync(outputPath, processedContent, forceOverwrite);

                if (!writeResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(writeResult.Errors));
                    return 1;
                }

                // Success message
                Console.WriteLine(JsonOutput.Success(new
                {
                    message = $"Successfully processed to: {outputPath}",
                    file = outputPath
                }));
            }

            return 0;
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private static async Task<ProcessingResult<Dictionary<string, object>>> LoadJsonArgsAsync(string argsPath)
    {
        var readResult = await FileHelper.ReadFileAsync(argsPath);
        if (!readResult.Success)
        {
            return ProcessingResult<Dictionary<string, object>>.Fail(readResult.Errors);
        }

        try
        {
            var json = readResult.Value!;
            var jsonDoc = JsonDocument.Parse(json);
            var dict = JsonElementToDictionary(jsonDoc.RootElement);
            return ProcessingResult<Dictionary<string, object>>.Ok(dict);
        }
        catch (JsonException ex)
        {
            return ProcessingResult<Dictionary<string, object>>.Fail(
                ValidationError.InvalidJson($"Failed to parse JSON: {ex.Message}")
            );
        }
    }

    private static Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = ConvertJsonElement(property.Value);
        }

        return dict;
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Object => element,
            JsonValueKind.Array => element,
            _ => element
        };
    }

    private static int HandleException(Exception ex)
    {
        var error = ValidationError.UnhandledException(ex.Message);
        Console.WriteLine(JsonOutput.Failure(new List<ValidationError> { error }));
        return 1;
    }
}
