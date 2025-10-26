namespace MDTool.Models;

/// <summary>
/// Types of errors that can occur during validation and processing.
/// </summary>
public enum ErrorType
{
    MissingRequiredVariable,
    InvalidYamlHeader,
    InvalidJsonArgs,
    FileNotFound,
    FileSizeExceeded,
    FileExists,
    FileAccessDenied,
    FileReadError,
    FileWriteError,
    DirectoryCreationFailed,
    InvalidPath,
    PathTraversalAttempt,
    InvalidVariableFormat,
    RecursionDepthExceeded,
    UnhandledException,
    SerializationError,
    ProcessingError
}

/// <summary>
/// Represents a validation or processing error with structured information.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// The type of error that occurred.
    /// </summary>
    public ErrorType Type { get; init; }

    /// <summary>
    /// The variable name related to this error (if applicable).
    /// </summary>
    public string? Variable { get; init; }

    /// <summary>
    /// Human-readable error description.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Line number where the error occurred (if applicable).
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    private ValidationError(
        ErrorType type,
        string description,
        string? variable = null,
        int? line = null)
    {
        Type = type;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Variable = variable;
        Line = line;
    }

    /// <summary>
    /// Creates a MissingRequiredVariable error.
    /// </summary>
    public static ValidationError MissingVariable(string variable, string description)
    {
        return new ValidationError(
            ErrorType.MissingRequiredVariable,
            $"Required variable '{variable}' is missing: {description}",
            variable
        );
    }

    /// <summary>
    /// Creates an InvalidYamlHeader error.
    /// </summary>
    public static ValidationError InvalidYaml(string message, int? line = null)
    {
        return new ValidationError(
            ErrorType.InvalidYamlHeader,
            $"Invalid YAML frontmatter: {message}",
            line: line
        );
    }

    /// <summary>
    /// Creates an InvalidJsonArgs error.
    /// </summary>
    public static ValidationError InvalidJson(string message)
    {
        return new ValidationError(
            ErrorType.InvalidJsonArgs,
            $"Invalid JSON arguments: {message}"
        );
    }

    /// <summary>
    /// Creates a FileNotFound error.
    /// </summary>
    public static ValidationError FileNotFound(string path)
    {
        return new ValidationError(
            ErrorType.FileNotFound,
            $"File not found: {path}"
        );
    }

    /// <summary>
    /// Creates a FileSizeExceeded error.
    /// </summary>
    public static ValidationError FileSizeExceeded(string path, long size, long maxSize)
    {
        return new ValidationError(
            ErrorType.FileSizeExceeded,
            $"File size exceeded: {path} ({size} bytes, max {maxSize} bytes)"
        );
    }

    /// <summary>
    /// Creates a FileExists error.
    /// </summary>
    public static ValidationError FileExists(string path)
    {
        return new ValidationError(
            ErrorType.FileExists,
            $"File already exists: {path}"
        );
    }

    /// <summary>
    /// Creates a FileAccessDenied error.
    /// </summary>
    public static ValidationError FileAccessDenied(string path)
    {
        return new ValidationError(
            ErrorType.FileAccessDenied,
            $"Access denied: {path}"
        );
    }

    /// <summary>
    /// Creates a FileReadError error.
    /// </summary>
    public static ValidationError FileReadError(string path, string message)
    {
        return new ValidationError(
            ErrorType.FileReadError,
            $"Error reading file {path}: {message}"
        );
    }

    /// <summary>
    /// Creates a FileWriteError error.
    /// </summary>
    public static ValidationError FileWriteError(string path, string message)
    {
        return new ValidationError(
            ErrorType.FileWriteError,
            $"Error writing file {path}: {message}"
        );
    }

    /// <summary>
    /// Creates a DirectoryCreationFailed error.
    /// </summary>
    public static ValidationError DirectoryCreationFailed(string path, string message)
    {
        return new ValidationError(
            ErrorType.DirectoryCreationFailed,
            $"Failed to create directory {path}: {message}"
        );
    }

    /// <summary>
    /// Creates an InvalidPath error.
    /// </summary>
    public static ValidationError InvalidPath(string path, string message)
    {
        return new ValidationError(
            ErrorType.InvalidPath,
            $"Invalid path {path}: {message}"
        );
    }

    /// <summary>
    /// Creates a PathTraversalAttempt error.
    /// </summary>
    public static ValidationError PathTraversalAttempt(string path)
    {
        return new ValidationError(
            ErrorType.PathTraversalAttempt,
            $"Path traversal attempt detected: {path}"
        );
    }

    /// <summary>
    /// Creates an InvalidVariableFormat error.
    /// </summary>
    public static ValidationError InvalidFormat(string variable, string message, int? line = null)
    {
        return new ValidationError(
            ErrorType.InvalidVariableFormat,
            $"Invalid variable format '{variable}': {message}",
            variable,
            line
        );
    }

    /// <summary>
    /// Creates a RecursionDepthExceeded error.
    /// </summary>
    public static ValidationError RecursionDepthExceeded(int maxDepth)
    {
        return new ValidationError(
            ErrorType.RecursionDepthExceeded,
            $"Maximum recursion depth exceeded: {maxDepth}"
        );
    }

    /// <summary>
    /// Creates an UnhandledException error.
    /// </summary>
    public static ValidationError UnhandledException(string message)
    {
        return new ValidationError(
            ErrorType.UnhandledException,
            $"Unhandled exception: {message}"
        );
    }

    /// <summary>
    /// Creates a SerializationError error.
    /// </summary>
    public static ValidationError SerializationError(string message)
    {
        return new ValidationError(
            ErrorType.SerializationError,
            $"Serialization error: {message}"
        );
    }

    /// <summary>
    /// Creates a generic processing error.
    /// </summary>
    public static ValidationError ProcessingError(string message)
    {
        return new ValidationError(
            ErrorType.ProcessingError,
            message
        );
    }

    /// <summary>
    /// Converts to JSON object for serialization.
    /// </summary>
    public object ToJsonObject()
    {
        return new
        {
            type = Type.ToString(),
            variable = Variable,
            description = Description,
            line = Line
        };
    }

    public override string ToString()
    {
        var location = Line.HasValue ? $" (line {Line})" : "";
        var variable = !string.IsNullOrEmpty(Variable) ? $" [{Variable}]" : "";
        return $"{Type}{variable}{location}: {Description}";
    }
}
