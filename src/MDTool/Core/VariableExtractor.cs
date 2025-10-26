using System.Text.RegularExpressions;
using MDTool.Models;

namespace MDTool.Core;

/// <summary>
/// Extracts variable references from markdown content using pattern matching.
/// Supports both simple variables ({{NAME}}) and nested dot-notation variables ({{USER.EMAIL}}).
/// </summary>
public class VariableExtractor
{
    // Regex pattern: matches {{VARIABLE_NAME}} and {{USER.NAME.FIELD}}
    // - Starts with {{
    // - Captures uppercase letter followed by letters/numbers/underscores
    // - Supports dot notation for nesting
    // - Ends with }}
    private static readonly Regex VariablePattern = new(
        @"\{\{([A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*)\}\}",
        RegexOptions.Compiled | RegexOptions.Multiline
    );

    /// <summary>
    /// Extracts all unique variable names from the content.
    /// Returns a deduplicated list sorted alphabetically.
    /// </summary>
    /// <param name="content">The markdown content to extract variables from</param>
    /// <returns>List of unique variable names found in the content</returns>
    public static List<string> ExtractVariables(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new List<string>();
        }

        var variables = new HashSet<string>(StringComparer.Ordinal);
        var matches = VariablePattern.Matches(content);

        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value;
            variables.Add(variableName);
        }

        // Return sorted list for deterministic order
        return variables.OrderBy(v => v).ToList();
    }

    /// <summary>
    /// Validates that all variable references in the content follow the correct format.
    /// This checks for malformed variable syntax like unclosed braces, empty variables, etc.
    /// </summary>
    /// <param name="content">The markdown content to validate</param>
    /// <returns>ProcessingResult indicating success or containing validation errors</returns>
    public static ProcessingResult<Unit> ValidateVariableFormat(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return ProcessingResult<Unit>.Ok(Unit.Value);
        }

        var errors = new List<ValidationError>();
        var lines = content.Split('\n');

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineNumber = lineIndex + 1;

            // Check for unclosed opening braces {{
            var openBraces = Regex.Matches(line, @"\{\{");
            var closeBraces = Regex.Matches(line, @"\}\}");

            // Simple check: if we have {{ without matching }}
            if (openBraces.Count != closeBraces.Count)
            {
                errors.Add(ValidationError.InvalidFormat(
                    "{{",
                    "Unclosed or mismatched variable braces",
                    lineNumber
                ));
            }

            // Check for empty variables {{}}
            if (line.Contains("{{}}"))
            {
                errors.Add(ValidationError.InvalidFormat(
                    "{{}}",
                    "Empty variable name",
                    lineNumber
                ));
            }

            // Check for variables with invalid format (lowercase, special chars, etc.)
            var invalidPattern = new Regex(@"\{\{([^}]+)\}\}");
            var allMatches = invalidPattern.Matches(line);

            foreach (Match match in allMatches)
            {
                var varName = match.Groups[1].Value.Trim();

                // Skip if it's a valid variable (already validated by VariablePattern)
                if (VariablePattern.IsMatch(match.Value))
                {
                    continue;
                }

                // This is an invalid variable format
                errors.Add(ValidationError.InvalidFormat(
                    match.Value,
                    $"Invalid variable format: '{varName}'. Variables must be UPPERCASE with optional dot-separated segments.",
                    lineNumber
                ));
            }
        }

        if (errors.Any())
        {
            return ProcessingResult<Unit>.Fail(errors);
        }

        return ProcessingResult<Unit>.Ok(Unit.Value);
    }
}
