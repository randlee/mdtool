namespace MDTool.Models;

/// <summary>
/// Result of validating variables against provided arguments.
/// Implements the Result pattern for structured error handling.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether validation succeeded (no errors).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// List of validation errors (empty if Success is true).
    /// </summary>
    public List<ValidationError> Errors { get; init; }

    /// <summary>
    /// Names of variables that were provided in the arguments.
    /// </summary>
    public List<string> ProvidedVariables { get; init; }

    /// <summary>
    /// Names of required variables that were not provided.
    /// </summary>
    public List<string> MissingVariables { get; init; }

    /// <summary>
    /// Private constructor - use factory methods to create instances.
    /// </summary>
    private ValidationResult(
        bool success,
        List<ValidationError> errors,
        List<string> providedVariables,
        List<string> missingVariables)
    {
        Success = success;
        Errors = errors;
        ProvidedVariables = providedVariables;
        MissingVariables = missingVariables;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="providedVariables">Variables that were provided</param>
    public static ValidationResult Ok(List<string> providedVariables)
    {
        return new ValidationResult(
            success: true,
            errors: new List<ValidationError>(),
            providedVariables: providedVariables ?? new List<string>(),
            missingVariables: new List<string>()
        );
    }

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    /// <param name="errors">List of validation errors</param>
    /// <param name="providedVariables">Variables that were provided</param>
    /// <param name="missingVariables">Required variables that were missing</param>
    public static ValidationResult Fail(
        List<ValidationError> errors,
        List<string> providedVariables,
        List<string> missingVariables)
    {
        if (errors == null || errors.Count == 0)
            throw new ArgumentException("Failed validation must have errors", nameof(errors));

        return new ValidationResult(
            success: false,
            errors: errors,
            providedVariables: providedVariables ?? new List<string>(),
            missingVariables: missingVariables ?? new List<string>()
        );
    }

    /// <summary>
    /// Creates a failed validation result from a single error.
    /// </summary>
    public static ValidationResult Fail(ValidationError error)
    {
        return Fail(
            new List<ValidationError> { error },
            new List<string>(),
            new List<string> { error.Variable ?? string.Empty }
        );
    }

    /// <summary>
    /// Combines multiple validation results into one.
    /// If any result failed, the combined result fails.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var allErrors = new List<ValidationError>();
        var allProvided = new HashSet<string>();
        var allMissing = new HashSet<string>();
        bool anyFailed = false;

        foreach (var result in results)
        {
            if (!result.Success)
            {
                anyFailed = true;
                allErrors.AddRange(result.Errors);
            }

            foreach (var provided in result.ProvidedVariables)
                allProvided.Add(provided);

            foreach (var missing in result.MissingVariables)
                allMissing.Add(missing);
        }

        if (anyFailed)
        {
            return Fail(
                allErrors,
                allProvided.ToList(),
                allMissing.ToList()
            );
        }

        return Ok(allProvided.ToList());
    }

    /// <summary>
    /// Gets a summary message of the validation result.
    /// </summary>
    public string GetSummary()
    {
        if (Success)
        {
            return $"Validation successful. {ProvidedVariables.Count} variables provided.";
        }

        return $"Validation failed. {Errors.Count} error(s), {MissingVariables.Count} missing variable(s).";
    }

    /// <summary>
    /// Converts to JSON for CLI output.
    /// </summary>
    public string ToJson()
    {
        var result = new
        {
            success = Success,
            errors = Errors.Select(e => new
            {
                type = e.Type.ToString(),
                variable = e.Variable,
                description = e.Description,
                line = e.Line
            }),
            provided = ProvidedVariables,
            missing = MissingVariables
        };

        return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
