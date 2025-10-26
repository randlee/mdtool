using System.Text.Json;

namespace MDTool.Core;

/// <summary>
/// JSON-backed implementation of IArgsAccessor with case-insensitive, dot-path navigation.
/// </summary>
public class ArgsJsonAccessor : IArgsAccessor
{
    private readonly Dictionary<string, object> _args;

    /// <summary>
    /// Creates an accessor from a JsonDocument.
    /// </summary>
    public ArgsJsonAccessor(JsonDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        _args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Convert JsonDocument to dictionary for easier access
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                _args[property.Name] = ConvertJsonElement(property.Value);
            }
        }
    }

    /// <summary>
    /// Creates an accessor from a dictionary.
    /// </summary>
    public ArgsJsonAccessor(Dictionary<string, object> args)
    {
        _args = new Dictionary<string, object>(args, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to retrieve a value by path with case-insensitive matching.
    /// Supports dot notation for nested objects (e.g., "USER.NAME").
    /// </summary>
    public bool TryGet(string path, out object? value)
    {
        if (string.IsNullOrEmpty(path))
        {
            value = null;
            return false;
        }

        // Handle simple path (no dots)
        if (!path.Contains('.'))
        {
            return _args.TryGetValue(path, out value);
        }

        // Handle nested path (dot notation)
        var segments = path.Split('.');
        object? current = _args;

        foreach (var segment in segments)
        {
            if (current == null)
            {
                value = null;
                return false;
            }

            if (current is Dictionary<string, object> dict)
            {
                if (!GetValueCaseInsensitive(dict, segment, out current))
                {
                    value = null;
                    return false;
                }
            }
            else if (current is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (!GetPropertyCaseInsensitive(element, segment, out current))
                    {
                        value = null;
                        return false;
                    }
                }
                else
                {
                    value = null;
                    return false;
                }
            }
            else
            {
                // Can't navigate further
                value = null;
                return false;
            }
        }

        value = current;
        return true;
    }

    /// <summary>
    /// Gets a value from a dictionary using case-insensitive key matching.
    /// </summary>
    private static bool GetValueCaseInsensitive(Dictionary<string, object> dict, string key, out object? value)
    {
        // Try direct lookup first (fast path)
        if (dict.TryGetValue(key, out value))
        {
            return true;
        }

        // Try case-insensitive lookup
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Gets a property from a JsonElement using case-insensitive matching.
    /// </summary>
    private static bool GetPropertyCaseInsensitive(JsonElement element, string propertyName, out object? value)
    {
        // Try exact match first
        if (element.TryGetProperty(propertyName, out var prop))
        {
            value = ConvertJsonElement(prop);
            return true;
        }

        // Try case-insensitive match
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = ConvertJsonElement(property.Value);
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Converts a JsonElement to a native .NET type.
    /// </summary>
    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Object => element,
            JsonValueKind.Array => element,
            _ => element
        };
    }
}
