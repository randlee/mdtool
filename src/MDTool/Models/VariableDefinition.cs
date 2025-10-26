using System.Text.RegularExpressions;

namespace MDTool.Models;

/// <summary>
/// Represents a variable definition from YAML frontmatter.
/// Supports both simple string format and object format.
/// </summary>
public class VariableDefinition
{
    /// <summary>
    /// The variable name in uppercase snake-case with optional dot-separated segments.
    /// Must match pattern: ^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Human-readable description of the variable's purpose.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Whether this variable must be provided (has no default value).
    /// Defaults to true.
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// The default value if the variable is not provided.
    /// If null, the variable is required.
    /// Type is inferred from this value (string, int, bool, etc.)
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Validates variable name format: uppercase snake-case with underscores.
    /// Pattern: ^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$
    /// </summary>
    private static readonly Regex VarNameRegex = new(
        @"^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Creates a variable definition with validation.
    /// </summary>
    /// <param name="name">Variable name (uppercase snake-case)</param>
    /// <param name="description">Variable description</param>
    /// <param name="required">Whether variable is required</param>
    /// <param name="defaultValue">Default value (null if required)</param>
    public VariableDefinition(
        string name,
        string description,
        bool required = true,
        object? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be empty", nameof(name));

        // Normalize to uppercase first
        var normalizedName = name.ToUpperInvariant();

        // Then validate the normalized name
        if (!IsValidVariableName(normalizedName))
            throw new ArgumentException(
                $"Variable name '{name}' must be uppercase snake-case (A-Z, 0-9, underscores) with optional dot-separated segments",
                nameof(name));

        if (!required && defaultValue == null)
            throw new ArgumentException(
                $"Optional variable '{normalizedName}' must have a default value",
                nameof(name));

        Name = normalizedName;
        Description = description ?? string.Empty;
        Required = required;
        DefaultValue = defaultValue;
    }

    private static bool IsValidVariableName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && VarNameRegex.IsMatch(name);
    }

    /// <summary>
    /// Infers the type of this variable from its default value.
    /// </summary>
    public Type? InferredType => DefaultValue?.GetType();

    /// <summary>
    /// Gets a string representation of the inferred type.
    /// </summary>
    public string TypeName => InferredType?.Name ?? "string";

    /// <summary>
    /// Creates a required variable from a simple description string.
    /// YAML format: NAME: "description"
    /// </summary>
    public static VariableDefinition RequiredVariable(string name, string description)
    {
        return new VariableDefinition(name, description, required: true);
    }

    /// <summary>
    /// Creates an optional variable with a default value.
    /// YAML format: NAME: { description: "...", default: value }
    /// </summary>
    public static VariableDefinition OptionalVariable(string name, string description, object defaultValue)
    {
        return new VariableDefinition(name, description, required: false, defaultValue);
    }
}
