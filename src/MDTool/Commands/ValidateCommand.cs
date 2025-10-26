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

        // Add to command
        AddArgument(fileArgument);
        AddOption(argsOption);

        // Set handler
        this.SetHandler(async (context) =>
        {
            var file = context.ParseResult.GetValueForArgument(fileArgument);
            var args = context.ParseResult.GetValueForOption(argsOption);
            context.ExitCode = await ExecuteAsync(file, args!);
        });
    }

    private static async Task<int> ExecuteAsync(string filePath, string argsPath)
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

            // Step 3: Validate arguments
            var validationResult = ValidateArgs(document.Variables, document.Content, args);

            // Step 4: Output validation result
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
        Dictionary<string, object> args)
    {
        // Extract variables used in content
        var usedVars = VariableExtractor.ExtractVariables(content);

        var provided = new List<string>();
        var missing = new List<string>();
        var errors = new List<ValidationError>();

        // Build complete args with defaults
        var completeArgs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Add provided args
        foreach (var kvp in args)
        {
            completeArgs[kvp.Key] = kvp.Value;
        }

        // Add defaults for optional variables
        foreach (var varDef in variables.Values)
        {
            if (!varDef.Required && varDef.DefaultValue != null)
            {
                if (!HasValue(varDef.Name, completeArgs))
                {
                    completeArgs[varDef.Name] = varDef.DefaultValue;
                }
            }
        }

        // Validate each used variable
        foreach (var varName in usedVars)
        {
            if (!variables.TryGetValue(varName, out var varDef))
            {
                // Variable used but not defined
                errors.Add(ValidationError.MissingVariable(varName, $"Variable '{varName}' is used but not defined in YAML frontmatter"));
                missing.Add(varName);
                continue;
            }

            var value = ResolveVariable(varName, completeArgs);

            if (value != null)
            {
                provided.Add(varName);
            }
            else if (varDef.Required)
            {
                errors.Add(ValidationError.MissingVariable(varName, varDef.Description));
                missing.Add(varName);
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

    private static int HandleException(Exception ex)
    {
        var error = ValidationError.UnhandledException(ex.Message);
        Console.WriteLine(JsonOutput.Failure(new List<ValidationError> { error }));
        return 1;
    }
}
