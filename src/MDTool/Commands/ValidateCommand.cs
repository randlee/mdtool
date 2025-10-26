using System.CommandLine;
using System.Text.Json;
using MDTool.Core;
using MDTool.Models;
using MDTool.Utilities;

namespace MDTool.Commands;

/// <summary>
/// Command to validate JSON arguments against markdown schema.
/// Usage: mdtool validate <file> --args <path>
/// </summary>
public class ValidateCommand : Command
{
    public ValidateCommand()
        : base("validate", "Validate JSON arguments against markdown schema")
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

        var requireAllYamlOption = new Option<bool>(
            name: "--require-all-yaml",
            description: "Require all YAML-declared required variables regardless of content usage");

        // Add to command
        AddArgument(fileArgument);
        AddOption(argsOption);
        AddOption(enableConditionsOption);
        AddOption(strictConditionsOption);
        AddOption(conditionsTraceOutOption);
        AddOption(conditionsTraceStderrOption);
        AddOption(conditionsMaxDepthOption);
        AddOption(parseFencesOption);
        AddOption(requireAllYamlOption);

        // Set handler
        this.SetHandler(async (context) =>
        {
            var file = context.ParseResult.GetValueForArgument(fileArgument);
            var args = context.ParseResult.GetValueForOption(argsOption);
            var enableConditions = context.ParseResult.GetValueForOption(enableConditionsOption);
            var strictConditions = context.ParseResult.GetValueForOption(strictConditionsOption);
            var conditionsTraceOut = context.ParseResult.GetValueForOption(conditionsTraceOutOption);
            var conditionsTraceStderr = context.ParseResult.GetValueForOption(conditionsTraceStderrOption);
            var conditionsMaxDepth = context.ParseResult.GetValueForOption(conditionsMaxDepthOption);
            var parseFences = context.ParseResult.GetValueForOption(parseFencesOption);
            var requireAllYaml = context.ParseResult.GetValueForOption(requireAllYamlOption);

            context.ExitCode = await ExecuteAsync(
                file,
                args!,
                enableConditions,
                strictConditions,
                conditionsTraceOut,
                conditionsTraceStderr,
                conditionsMaxDepth,
                parseFences,
                requireAllYaml);
        });
    }

    private static async Task<int> ExecuteAsync(
        string filePath,
        string argsPath,
        bool enableConditions,
        bool strictConditions,
        string? conditionsTraceOut,
        bool conditionsTraceStderr,
        int conditionsMaxDepth,
        bool parseFences,
        bool requireAllYaml)
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

            // Step 5: Validate arguments
            var validationResult = ValidateArgs(
                document.Variables,
                effectiveContent,
                mergedArgs,
                requireAllYaml);

            // Step 6: Write trace output if requested
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

            // Step 7: Output validation result
            var output = new
            {
                success = validationResult.Success,
                provided = validationResult.ProvidedVariables,
                missing = validationResult.MissingVariables,
                errors = validationResult.Success ? null : validationResult.Errors.Select(e => new
                {
                    type = e.Type.ToString(),
                    variable = e.Variable,
                    description = e.Description,
                    line = e.Line
                })
            };

            Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));

            return validationResult.Success ? 0 : 1;
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

    private static ValidationResult ValidateArgs(
        Dictionary<string, VariableDefinition> variables,
        string content,
        Dictionary<string, object> args,
        bool requireAllYaml)
    {
        // Extract variables used in content
        var usedVars = VariableExtractor.ExtractVariables(content);

        var provided = new List<string>();
        var missing = new List<string>();
        var errors = new List<ValidationError>();

        // Determine which variables to require
        HashSet<string> requiredVars;

        if (requireAllYaml)
        {
            // Require all YAML-declared required variables
            requiredVars = new HashSet<string>(
                variables.Values.Where(v => v.Required).Select(v => v.Name),
                StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            // Content-scoped: only require variables used in effective content
            requiredVars = new HashSet<string>(
                usedVars.Where(v =>
                    variables.TryGetValue(v, out var varDef) && varDef.Required),
                StringComparer.OrdinalIgnoreCase);
        }

        // Validate each required variable
        foreach (var varName in requiredVars)
        {
            if (!variables.TryGetValue(varName, out var varDef))
            {
                // Variable required but not defined (shouldn't happen with requireAllYaml)
                errors.Add(ValidationError.MissingVariable(varName, $"Variable '{varName}' is required but not defined in YAML frontmatter"));
                missing.Add(varName);
                continue;
            }

            var value = ResolveVariable(varName, args);

            if (value != null)
            {
                provided.Add(varName);
            }
            else
            {
                errors.Add(ValidationError.MissingVariable(varName, varDef.Description));
                missing.Add(varName);
            }
        }

        // Also check for variables used in content that are not defined
        foreach (var varName in usedVars)
        {
            if (!variables.ContainsKey(varName))
            {
                errors.Add(ValidationError.MissingVariable(varName, $"Variable '{varName}' is used but not defined in YAML frontmatter"));
                if (!missing.Contains(varName))
                {
                    missing.Add(varName);
                }
            }
        }

        if (errors.Any())
        {
            return ValidationResult.Fail(errors, provided, missing);
        }

        return ValidationResult.Ok(provided);
    }

    private static bool HasValue(string variableName, Dictionary<string, object> args)
    {
        return ResolveVariable(variableName, args) != null;
    }

    private static object? ResolveVariable(string variableName, Dictionary<string, object> args)
    {
        if (string.IsNullOrEmpty(variableName))
        {
            return null;
        }

        // Handle simple variables (no dots)
        if (!variableName.Contains('.'))
        {
            // Try direct lookup (case-insensitive)
            if (args.TryGetValue(variableName, out var value))
            {
                return value;
            }

            // Try normalized comparison (remove underscores and compare)
            var normalizedVarName = variableName.Replace("_", "").ToLowerInvariant();
            foreach (var key in args.Keys)
            {
                var normalizedKey = key.Replace("_", "").ToLowerInvariant();
                if (normalizedVarName == normalizedKey)
                {
                    return args[key];
                }
            }

            return null;
        }

        // Handle nested variables (dot notation)
        var segments = variableName.Split('.');
        object? current = args;

        foreach (var segment in segments)
        {
            if (current == null)
            {
                return null;
            }

            if (current is Dictionary<string, object> dict)
            {
                current = GetValueCaseInsensitive(dict, segment);
            }
            else if (current is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                current = GetPropertyCaseInsensitive(element, segment);
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static object? GetValueCaseInsensitive(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value;
        }

        var normalizedKey = key.Replace("_", "").ToLowerInvariant();
        foreach (var kvp in dict)
        {
            var normalizedDictKey = kvp.Key.Replace("_", "").ToLowerInvariant();
            if (normalizedKey == normalizedDictKey)
            {
                return kvp.Value;
            }
        }

        return null;
    }

    private static object? GetPropertyCaseInsensitive(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return ConvertJsonElement(prop);
        }

        var normalizedName = propertyName.Replace("_", "").ToLowerInvariant();
        foreach (var property in element.EnumerateObject())
        {
            var normalizedPropName = property.Name.Replace("_", "").ToLowerInvariant();
            if (normalizedName == normalizedPropName)
            {
                return ConvertJsonElement(property.Value);
            }
        }

        return null;
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
