using System.Text.Json;
using MDTool.Models;

namespace MDTool.Core;

/// <summary>
/// Substitutes variable placeholders in markdown content with actual values.
/// Supports case-insensitive JSON matching and nested object navigation via dot notation.
/// </summary>
public class VariableSubstitutor
{
    /// <summary>
    /// Substitutes all variable placeholders in content with values from args.
    /// Performs case-insensitive matching for JSON keys and supports nested object navigation.
    /// </summary>
    /// <param name="content">The markdown content with variable placeholders</param>
    /// <param name="variables">Variable definitions from YAML frontmatter</param>
    /// <param name="args">JSON arguments providing variable values</param>
    /// <returns>ProcessingResult containing substituted content or errors</returns>
    public static ProcessingResult<string> Substitute(
        string content,
        Dictionary<string, VariableDefinition> variables,
        Dictionary<string, object> args)
    {
        if (content == null)
        {
            return ProcessingResult<string>.Fail(
                ValidationError.ProcessingError("Content cannot be null")
            );
        }

        if (variables == null)
        {
            return ProcessingResult<string>.Fail(
                ValidationError.ProcessingError("Variables dictionary cannot be null")
            );
        }

        if (args == null)
        {
            args = new Dictionary<string, object>();
        }

        var errors = new List<ValidationError>();

        // Step 1: Extract all variables from content
        var extractedVars = VariableExtractor.ExtractVariables(content);

        // Step 2: Build complete args with defaults for optional variables
        var completeArgs = BuildCompleteArgs(variables, args);

        // Step 3: Validate all required variables are present
        var missingVars = ValidateRequiredVariables(variables, completeArgs, extractedVars);
        if (missingVars.Any())
        {
            foreach (var varName in missingVars)
            {
                if (variables.TryGetValue(varName, out var varDef))
                {
                    errors.Add(ValidationError.MissingVariable(varName, varDef.Description));
                }
                else
                {
                    errors.Add(ValidationError.MissingVariable(varName, $"Variable '{varName}' is not defined"));
                }
            }

            return ProcessingResult<string>.Fail(errors);
        }

        // Step 4: Perform substitution
        var result = content;

        // Sort by position descending to preserve string positions during replacement
        var sortedVars = extractedVars.OrderByDescending(v => v).ToList();

        foreach (var variableName in sortedVars)
        {
            var value = ResolveVariable(variableName, completeArgs);

            if (value == null)
            {
                // This shouldn't happen after validation, but be safe
                errors.Add(ValidationError.MissingVariable(
                    variableName,
                    $"Could not resolve variable: {variableName}"
                ));
                continue;
            }

            // Replace all occurrences of this variable
            var placeholder = $"{{{{{variableName}}}}}";
            var valueString = ConvertValueToString(value);
            result = result.Replace(placeholder, valueString);
        }

        if (errors.Any())
        {
            return ProcessingResult<string>.Fail(errors);
        }

        return ProcessingResult<string>.Ok(result);
    }

    /// <summary>
    /// Builds a complete args dictionary including defaults for optional variables.
    /// Uses case-insensitive comparison for JSON keys.
    /// </summary>
    private static Dictionary<string, object> BuildCompleteArgs(
        Dictionary<string, VariableDefinition> variables,
        Dictionary<string, object> providedArgs)
    {
        var complete = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Add provided args (case-insensitive)
        foreach (var kvp in providedArgs)
        {
            complete[kvp.Key] = kvp.Value;
        }

        // Add defaults for optional variables not provided
        foreach (var varDef in variables.Values)
        {
            if (!varDef.Required && varDef.DefaultValue != null)
            {
                var varName = varDef.Name;

                // Check if this variable (or its path) is already provided
                if (!HasValue(varName, complete))
                {
                    complete[varName] = varDef.DefaultValue;
                }
            }
        }

        return complete;
    }

    /// <summary>
    /// Validates that all required variables are present in args.
    /// Returns a list of missing required variable names.
    /// </summary>
    private static List<string> ValidateRequiredVariables(
        Dictionary<string, VariableDefinition> variables,
        Dictionary<string, object> args,
        List<string> extractedVars)
    {
        var missing = new List<string>();

        foreach (var varName in extractedVars)
        {
            // Check if variable is defined
            if (!variables.TryGetValue(varName, out var varDef))
            {
                // Variable used but not defined in YAML - this is an error
                missing.Add(varName);
                continue;
            }

            // If required, check if value is provided
            if (varDef.Required)
            {
                var resolved = ResolveVariable(varName, args);
                if (resolved == null)
                {
                    missing.Add(varName);
                }
            }
        }

        return missing;
    }

    /// <summary>
    /// Resolves a variable value from the args dictionary.
    /// Supports both simple variables (NAME) and nested dot notation (USER.NAME).
    /// Uses case-insensitive matching for JSON keys.
    /// </summary>
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

            // Try case-insensitive lookup
            foreach (var key in args.Keys)
            {
                if (string.Equals(key, variableName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[key];
                }
            }

            // Try normalized comparison (remove underscores and compare)
            // This handles USER_NAME matching userName, user_name, etc.
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

            // Handle Dictionary<string, object>
            if (current is Dictionary<string, object> dict)
            {
                current = GetValueCaseInsensitive(dict, segment);
            }
            // Handle JsonElement (from System.Text.Json deserialization)
            else if (current is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    current = GetPropertyCaseInsensitive(element, segment);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                // Can't navigate further
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Gets a value from a dictionary using case-insensitive key matching.
    /// Also handles format variations like USER_NAME matching userName or user_name.
    /// </summary>
    private static object? GetValueCaseInsensitive(Dictionary<string, object> dict, string key)
    {
        // Try direct lookup first (fast path)
        if (dict.TryGetValue(key, out var value))
        {
            return value;
        }

        // Try case-insensitive lookup
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // Try normalized comparison (remove underscores and compare)
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

    /// <summary>
    /// Gets a property from a JsonElement using case-insensitive matching.
    /// Also handles format variations like USER_NAME matching userName or user_name.
    /// </summary>
    private static object? GetPropertyCaseInsensitive(JsonElement element, string propertyName)
    {
        // Try exact match first
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return ConvertJsonElement(prop);
        }

        // Try case-insensitive match
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return ConvertJsonElement(property.Value);
            }
        }

        // Try normalized comparison (remove underscores and compare)
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

    /// <summary>
    /// Converts a JsonElement to a native .NET type.
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => element,
            JsonValueKind.Array => element,
            _ => element
        };
    }

    /// <summary>
    /// Checks if a variable path has a value in the args dictionary.
    /// Handles both simple names and dot-notation paths.
    /// </summary>
    private static bool HasValue(string variableName, Dictionary<string, object> args)
    {
        return ResolveVariable(variableName, args) != null;
    }

    /// <summary>
    /// Converts a value to its string representation for substitution.
    /// </summary>
    private static string ConvertValueToString(object value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => element.ToString()
            };
        }

        return value.ToString() ?? string.Empty;
    }
}
