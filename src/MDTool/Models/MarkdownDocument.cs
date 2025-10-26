namespace MDTool.Models;

/// <summary>
/// Represents a markdown document with YAML frontmatter variable definitions.
/// </summary>
public class MarkdownDocument
{
    /// <summary>
    /// Dictionary of variable definitions from the YAML frontmatter.
    /// Key is the uppercase variable name (e.g., "USER_NAME").
    /// </summary>
    public Dictionary<string, VariableDefinition> Variables { get; init; }

    /// <summary>
    /// The markdown content without the YAML frontmatter.
    /// This is the body of the document that will be processed for substitution.
    /// </summary>
    public string Content { get; init; }

    /// <summary>
    /// The raw YAML frontmatter text, including the --- delimiters.
    /// Useful for debugging and error reporting.
    /// </summary>
    public string? RawYaml { get; init; }

    /// <summary>
    /// Creates a new MarkdownDocument instance.
    /// </summary>
    /// <param name="variables">Variable definitions from YAML frontmatter</param>
    /// <param name="content">Markdown content (without frontmatter)</param>
    /// <param name="rawYaml">Raw YAML text (optional)</param>
    public MarkdownDocument(
        Dictionary<string, VariableDefinition> variables,
        string content,
        string? rawYaml = null)
    {
        Variables = variables ?? throw new ArgumentNullException(nameof(variables));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        RawYaml = rawYaml;
    }

    /// <summary>
    /// Creates an empty MarkdownDocument with no variables.
    /// Useful for documents without YAML frontmatter.
    /// </summary>
    public static MarkdownDocument Empty(string content)
    {
        return new MarkdownDocument(
            new Dictionary<string, VariableDefinition>(),
            content,
            null
        );
    }

    /// <summary>
    /// Gets all required variables that don't have default values.
    /// </summary>
    public IEnumerable<VariableDefinition> RequiredVariables =>
        Variables.Values.Where(v => v.Required);

    /// <summary>
    /// Gets all optional variables (those with default values).
    /// </summary>
    public IEnumerable<VariableDefinition> OptionalVariables =>
        Variables.Values.Where(v => !v.Required);

    /// <summary>
    /// Checks if a variable is defined in the document.
    /// </summary>
    /// <param name="name">Variable name (case-insensitive)</param>
    public bool HasVariable(string name)
    {
        return Variables.ContainsKey(name.ToUpperInvariant());
    }

    /// <summary>
    /// Gets a variable by name (case-insensitive lookup).
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <returns>Variable definition or null if not found</returns>
    public VariableDefinition? GetVariable(string name)
    {
        return Variables.TryGetValue(name.ToUpperInvariant(), out var variable)
            ? variable
            : null;
    }
}
