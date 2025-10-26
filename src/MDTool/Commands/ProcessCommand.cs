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

        // Conditional options
        var enableConditionsOption = new Option<bool>(
            name: "--enable-conditions",
            description: "Enable conditional evaluation ({{#if}}, {{else}}, {{/if}})");

        var strictConditionsOption = new Option<bool>(
            name: "--strict-conditions",
            description: "Unknown variables in conditionals cause errors; enables case-sensitive string comparison");

        var conditionsTraceOutOption = new Option<string?>(
            name: "--conditions-trace-out",
            description: "Write conditional evaluation trace to file (JSON format)");

        var conditionsTraceStderrOption = new Option<bool>(
            name: "--conditions-trace-stderr",
            description: "Write conditional evaluation trace to stderr");

        var conditionsMaxDepthOption = new Option<int>(
            name: "--conditions-max-depth",
            description: "Maximum nesting depth for conditional blocks",
            getDefaultValue: () => 5);

        var parseFencesOption = new Option<bool>(
            name: "--parse-fences",
            description: "Evaluate conditional tags inside code fences (default: false)");

        // Add to command
        AddArgument(fileArgument);
        AddOption(argsOption);
        AddOption(outputOption);
        AddOption(forceOption);
        AddOption(enableConditionsOption);
        AddOption(strictConditionsOption);
        AddOption(conditionsTraceOutOption);
        AddOption(conditionsTraceStderrOption);
        AddOption(conditionsMaxDepthOption);
        AddOption(parseFencesOption);

        // Set handler
        this.SetHandler(async (context) =>
        {
            var file = context.ParseResult.GetValueForArgument(fileArgument);
            var args = context.ParseResult.GetValueForOption(argsOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            var enableConditions = context.ParseResult.GetValueForOption(enableConditionsOption);
            var strictConditions = context.ParseResult.GetValueForOption(strictConditionsOption);
            var conditionsTraceOut = context.ParseResult.GetValueForOption(conditionsTraceOutOption);
            var conditionsTraceStderr = context.ParseResult.GetValueForOption(conditionsTraceStderrOption);
            var conditionsMaxDepth = context.ParseResult.GetValueForOption(conditionsMaxDepthOption);
            var parseFences = context.ParseResult.GetValueForOption(parseFencesOption);

            context.ExitCode = await ExecuteAsync(
                file,
                args!,
                output,
                force,
                enableConditions,
                strictConditions,
                conditionsTraceOut,
                conditionsTraceStderr,
                conditionsMaxDepth,
                parseFences);
        });
    }

    private static async Task<int> ExecuteAsync(
        string filePath,
        string argsPath,
        string? outputPath,
        bool forceOverwrite,
        bool enableConditions,
        bool strictConditions,
        string? conditionsTraceOut,
        bool conditionsTraceStderr,
        int conditionsMaxDepth,
        bool parseFences)
    {
        ConditionalTrace? trace = null;

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

            // Step 3: Merge args with YAML defaults
            var mergedArgs = new Dictionary<string, object>(args, StringComparer.OrdinalIgnoreCase);
            foreach (var varDef in document.Variables.Values)
            {
                if (!varDef.Required && varDef.DefaultValue != null)
                {
                    // Only add default if not already present
                    if (!mergedArgs.ContainsKey(varDef.Name))
                    {
                        mergedArgs[varDef.Name] = varDef.DefaultValue;
                    }
                }
            }

            // Step 4: Evaluate conditionals if enabled
            string effectiveContent = document.Content;

            if (enableConditions)
            {
                var accessor = new ArgsJsonAccessor(mergedArgs);
                var options = new ConditionalOptions(
                    Strict: strictConditions,
                    CaseSensitiveStrings: strictConditions,
                    MaxNesting: conditionsMaxDepth
                );

                var evalResult = ConditionalEvaluator.EvaluateDetailed(
                    document.Content,
                    accessor,
                    options);

                if (!evalResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(evalResult.Errors));
                    return 1;
                }

                effectiveContent = evalResult.Value.content;
                trace = evalResult.Value.trace;
            }

            // Step 5: Perform variable substitution on effective content
            var substitutionResult = VariableSubstitutor.Substitute(
                effectiveContent,
                document.Variables,
                mergedArgs);

            if (!substitutionResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(substitutionResult.Errors));
                return 1;
            }

            var processedContent = substitutionResult.Value!;

            // Step 6: Check file overwrite protection
            if (!string.IsNullOrEmpty(outputPath))
            {
                if (File.Exists(outputPath) && !forceOverwrite)
                {
                    var error = ValidationError.FileExists(outputPath);
                    Console.WriteLine(JsonOutput.Failure(new List<ValidationError> { error }));
                    return 1;
                }
            }

            // Step 7: Write trace output if requested
            if (trace != null)
            {
                var traceJson = SerializeTrace(trace);

                if (!string.IsNullOrEmpty(conditionsTraceOut))
                {
                    await File.WriteAllTextAsync(conditionsTraceOut, traceJson);
                }

                if (conditionsTraceStderr)
                {
                    await Console.Error.WriteLineAsync(traceJson);
                }
            }

            // Step 8: Output processed content
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

    private static string SerializeTrace(ConditionalTrace trace)
    {
        var traceObj = new
        {
            blocks = trace.Blocks.Select(b => new
            {
                startLine = b.StartLine,
                endLine = b.EndLine,
                branches = b.Branches.Select(br => new
                {
                    kind = br.Kind,
                    expr = br.Expr,
                    taken = br.Taken
                })
            })
        };

        return JsonSerializer.Serialize(traceObj, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static int HandleException(Exception ex)
    {
        var error = ValidationError.UnhandledException(ex.Message);
        Console.WriteLine(JsonOutput.Failure(new List<ValidationError> { error }));
        return 1;
    }
}
