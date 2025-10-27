namespace MDTool.Models;

/// <summary>
/// Generic Result&lt;T&gt; pattern for operations that can succeed or fail.
/// Provides a consistent way to handle errors without exceptions.
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public class ProcessingResult<T>
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The result value (only valid if Success is true).
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// List of errors (only populated if Success is false).
    /// </summary>
    public IReadOnlyCollection<ValidationError> Errors { get; init; }

    /// <summary>
    /// Private constructor - use factory methods.
    /// </summary>
    private ProcessingResult(bool success, T value, IReadOnlyCollection<ValidationError> errors)
    {
        Success = success;
        Value = value;
        Errors = errors;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static ProcessingResult<T> Ok(T value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value), "Success value cannot be null");

        return new ProcessingResult<T>(
            success: true,
            value: value,
            errors: new List<ValidationError>()
        );
    }

    /// <summary>
    /// Creates a failed result with errors.
    /// </summary>
    public static ProcessingResult<T> Fail(IEnumerable<ValidationError> errors)
    {
        // ReSharper disable PossibleMultipleEnumeration
        if (errors == null || !errors.Any())
            throw new ArgumentException("Failed result must have errors", nameof(errors));
        
        // ReSharper disable once NullableWarningSuppressionIsUsed
        return new ProcessingResult<T>(
            success: false,
            value: default!,
            errors: errors.ToArray()
        );
        // ReSharper restore PossibleMultipleEnumeration
    }

    /// <summary>
    /// Creates a failed result from a single error.
    /// </summary>
    public static ProcessingResult<T> Fail(ValidationError error)
    {
        return Fail(new List<ValidationError> { error });
    }

    /// <summary>
    /// Creates a failed result from a ValidationResult.
    /// </summary>
    public static ProcessingResult<T> FromValidation(ValidationResult validation)
    {
        if (validation.Success)
            throw new ArgumentException("Cannot create failed result from successful validation");

        return Fail(validation.Errors);
    }

    /// <summary>
    /// Maps the value to a different type if successful.
    /// </summary>
    public ProcessingResult<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        if (!Success)
            return ProcessingResult<TOut>.Fail(Errors);

        try
        {
            // ReSharper disable once NullableWarningSuppressionIsUsed
            var mapped = mapper(Value!);
            return ProcessingResult<TOut>.Ok(mapped);
        }
        catch (Exception ex)
        {
            return ProcessingResult<TOut>.Fail(
                ValidationError.ProcessingError(ex.Message)
            );
        }
    }

    /// <summary>
    /// Executes a function if successful, otherwise returns the error.
    /// </summary>
    public ProcessingResult<TOut> Bind<TOut>(Func<T, ProcessingResult<TOut>> binder)
    {
        if (!Success)
            return ProcessingResult<TOut>.Fail(Errors);

        // ReSharper disable once NullableWarningSuppressionIsUsed
        return binder(Value!);
    }

    /// <summary>
    /// Gets the value or throws an exception with all error messages.
    /// Use sparingly - prefer pattern matching on Success.
    /// </summary>
    public T GetValueOrThrow()
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        if (Success) return Value!;
        var errorMessages = string.Join("; ", Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Cannot get value from failed result: {errorMessages}");
    }

    /// <summary>
    /// Converts to JSON for CLI output.
    /// </summary>
    public string ToJson()
    {
        object result;
        if (Success)
        {
            result = new { success = true, result = Value };
        }
        else
        {
            result = new { success = false, errors = Errors.Select(e => new
                {
                    type = e.Type.ToString(),
                    variable = e.Variable,
                    description = e.Description,
                    line = e.Line
                })
            };
        }

        return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

/// <summary>
/// Non-generic ProcessingResult for operations that don't return a value.
/// </summary>
public class ProcessingResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// List of errors (only populated if Success is false).
    /// </summary>
    public List<ValidationError> Errors { get; init; }

    /// <summary>
    /// Private constructor - use factory methods.
    /// </summary>
    private ProcessingResult(bool success, List<ValidationError> errors)
    {
        Success = success;
        Errors = errors;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ProcessingResult Ok()
    {
        return new ProcessingResult(true, new List<ValidationError>());
    }

    /// <summary>
    /// Creates a failed result with errors.
    /// </summary>
    public static ProcessingResult Fail(List<ValidationError> errors)
    {
        if (errors == null || errors.Count == 0)
            throw new ArgumentException("Failed result must have errors", nameof(errors));

        return new ProcessingResult(false, errors);
    }

    /// <summary>
    /// Creates a failed result from a single error.
    /// </summary>
    public static ProcessingResult Fail(ValidationError error) => Fail([error]);

    /// <summary>
    /// Creates a failed result from a ValidationResult.
    /// </summary>
    public static ProcessingResult FromValidation(ValidationResult validation)
    {
        if (validation.Success)
            throw new ArgumentException("Cannot create failed result from successful validation");

        return Fail(validation.Errors);
    }

    /// <summary>
    /// Converts to JSON for CLI output.
    /// </summary>
    public string ToJson()
    {
        object result;
        if (Success)
        {
            result = new { success = true };
        }
        else
        {
            result = new { success = false, errors = Errors.Select(e => new
                {
                    type = e.Type.ToString(),
                    variable = e.Variable,
                    description = e.Description,
                    line = e.Line
                })
            };
        }

        return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
