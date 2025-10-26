# Models Namespace Design Document

**Project:** MDTool - Markdown Processing with Variable Substitution
**Namespace:** `MDTool.Models`
**Version:** 1.0.0
**Last Updated:** 2025-10-25

---

## Table of Contents

1. [Overview](#overview)
2. [MarkdownDocument Class](#markdowndocument-class)
3. [VariableDefinition Class](#variabledefinition-class)
4. [ValidationResult Class](#validationresult-class)
5. [ProcessingResult Class](#processingresult-class)
6. [ValidationError Class](#validationerror-class)
7. [Class Relationships](#class-relationships)
8. [Design Patterns Used](#design-patterns-used)
9. [Code Examples](#code-examples)

---

## Overview

The Models namespace contains the core data structures used throughout MDTool. These classes represent:

- **MarkdownDocument**: Parsed markdown files with YAML frontmatter
- **VariableDefinition**: Metadata about template variables
- **ValidationResult**: Results of variable validation operations
- **ProcessingResult**: Generic result wrapper for operations
- **ValidationError**: Structured error information

### Purpose

The Models namespace serves as the foundation for:
1. **Type Safety**: Strongly-typed representations of parsed data
2. **Validation**: Structured validation results with detailed error information
3. **Error Handling**: Result pattern implementation for predictable error flows
4. **Serialization**: JSON-friendly structures for CLI output
5. **Maintainability**: Clear contracts between components

### Design Principles

- **Immutability**: Where possible, properties should be init-only or read-only
- **Validation**: All models validate their state in constructors or factory methods
- **Serialization**: All models support JSON serialization for CLI output
- **No Business Logic**: Models contain only data and simple validation, no complex operations
- **Result Pattern**: Use Result<T> instead of exceptions for expected failures

---

## MarkdownDocument Class

### Purpose

Represents a parsed markdown file with YAML frontmatter containing variable definitions.

### Class Definition

```csharp
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
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Variables` | `Dictionary<string, VariableDefinition>` | Variable definitions from YAML frontmatter, keyed by uppercase name |
| `Content` | `string` | Markdown content without the YAML frontmatter |
| `RawYaml` | `string?` | Raw YAML frontmatter text (optional, for debugging) |

### Validation Rules

1. **Variables cannot be null**: Constructor throws `ArgumentNullException` if variables dictionary is null
2. **Content cannot be null**: Constructor throws `ArgumentNullException` if content is null
3. **Empty variables allowed**: Documents without frontmatter have empty dictionary
4. **RawYaml is optional**: Can be null for documents without frontmatter

### Usage Examples

#### Creating a MarkdownDocument

```csharp
// With variables
var variables = new Dictionary<string, VariableDefinition>
{
    ["USER_NAME"] = new VariableDefinition
    {
        Name = "USER_NAME",
        Description = "The user's full name",
        Required = true
    },
    ["EMAIL"] = new VariableDefinition
    {
        Name = "EMAIL",
        Description = "User email address",
        Required = true
    }
};

var doc = new MarkdownDocument(
    variables,
    "# Welcome {{USER_NAME}}!\n\nContact: {{EMAIL}}",
    "---\nvariables:\n  USER_NAME: \"The user's full name\"\n---"
);

// Without variables (no frontmatter)
var simpleDoc = MarkdownDocument.Empty("# Simple document");
```

#### Querying Variables

```csharp
// Check if variable exists
if (doc.HasVariable("USER_NAME"))
{
    Console.WriteLine("Document requires USER_NAME");
}

// Get variable definition
var userVar = doc.GetVariable("user_name"); // Case-insensitive
if (userVar != null)
{
    Console.WriteLine($"Description: {userVar.Description}");
}

// Get all required variables
foreach (var variable in doc.RequiredVariables)
{
    Console.WriteLine($"Required: {variable.Name}");
}

// Get all optional variables
foreach (var variable in doc.OptionalVariables)
{
    Console.WriteLine($"Optional: {variable.Name} = {variable.DefaultValue}");
}
```

---

## VariableDefinition Class

### Purpose

Represents metadata about a template variable defined in YAML frontmatter. Supports both simple string format and object format with defaults.

### Class Definition

```csharp
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

        if (!IsValidVariableName(name))
            throw new ArgumentException(
                $"Variable name '{name}' must be uppercase snake-case (A-Z, 0-9, underscores)",
                nameof(name));

        if (!required && defaultValue == null)
            throw new ArgumentException(
                $"Optional variable '{name}' must have a default value",
                nameof(name));

        Name = name.ToUpperInvariant();
        Description = description ?? string.Empty;
        Required = required;
        DefaultValue = defaultValue;
    }

    /// <summary>
    /// Validates variable name format: uppercase snake-case with underscores.
    /// Pattern: [A-Z][A-Z0-9_]*
    /// </summary>
private static readonly Regex VarNameRegex = new(
    @"^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$",
    RegexOptions.Compiled);

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
    public static VariableDefinition Required(string name, string description)
    {
        return new VariableDefinition(name, description, required: true);
    }

    /// <summary>
    /// Creates an optional variable with a default value.
    /// YAML format: NAME: { description: "...", default: value }
    /// </summary>
    public static VariableDefinition Optional(string name, string description, object defaultValue)
    {
        return new VariableDefinition(name, description, required: false, defaultValue);
    }
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Variable name in uppercase snake-case (e.g., "USER_NAME") |
| `Description` | `string` | Human-readable description of the variable |
| `Required` | `bool` | Whether the variable must be provided (defaults to `true`) |
| `DefaultValue` | `object?` | Default value if not provided (null if required) |
| `InferredType` | `Type?` | Type inferred from default value |
| `TypeName` | `string` | String representation of inferred type |

### Type Inference

The `DefaultValue` property is used to infer the variable's type:

```csharp
// String type
var name = VariableDefinition.Optional("NAME", "User name", "John Doe");
// name.InferredType == typeof(string)

// Integer type
var port = VariableDefinition.Optional("PORT", "Server port", 8080);
// port.InferredType == typeof(int)

// Boolean type
var debug = VariableDefinition.Optional("DEBUG", "Debug mode", false);
// debug.InferredType == typeof(bool)
```

### Validation Rules

1. **Name format**: Must match `^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$` (UPPER_SNAKE with optional dot segments)
2. **Name required**: Cannot be null or whitespace
3. **Optional variables must have defaults**: If `Required=false`, `DefaultValue` cannot be null
4. **Name normalization**: Names are automatically converted to uppercase

### YAML Format Support

#### Simple String Format (Required Variable)

```yaml
---
variables:
  USER_NAME: "The user's full name"
  EMAIL: "User email address"
---
```

Maps to:
```csharp
VariableDefinition.Required("USER_NAME", "The user's full name")
VariableDefinition.Required("EMAIL", "User email address")
```

#### Object Format (Optional Variable)

```yaml
---
variables:
  BRANCH:
    description: "Git branch to deploy"
    required: false
    default: "main"
  PORT:
    description: "Server port number"
    default: 8080
---
```

Maps to:
```csharp
VariableDefinition.Optional("BRANCH", "Git branch to deploy", "main")
VariableDefinition.Optional("PORT", "Server port number", 8080)
```

### Usage Examples

```csharp
// Create required variable
var userName = VariableDefinition.Required(
    "USER_NAME",
    "The user's full name"
);

// Create optional variable with string default
var branch = VariableDefinition.Optional(
    "BRANCH",
    "Git branch to deploy",
    "main"
);

// Create optional variable with integer default
var port = VariableDefinition.Optional(
    "PORT",
    "Server port",
    8080
);

// Validation - this throws ArgumentException
try
{
    var invalid = new VariableDefinition(
        "user_name",  // Invalid: not uppercase
        "Description"
    );
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message); // "must be uppercase snake-case"
}

// Optional without default - throws ArgumentException
try
{
    var invalid = new VariableDefinition(
        "OPTIONAL_VAR",
        "Description",
        required: false,
        defaultValue: null  // Invalid: optional must have default
    );
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message); // "must have a default value"
}
```

---

## ValidationResult Class

### Purpose

Represents the result of validating variables against provided arguments. Implements the Result pattern for structured error handling.

### Class Definition

```csharp
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
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | Whether validation succeeded (no errors) |
| `Errors` | `List<ValidationError>` | List of validation errors (empty if successful) |
| `ProvidedVariables` | `List<string>` | Names of variables that were provided |
| `MissingVariables` | `List<string>` | Names of required variables that were missing |

### Factory Methods

| Method | Purpose |
|--------|---------|
| `Ok(providedVariables)` | Creates successful result |
| `Fail(errors, provided, missing)` | Creates failed result with multiple errors |
| `Fail(error)` | Creates failed result with single error |
| `Combine(results...)` | Combines multiple results into one |

### Error Collection Pattern

ValidationResult collects ALL errors instead of failing on the first error:

```csharp
var errors = new List<ValidationError>();

// Collect all missing variables
foreach (var required in requiredVariables)
{
    if (!args.ContainsKey(required.Name))
    {
        errors.Add(ValidationError.MissingVariable(
            required.Name,
            required.Description
        ));
    }
}

// Return all errors at once
if (errors.Any())
{
    return ValidationResult.Fail(
        errors,
        providedVariables,
        missingVariables
    );
}

return ValidationResult.Ok(providedVariables);
```

### Usage Examples

```csharp
// Successful validation
var provided = new List<string> { "USER_NAME", "EMAIL", "PORT" };
var success = ValidationResult.Ok(provided);
Console.WriteLine(success.GetSummary());
// Output: "Validation successful. 3 variables provided."

// Failed validation - single error
var error = ValidationError.MissingVariable("USER_NAME", "The user's full name");
var failed = ValidationResult.Fail(error);
Console.WriteLine(failed.ToJson());

// Failed validation - multiple errors
var errors = new List<ValidationError>
{
    ValidationError.MissingVariable("USER_NAME", "User's name"),
    ValidationError.MissingVariable("EMAIL", "User's email"),
    ValidationError.InvalidFormat("INVALID{{VAR", "Malformed variable syntax", 5)
};

var result = ValidationResult.Fail(
    errors,
    provided: new List<string> { "PORT" },
    missing: new List<string> { "USER_NAME", "EMAIL" }
);

Console.WriteLine(result.ToJson());
// Output:
// {
//   "success": false,
//   "errors": [
//     {
//       "type": "MissingRequiredVariable",
//       "variable": "USER_NAME",
//       "description": "User's name",
//       "line": null
//     },
//     ...
//   ],
//   "provided": ["PORT"],
//   "missing": ["USER_NAME", "EMAIL"]
// }

// Combining results
var result1 = ValidationResult.Ok(new List<string> { "VAR1" });
var result2 = ValidationResult.Fail(
    ValidationError.MissingVariable("VAR2", "Description")
);

var combined = ValidationResult.Combine(result1, result2);
// combined.Success == false (one failed)
// combined.Errors.Count == 1
// combined.ProvidedVariables contains "VAR1"
// combined.MissingVariables contains "VAR2"
```

---

### Unit Type

```csharp
namespace MDTool.Models;

public readonly struct Unit
{
    public static readonly Unit Value = new();
}
```

## ProcessingResult Class

### Purpose

Generic Result<T> wrapper for operations that can succeed with a value or fail with errors. Used throughout MDTool for predictable error handling.

### Class Definition

```csharp
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
    public List<ValidationError> Errors { get; init; }

    /// <summary>
    /// Private constructor - use factory methods.
    /// </summary>
    private ProcessingResult(bool success, T? value, List<ValidationError> errors)
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
    public static ProcessingResult<T> Fail(List<ValidationError> errors)
    {
        if (errors == null || errors.Count == 0)
            throw new ArgumentException("Failed result must have errors", nameof(errors));

        return new ProcessingResult<T>(
            success: false,
            value: default,
            errors: errors
        );
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

        return binder(Value!);
    }

    /// <summary>
    /// Gets the value or throws an exception with all error messages.
    /// Use sparingly - prefer pattern matching on Success.
    /// </summary>
    public T GetValueOrThrow()
    {
        if (!Success)
        {
            var errorMessages = string.Join("; ", Errors.Select(e => e.Description));
            throw new InvalidOperationException(
                $"Cannot get value from failed result: {errorMessages}"
            );
        }

        return Value!;
    }

    /// <summary>
    /// Converts to JSON for CLI output.
    /// </summary>
    public string ToJson()
    {
        var result = Success
            ? new { success = true, result = Value }
            : new { success = false, errors = Errors.Select(e => new
                {
                    type = e.Type.ToString(),
                    variable = e.Variable,
                    description = e.Description,
                    line = e.Line
                })
            };

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
    public bool Success { get; init; }
    public List<ValidationError> Errors { get; init; }

    private ProcessingResult(bool success, List<ValidationError> errors)
    {
        Success = success;
        Errors = errors;
    }

    public static ProcessingResult Ok()
    {
        return new ProcessingResult(true, new List<ValidationError>());
    }

    public static ProcessingResult Fail(List<ValidationError> errors)
    {
        if (errors == null || errors.Count == 0)
            throw new ArgumentException("Failed result must have errors", nameof(errors));

        return new ProcessingResult(false, errors);
    }

    public static ProcessingResult Fail(ValidationError error)
    {
        return Fail(new List<ValidationError> { error });
    }
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | Whether the operation succeeded |
| `Value` | `T?` | Result value (only valid if Success is true) |
| `Errors` | `List<ValidationError>` | List of errors (only populated if Success is false) |

### Factory Methods

| Method | Purpose |
|--------|---------|
| `Ok(value)` | Creates successful result with value |
| `Fail(errors)` | Creates failed result with errors |
| `Fail(error)` | Creates failed result with single error |
| `FromValidation(validation)` | Creates failed result from ValidationResult |

### Functional Methods

| Method | Purpose |
|--------|---------|
| `Map<TOut>(mapper)` | Transforms value if successful |
| `Bind<TOut>(binder)` | Chains operations that return Results |
| `GetValueOrThrow()` | Gets value or throws (use sparingly) |

### Integration with ValidationResult

```csharp
// Convert ValidationResult to ProcessingResult
ValidationResult validation = Validate(document, args);
if (!validation.Success)
{
    return ProcessingResult<string>.FromValidation(validation);
}

// Continue with processing
string processed = SubstituteVariables(content, args);
return ProcessingResult<string>.Ok(processed);
```

### Error Propagation

```csharp
// Chain operations with error propagation
ProcessingResult<MarkdownDocument> ParseDocument(string path)
{
    if (!File.Exists(path))
    {
        return ProcessingResult<MarkdownDocument>.Fail(
            ValidationError.FileNotFound(path)
        );
    }

    try
    {
        var content = File.ReadAllText(path);
        var doc = Parser.Parse(content);
        return ProcessingResult<MarkdownDocument>.Ok(doc);
    }
    catch (Exception ex)
    {
        return ProcessingResult<MarkdownDocument>.Fail(
            ValidationError.InvalidYaml(ex.Message)
        );
    }
}

ProcessingResult<string> ProcessDocument(string path, Dictionary<string, object> args)
{
    // Parse document
    var parseResult = ParseDocument(path);
    if (!parseResult.Success)
        return ProcessingResult<string>.Fail(parseResult.Errors);

    // Validate args
    var validation = Validate(parseResult.Value, args);
    if (!validation.Success)
        return ProcessingResult<string>.FromValidation(validation);

    // Substitute variables
    var substituted = Substitute(parseResult.Value.Content, args);
    return ProcessingResult<string>.Ok(substituted);
}
```

### Usage Examples

```csharp
// Successful processing
var result = ProcessingResult<string>.Ok("Processed content");
if (result.Success)
{
    Console.WriteLine(result.Value);
}

// Failed processing
var error = ValidationError.FileNotFound("template.md");
var failed = ProcessingResult<string>.Fail(error);
Console.WriteLine(failed.ToJson());

// Mapping values
var intResult = ProcessingResult<int>.Ok(42);
var stringResult = intResult.Map(x => x.ToString());
// stringResult.Success == true
// stringResult.Value == "42"

// Chaining operations
ProcessingResult<MarkdownDocument> docResult = ParseFile("template.md");
ProcessingResult<string> processedResult = docResult.Bind(doc =>
{
    var validation = Validate(doc, args);
    if (!validation.Success)
        return ProcessingResult<string>.FromValidation(validation);

    var content = Substitute(doc.Content, args);
    return ProcessingResult<string>.Ok(content);
});

// Pattern matching
var result = ProcessFile("template.md");
var output = result.Success
    ? result.Value
    : $"Error: {string.Join(", ", result.Errors.Select(e => e.Description))}";
```

---

## ValidationError Class

### Purpose

Represents a structured error with type information, variable context, and location details. Used for consistent error reporting across all commands.

### Class Definition

```csharp
namespace MDTool.Models;

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
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `ErrorType` | The type of error (from enum) |
| `Variable` | `string?` | Variable name related to error (if applicable) |
| `Description` | `string` | Human-readable error description |
| `Line` | `int?` | Line number where error occurred (if applicable) |

### Error Type Enum

| Error Type | Purpose | Example |
|------------|---------|---------|
| `MissingRequiredVariable` | Required variable not provided | USER_NAME not in args.json |
| `InvalidYamlHeader` | YAML parsing failed | Malformed YAML syntax |
| `InvalidJsonArgs` | JSON parsing failed | args.json is not valid JSON |
| `FileNotFound` | File doesn't exist | template.md not found |
| `InvalidVariableFormat` | Malformed variable syntax | Unclosed `{{VAR` |
| `ProcessingError` | Generic error | Any other processing failure |

### Factory Methods

| Method | Purpose |
|--------|---------|
| `MissingVariable(variable, description)` | Creates missing variable error |
| `InvalidYaml(message, line)` | Creates YAML parsing error |
| `InvalidJson(message)` | Creates JSON parsing error |
| `FileNotFound(path)` | Creates file not found error |
| `InvalidFormat(variable, message, line)` | Creates invalid format error |
| `ProcessingError(message)` | Creates generic error |

### JSON Serialization

```csharp
var error = ValidationError.MissingVariable("USER_NAME", "The user's full name");
var json = error.ToJsonObject();
// {
//   "type": "MissingRequiredVariable",
//   "variable": "USER_NAME",
//   "description": "Required variable 'USER_NAME' is missing: The user's full name",
//   "line": null
// }
```

### Usage Examples

```csharp
// Missing required variable
var error1 = ValidationError.MissingVariable(
    "USER_NAME",
    "The user's full name"
);
Console.WriteLine(error1);
// Output: MissingRequiredVariable [USER_NAME]: Required variable 'USER_NAME' is missing: The user's full name

// Invalid YAML with line number
var error2 = ValidationError.InvalidYaml(
    "Unexpected character ':' at column 5",
    line: 3
);
Console.WriteLine(error2);
// Output: InvalidYamlHeader (line 3): Invalid YAML frontmatter: Unexpected character ':' at column 5

// Invalid JSON
var error3 = ValidationError.InvalidJson(
    "Unexpected token '}' at position 42"
);

// File not found
var error4 = ValidationError.FileNotFound("/path/to/template.md");

// Invalid variable format
var error5 = ValidationError.InvalidFormat(
    "{{UNCLOSED",
    "Variable not properly closed with }}",
    line: 15
);
Console.WriteLine(error5);
// Output: InvalidVariableFormat [{{UNCLOSED] (line 15): Invalid variable format '{{UNCLOSED': Variable not properly closed with }}

// Collecting multiple errors
var errors = new List<ValidationError>
{
    ValidationError.MissingVariable("USER_NAME", "User's name"),
    ValidationError.MissingVariable("EMAIL", "User's email"),
    ValidationError.InvalidFormat("{{BAD}}", "Unknown variable", 10)
};

var result = ValidationResult.Fail(
    errors,
    provided: new List<string> { "PORT" },
    missing: new List<string> { "USER_NAME", "EMAIL" }
);
```

---

## Class Relationships

### Dependency Diagram

```
┌─────────────────────┐
│ MarkdownDocument    │
│                     │
│ - Variables: Dict   │───────┐
│ - Content: string   │       │
│ - RawYaml: string?  │       │
└─────────────────────┘       │
                              │ contains
                              │
                              ▼
                    ┌──────────────────────┐
                    │ VariableDefinition   │
                    │                      │
                    │ - Name: string       │
                    │ - Description: string│
                    │ - Required: bool     │
                    │ - DefaultValue: obj? │
                    └──────────────────────┘

┌─────────────────────┐       ┌──────────────────┐
│ ValidationResult    │       │ ValidationError  │
│                     │       │                  │
│ - Success: bool     │───────│ - Type: enum     │
│ - Errors: List      │contains│ - Variable: str? │
│ - Provided: List    │       │ - Description    │
│ - Missing: List     │       │ - Line: int?     │
└─────────────────────┘       └──────────────────┘
         △                             △
         │                             │
         │ uses                        │ contains
         │                             │
┌─────────────────────┐               │
│ ProcessingResult<T> │               │
│                     │───────────────┘
│ - Success: bool     │
│ - Value: T?         │
│ - Errors: List      │
└─────────────────────┘
```

### Relationships

1. **MarkdownDocument → VariableDefinition** (Composition)
   - MarkdownDocument contains a dictionary of VariableDefinition objects
   - One-to-many relationship

2. **ValidationResult → ValidationError** (Composition)
   - ValidationResult contains a list of ValidationError objects
   - Collects multiple errors for comprehensive feedback

3. **ProcessingResult → ValidationError** (Composition)
   - ProcessingResult uses ValidationError for error reporting
   - Can be created from ValidationResult

4. **ProcessingResult → ValidationResult** (Conversion)
   - ProcessingResult.FromValidation() converts ValidationResult to ProcessingResult
   - Enables error propagation between validation and processing phases

### Data Flow

```
Input File
    │
    ▼
┌─────────────┐
│ Parser      │
└─────────────┘
    │
    ▼ returns
┌──────────────────────┐
│ ProcessingResult     │
│ <MarkdownDocument>   │
└──────────────────────┘
    │
    │ if Success
    ▼
┌──────────────────────┐
│ MarkdownDocument     │
│ + VariableDefinitions│
└──────────────────────┘
    │
    │ + JSON Args
    ▼
┌─────────────┐
│ Validator   │
└─────────────┘
    │
    ▼ returns
┌──────────────────┐
│ ValidationResult │
│ + Errors         │
└──────────────────┘
    │
    │ if Success
    ▼
┌──────────────┐
│ Substitutor  │
└──────────────┘
    │
    ▼ returns
┌──────────────────────┐
│ ProcessingResult     │
│ <string>             │
└──────────────────────┘
```

---

## Design Patterns Used

### 1. Result Pattern (Railway-Oriented Programming)

Instead of throwing exceptions for expected failures, use Result types:

```csharp
// Bad: Exception-based
public string ProcessDocument(string path)
{
    if (!File.Exists(path))
        throw new FileNotFoundException(path);
    // ...
}

// Good: Result pattern
public ProcessingResult<string> ProcessDocument(string path)
{
    if (!File.Exists(path))
        return ProcessingResult<string>.Fail(
            ValidationError.FileNotFound(path)
        );
    // ...
}
```

**Benefits:**
- Explicit error handling in type signatures
- No hidden control flow via exceptions
- Easy error composition and propagation
- Predictable behavior for CLI tools

### 2. Value Objects

VariableDefinition and ValidationError are value objects:

```csharp
// Immutable properties
public string Name { get; init; }

// Validation in constructor
public VariableDefinition(string name, ...)
{
    if (!IsValidVariableName(name))
        throw new ArgumentException(...);
    // ...
}
```

**Benefits:**
- Immutability prevents accidental changes
- Validation ensures invariants
- Self-contained, testable objects

### 3. Factory Methods

Use static factory methods instead of public constructors:

```csharp
// Instead of: new ValidationResult(...)
var success = ValidationResult.Ok(providedVars);
var failure = ValidationResult.Fail(errors, provided, missing);

// Clear intent, enforces correct usage
```

**Benefits:**
- Named methods convey intent
- Can enforce constraints
- Allow different construction paths

### 4. Builder Pattern (Implicit)

ValidationResult and ProcessingResult collect errors:

```csharp
var errors = new List<ValidationError>();

// Collect all errors
foreach (var required in requiredVars)
{
    if (!provided.Contains(required))
        errors.Add(ValidationError.MissingVariable(required, desc));
}

// Build final result
if (errors.Any())
    return ValidationResult.Fail(errors, provided, missing);
```

**Benefits:**
- Report all errors at once
- Better developer experience
- Avoid multiple validation passes

### 5. Strategy Pattern (Type Inference)

VariableDefinition infers type from DefaultValue:

```csharp
public Type? InferredType => DefaultValue?.GetType();

// Different types handled automatically
var stringVar = VariableDefinition.Optional("NAME", "...", "default");  // string
var intVar = VariableDefinition.Optional("PORT", "...", 8080);          // int
var boolVar = VariableDefinition.Optional("DEBUG", "...", false);       // bool
```

**Benefits:**
- Type-safe without explicit type annotations
- Flexible for JSON deserialization
- Natural C# type system integration

### 6. Composite Pattern (Error Collection)

ValidationResult can combine multiple results:

```csharp
var result1 = ValidateYaml(doc);
var result2 = ValidateArgs(args);
var result3 = ValidateVariables(doc, args);

var combined = ValidationResult.Combine(result1, result2, result3);
```

**Benefits:**
- Compose validation steps
- Aggregate errors from multiple sources
- Single point of failure checking

---

## Code Examples

### Complete Workflow Example

```csharp
using MDTool.Models;

// Step 1: Parse markdown document
public ProcessingResult<MarkdownDocument> ParseMarkdown(string path)
{
    if (!File.Exists(path))
    {
        return ProcessingResult<MarkdownDocument>.Fail(
            ValidationError.FileNotFound(path)
        );
    }

    try
    {
        var content = File.ReadAllText(path);
        var (yaml, body) = SplitFrontmatter(content);

        var variables = ParseYaml(yaml);
        var doc = new MarkdownDocument(variables, body, yaml);

        return ProcessingResult<MarkdownDocument>.Ok(doc);
    }
    catch (Exception ex)
    {
        return ProcessingResult<MarkdownDocument>.Fail(
            ValidationError.InvalidYaml(ex.Message)
        );
    }
}

// Step 2: Validate arguments
public ValidationResult ValidateArguments(
    MarkdownDocument doc,
    Dictionary<string, object> args)
{
    var errors = new List<ValidationError>();
    var provided = args.Keys.Select(k => k.ToUpperInvariant()).ToList();
    var missing = new List<string>();

    // Check all required variables
    foreach (var variable in doc.RequiredVariables)
    {
        if (!args.ContainsKey(variable.Name.ToLowerInvariant()))
        {
            errors.Add(ValidationError.MissingVariable(
                variable.Name,
                variable.Description
            ));
            missing.Add(variable.Name);
        }
    }

    if (errors.Any())
    {
        return ValidationResult.Fail(errors, provided, missing);
    }

    return ValidationResult.Ok(provided);
}

// Step 3: Process document
public ProcessingResult<string> ProcessMarkdown(string path, Dictionary<string, object> args)
{
    // Parse
    var parseResult = ParseMarkdown(path);
    if (!parseResult.Success)
        return ProcessingResult<string>.Fail(parseResult.Errors);

    var doc = parseResult.Value!;

    // Validate
    var validation = ValidateArguments(doc, args);
    if (!validation.Success)
        return ProcessingResult<string>.FromValidation(validation);

    // Substitute
    var substituted = SubstituteVariables(doc.Content, args);
    return ProcessingResult<string>.Ok(substituted);
}

// Usage
var args = new Dictionary<string, object>
{
    ["user_name"] = "John Doe",
    ["email"] = "john@example.com",
    ["port"] = 8080
};

var result = ProcessMarkdown("template.md", args);
if (result.Success)
{
    Console.WriteLine(result.Value);
    File.WriteAllText("output.md", result.Value);
}
else
{
    Console.WriteLine("Processing failed:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }

    // Output as JSON for CLI
    Console.WriteLine(result.ToJson());
}
```

### Error Collection Example

```csharp
public ValidationResult ValidateDocument(MarkdownDocument doc)
{
    var errors = new List<ValidationError>();

    // Validate all variable definitions
    foreach (var variable in doc.Variables.Values)
    {
        // Optional variables must have defaults
        if (!variable.Required && variable.DefaultValue == null)
        {
            errors.Add(ValidationError.ProcessingError(
                $"Optional variable '{variable.Name}' must have a default value"
            ));
        }

        // Variable names must be uppercase
        if (variable.Name != variable.Name.ToUpperInvariant())
        {
            errors.Add(ValidationError.InvalidFormat(
                variable.Name,
                "Variable name must be uppercase"
            ));
        }
    }

    // Validate content for malformed variables
    var regex = new Regex(@"\{\{([^}]*)\}\}");
    var lines = doc.Content.Split('\n');

    for (int i = 0; i < lines.Length; i++)
    {
        var matches = regex.Matches(lines[i]);
        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value.Trim();

            // Check for empty variables
            if (string.IsNullOrWhiteSpace(varName))
            {
                errors.Add(ValidationError.InvalidFormat(
                    match.Value,
                    "Empty variable name",
                    i + 1
                ));
                continue;
            }

            // Check if variable is defined
            if (!doc.HasVariable(varName))
            {
                errors.Add(ValidationError.InvalidFormat(
                    varName,
                    "Variable not defined in YAML frontmatter",
                    i + 1
                ));
            }
        }
    }

    if (errors.Any())
    {
        return ValidationResult.Fail(
            errors,
            doc.Variables.Keys.ToList(),
            new List<string>()
        );
    }

    return ValidationResult.Ok(doc.Variables.Keys.ToList());
}
```

### Functional Composition Example

```csharp
// Chain operations with Map and Bind
public ProcessingResult<string> GenerateReport(string templatePath, string argsPath)
{
    return LoadJsonArgs(argsPath)
        .Bind(args => ParseMarkdown(templatePath)
            .Bind(doc => ValidateArguments(doc, args)
                .Success
                    ? ProcessDocument(doc, args)
                    : ProcessingResult<string>.FromValidation(ValidateArguments(doc, args))
            )
        );
}

// Helper methods
ProcessingResult<Dictionary<string, object>> LoadJsonArgs(string path)
{
    if (!File.Exists(path))
        return ProcessingResult<Dictionary<string, object>>.Fail(
            ValidationError.FileNotFound(path)
        );

    try
    {
        var json = File.ReadAllText(path);
        var args = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        return ProcessingResult<Dictionary<string, object>>.Ok(args!);
    }
    catch (JsonException ex)
    {
        return ProcessingResult<Dictionary<string, object>>.Fail(
            ValidationError.InvalidJson(ex.Message)
        );
    }
}

ProcessingResult<string> ProcessDocument(MarkdownDocument doc, Dictionary<string, object> args)
{
    try
    {
        var content = SubstituteVariables(doc.Content, args);
        return ProcessingResult<string>.Ok(content);
    }
    catch (Exception ex)
    {
        return ProcessingResult<string>.Fail(
            ValidationError.ProcessingError(ex.Message)
        );
    }
}
```

### Testing Example

```csharp
using Xunit;
using MDTool.Models;

public class VariableDefinitionTests
{
    [Fact]
    public void Required_CreatesRequiredVariable()
    {
        var variable = VariableDefinition.Required("USER_NAME", "User's name");

        Assert.Equal("USER_NAME", variable.Name);
        Assert.Equal("User's name", variable.Description);
        Assert.True(variable.Required);
        Assert.Null(variable.DefaultValue);
    }

    [Fact]
    public void Optional_CreatesOptionalVariable()
    {
        var variable = VariableDefinition.Optional("PORT", "Server port", 8080);

        Assert.Equal("PORT", variable.Name);
        Assert.False(variable.Required);
        Assert.Equal(8080, variable.DefaultValue);
        Assert.Equal(typeof(int), variable.InferredType);
    }

    [Theory]
    [InlineData("user_name")]  // lowercase
    [InlineData("User-Name")]  // hyphen
    [InlineData("123NAME")]    // starts with digit
    public void Constructor_ThrowsOnInvalidName(string invalidName)
    {
        Assert.Throws<ArgumentException>(() =>
            new VariableDefinition(invalidName, "Description")
        );
    }

    [Fact]
    public void Constructor_ThrowsWhenOptionalWithoutDefault()
    {
        Assert.Throws<ArgumentException>(() =>
            new VariableDefinition("VAR", "Desc", required: false, defaultValue: null)
        );
    }
}

public class ValidationResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        var result = ValidationResult.Ok(new List<string> { "VAR1", "VAR2" });

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.ProvidedVariables.Count);
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        var error = ValidationError.MissingVariable("VAR", "Description");
        var result = ValidationResult.Fail(error);

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("VAR", result.MissingVariables);
    }

    [Fact]
    public void Combine_AggregatesMultipleResults()
    {
        var result1 = ValidationResult.Ok(new List<string> { "VAR1" });
        var result2 = ValidationResult.Fail(
            ValidationError.MissingVariable("VAR2", "Desc")
        );

        var combined = ValidationResult.Combine(result1, result2);

        Assert.False(combined.Success);  // One failed
        Assert.Contains("VAR1", combined.ProvidedVariables);
        Assert.Contains("VAR2", combined.MissingVariables);
    }
}

public class ProcessingResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        var result = ProcessingResult<int>.Ok(42);

        Assert.True(result.Success);
        Assert.Equal(42, result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Map_TransformsValue()
    {
        var intResult = ProcessingResult<int>.Ok(42);
        var stringResult = intResult.Map(x => x.ToString());

        Assert.True(stringResult.Success);
        Assert.Equal("42", stringResult.Value);
    }

    [Fact]
    public void Map_PropagatesErrors()
    {
        var error = ValidationError.FileNotFound("test.md");
        var intResult = ProcessingResult<int>.Fail(error);
        var stringResult = intResult.Map(x => x.ToString());

        Assert.False(stringResult.Success);
        Assert.Single(stringResult.Errors);
    }

    [Fact]
    public void FromValidation_ConvertsValidationResult()
    {
        var validation = ValidationResult.Fail(
            ValidationError.MissingVariable("VAR", "Desc")
        );

        var result = ProcessingResult<string>.FromValidation(validation);

        Assert.False(result.Success);
        Assert.Equal(validation.Errors, result.Errors);
    }
}
```

---

## Summary

The Models namespace provides:

1. **MarkdownDocument**: Represents parsed markdown with variable definitions
2. **VariableDefinition**: Metadata about template variables with validation
3. **ValidationResult**: Structured validation results with error collection
4. **ProcessingResult**: Generic Result<T> pattern for error handling
5. **ValidationError**: Structured error information with types and context

### Key Design Decisions

- **Result Pattern**: No exceptions for expected failures
- **Error Collection**: Report all errors at once, not first failure
- **Immutability**: Properties are init-only where possible
- **Type Inference**: Automatic type detection from default values
- **Factory Methods**: Clear intent and validation enforcement
- **JSON Serialization**: All models support JSON output for CLI

### Integration Points

- **Core/MarkdownParser.cs**: Creates MarkdownDocument instances
- **Core/VariableExtractor.cs**: Uses VariableDefinition for validation
- **Core/VariableSubstitutor.cs**: Returns ProcessingResult<string>
- **Commands/**: All commands use ValidationResult and ProcessingResult
- **Utilities/JsonOutput.cs**: Serializes all models to JSON

This design ensures type safety, predictable error handling, and maintainable code throughout the MDTool project.
