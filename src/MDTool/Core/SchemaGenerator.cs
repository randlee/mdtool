using System.Text.Json;
using MDTool.Models;

namespace MDTool.Core;

/// <summary>
/// Generates JSON schemas from variable definitions with lowerCamelCase keys.
/// Converts dot-notation variables into nested object structures.
/// </summary>
public class SchemaGenerator
{
    /// <summary>
    /// Generates a JSON schema from variable definitions.
    /// Variable names are converted to lowerCamelCase, and dot-notation creates nested objects.
    /// </summary>
    /// <param name="variables">Dictionary of variable definitions</param>
    /// <returns>Pretty-printed JSON schema string</returns>
    public static string GenerateSchema(Dictionary<string, VariableDefinition> variables)
    {
        if (variables == null || variables.Count == 0)
        {
            return "{}";
        }

        var schema = new Dictionary<string, object>(StringComparer.Ordinal);

        // Process variables in sorted order for deterministic output
        foreach (var variable in variables.Values.OrderBy(v => v.Name))
        {
            var path = variable.Name.Split('.');
            var current = schema;

            // Navigate/create nested structure
            for (int i = 0; i < path.Length - 1; i++)
            {
                var segmentKey = ToLowerCamelCase(path[i]);

                if (!current.TryGetValue(segmentKey, out var next) || next is not Dictionary<string, object> dict)
                {
                    dict = new Dictionary<string, object>(StringComparer.Ordinal);
                    current[segmentKey] = dict;
                }

                current = dict;
            }

            // Set final value
            var finalKey = ToLowerCamelCase(path[^1]);
            var value = GetSchemaValue(variable);
            current[finalKey] = value;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(schema, options);
    }

    /// <summary>
    /// Converts UPPER_SNAKE_CASE to lowerCamelCase.
    /// Examples:
    /// - USER_NAME → userName
    /// - API_KEY → apiKey
    /// - EMAIL → email
    /// </summary>
    private static string ToLowerCamelCase(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return segment;
        }

        // Split by underscores
        var parts = segment.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        // First part is lowercase
        var result = parts[0].ToLowerInvariant();

        // Remaining parts: capitalize first letter, lowercase rest
        for (int i = 1; i < parts.Length; i++)
        {
            var part = parts[i].ToLowerInvariant();
            if (part.Length > 0)
            {
                result += char.ToUpperInvariant(part[0]) + part.Substring(1);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the schema value for a variable definition.
    /// - Required variables: use description as placeholder
    /// - Optional variables: use default value
    /// </summary>
    private static object GetSchemaValue(VariableDefinition variable)
    {
        if (!variable.Required && variable.DefaultValue != null)
        {
            return variable.DefaultValue;
        }

        return variable.Description;
    }
}
