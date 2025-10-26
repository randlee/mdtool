using System.Text.RegularExpressions;
using MDTool.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Core;

namespace MDTool.Core;

/// <summary>
/// Parses markdown content to extract YAML frontmatter and content,
/// converting variable definitions into structured VariableDefinition objects.
/// </summary>
public class MarkdownParser
{
    private readonly IDeserializer _yamlDeserializer;
    private static readonly Regex VarNameRegex = new(
        @"^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Initializes a new instance of MarkdownParser with YAML deserialization support.
    /// </summary>
    public MarkdownParser()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Parses markdown content (not a file path) to extract YAML frontmatter and content.
    /// Supports both simple string format and object format for variable definitions.
    /// </summary>
    /// <param name="content">The markdown content string to parse</param>
    /// <returns>ProcessingResult containing MarkdownDocument or errors</returns>
    public ProcessingResult<MarkdownDocument> ParseContent(string content)
    {
        if (content == null)
        {
            return ProcessingResult<MarkdownDocument>.Fail(
                ValidationError.ProcessingError("Content cannot be null")
            );
        }

        var errors = new List<ValidationError>();

        // Step 1: Extract frontmatter
        var (hasYaml, yamlContent, markdownContent) = SplitFrontmatter(content);

        // Step 2: Handle missing frontmatter
        if (!hasYaml)
        {
            return ProcessingResult<MarkdownDocument>.Ok(
                new MarkdownDocument(
                    new Dictionary<string, VariableDefinition>(),
                    markdownContent,
                    null
                )
            );
        }

        // Step 3: Parse YAML
        Dictionary<string, object>? yamlData;
        try
        {
            var yamlObject = _yamlDeserializer.Deserialize<Dictionary<object, object>?>(yamlContent);

            if (yamlObject == null || yamlObject.Count == 0)
            {
                // Empty YAML is okay, return empty variables
                return ProcessingResult<MarkdownDocument>.Ok(
                    new MarkdownDocument(
                        new Dictionary<string, VariableDefinition>(),
                        markdownContent,
                        yamlContent
                    )
                );
            }

            if (!yamlObject.ContainsKey("variables"))
            {
                errors.Add(ValidationError.InvalidYaml("YAML frontmatter missing 'variables:' section"));
                return ProcessingResult<MarkdownDocument>.Fail(errors);
            }

            var variablesObj = yamlObject["variables"];
            if (variablesObj is not Dictionary<object, object> varDict)
            {
                errors.Add(ValidationError.InvalidYaml("'variables' must be a dictionary"));
                return ProcessingResult<MarkdownDocument>.Fail(errors);
            }

            // Convert Dictionary<object, object> to Dictionary<string, object>
            yamlData = varDict.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => kvp.Value
            );
        }
        catch (YamlException ex)
        {
            errors.Add(ValidationError.InvalidYaml($"YAML parsing error: {ex.Message}"));
            return ProcessingResult<MarkdownDocument>.Fail(errors);
        }
        catch (Exception ex)
        {
            errors.Add(ValidationError.InvalidYaml($"Unexpected error parsing YAML: {ex.Message}"));
            return ProcessingResult<MarkdownDocument>.Fail(errors);
        }

        // Step 4: Convert to VariableDefinitions and validate
        var variables = new Dictionary<string, VariableDefinition>(StringComparer.Ordinal);
        var rawNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, val) in yamlData)
        {
            // Validate variable name format
            if (!VarNameRegex.IsMatch(key))
            {
                errors.Add(ValidationError.InvalidFormat(
                    key,
                    "Variable name must be UPPERCASE with optional dot-separated segments (e.g., USER_NAME or USER.EMAIL)"
                ));
                continue;
            }

            rawNames.Add(key);

            var parsed = ParseVariableDefinition(key, val);
            if (parsed.Error != null)
            {
                errors.Add(parsed.Error);
                continue;
            }

            if (parsed.Definition != null)
            {
                variables[key] = parsed.Definition;
            }
        }

        // Step 5: Conflict detection - cannot have both X and X.Y
        foreach (var name in rawNames)
        {
            var prefix = name + ".";
            var conflicts = rawNames.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).ToList();

            if (conflicts.Any())
            {
                errors.Add(ValidationError.InvalidFormat(
                    name,
                    $"Conflicting variable paths: '{name}' conflicts with '{string.Join("', '", conflicts)}'. Cannot define both a variable and its nested path."
                ));
            }
        }

        // Step 6: Validate optional variables have defaults
        foreach (var v in variables.Values)
        {
            if (!v.Required && v.DefaultValue == null)
            {
                errors.Add(ValidationError.InvalidYaml(
                    $"Optional variable '{v.Name}' must have a default value"
                ));
            }
        }

        if (errors.Any())
        {
            return ProcessingResult<MarkdownDocument>.Fail(errors);
        }

        return ProcessingResult<MarkdownDocument>.Ok(
            new MarkdownDocument(variables, markdownContent, yamlContent)
        );
    }

    /// <summary>
    /// Splits content into YAML frontmatter and markdown body.
    /// Frontmatter must be delimited by --- at the start and end.
    /// </summary>
    /// <param name="content">The full markdown content</param>
    /// <returns>Tuple of (hasYaml, yamlContent, markdownContent)</returns>
    public static (bool hasYaml, string yaml, string content) SplitFrontmatter(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return (false, string.Empty, string.Empty);
        }

        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("---"))
        {
            return (false, string.Empty, content);
        }

        var lines = content.Split('\n');
        int startIndex = -1, endIndex = -1;

        // Find first ---
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex == -1)
        {
            return (false, string.Empty, content);
        }

        // Find closing ---
        for (int i = startIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex == -1)
        {
            return (false, string.Empty, content);
        }

        // Extract YAML and content
        var yaml = string.Join('\n', lines.Skip(startIndex + 1).Take(endIndex - startIndex - 1));
        var body = string.Join('\n', lines.Skip(endIndex + 1));

        return (true, yaml, body);
    }

    /// <summary>
    /// Parses a single variable definition from YAML.
    /// Supports both simple string format (required variable) and object format (optional with defaults).
    /// </summary>
    private (VariableDefinition? Definition, ValidationError? Error) ParseVariableDefinition(
        string name,
        object value)
    {
        // Format 1: Simple string - required variable
        if (value is string s)
        {
            return (new VariableDefinition(name, s, required: true, defaultValue: null), null);
        }

        // Format 2: Object with description, required, default
        if (value is Dictionary<object, object> objDict)
        {
            var dict = objDict.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => kvp.Value
            );

            if (!dict.ContainsKey("description"))
            {
                return (null, ValidationError.InvalidYaml(
                    $"Variable '{name}' object format must have 'description' field"
                ));
            }

            var description = dict["description"]?.ToString() ?? string.Empty;
            var required = dict.ContainsKey("required")
                ? Convert.ToBoolean(dict["required"])
                : true;
            var defaultValue = dict.ContainsKey("default") ? dict["default"] : null;

            // Validate: optional variables must have defaults
            if (!required && defaultValue == null)
            {
                return (null, ValidationError.InvalidYaml(
                    $"Optional variable '{name}' must have a default value"
                ));
            }

            return (new VariableDefinition(name, description, required, defaultValue), null);
        }

        return (null, ValidationError.InvalidYaml(
            $"Variable '{name}' must be either a string (for required variables) or an object with 'description' field"
        ));
    }
}
