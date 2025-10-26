# Utilities Namespace Design Document

**Project:** MDTool - Markdown processing with variable substitution
**Namespace:** `MDTool.Utilities`
**Version:** 1.0.0
**Last Updated:** 2025-10-25

---

## Table of Contents

1. [Overview](#overview)
2. [JsonOutput Class](#jsonoutput-class)
3. [FileHelper Class](#filehelper-class)
4. [Helper Methods Design](#helper-methods-design)
5. [Security Patterns](#security-patterns)
6. [Code Examples](#code-examples)
7. [Testing Considerations](#testing-considerations)

---

## Overview

The **Utilities** namespace provides cross-cutting concerns for MDTool, focusing on two primary areas:

1. **JSON Output Standardization** - Consistent JSON formatting for all command responses
2. **File Operations** - Secure file I/O with validation and error handling

### Purpose

The Utilities namespace ensures:
- **Consistency**: All commands return uniform JSON output format
- **Security**: File operations are protected against common vulnerabilities
- **Reliability**: Comprehensive error handling and validation
- **Maintainability**: Centralized logic for common operations

### Design Philosophy

- **Static Methods**: Utilities are stateless and thread-safe
- **Defensive Programming**: Validate all inputs before processing
- **Fail-Fast**: Detect and report errors immediately
- **Clear Error Messages**: Provide actionable feedback to users

---

## JsonOutput Class

The `JsonOutput` class provides standardized JSON serialization for all MDTool command responses, ensuring consistent output format across success and error scenarios.

### Class Definition

```csharp
namespace MDTool.Utilities;

public static class JsonOutput
{
    /// <summary>
    /// Creates a success JSON response with the result data.
    /// </summary>
    /// <param name="result">The successful result data to serialize</param>
    /// <returns>JSON string with success=true and result field</returns>
    public static string Success(object result);

    /// <summary>
    /// Creates a failure JSON response with error details.
    /// </summary>
    /// <param name="errors">List of validation or processing errors</param>
    /// <returns>JSON string with success=false and error details</returns>
    public static string Failure(List<ValidationError> errors);

    /// <summary>
    /// Creates a failure JSON response with error details and variable context.
    /// </summary>
    /// <param name="errors">List of validation or processing errors</param>
    /// <param name="provided">Variables that were provided</param>
    /// <param name="missing">Variables that are missing</param>
    /// <returns>JSON string with success=false, errors, provided, and missing arrays</returns>
    public static string Failure(List<ValidationError> errors, List<string> provided, List<string> missing);
}
```

### Output Format Specifications

#### Success Response

**Format:**
```json
{
  "success": true,
  "result": "...content or data..."
}
```

**Characteristics:**
- `success`: Always `true`
- `result`: Can be string, object, or complex type
- Pretty-printed with 2-space indentation
- UTF-8 encoding without BOM

**Example - Schema Output:**
```json
{
  "success": true,
  "result": {
    "name": "The application name",
    "branch": "Git branch to deploy",
    "port": 8080
  }
}
```

**Example - Validation Success:**
```json
{
  "success": true,
  "result": "All required variables provided"
}
```

#### Failure Response

**Format:**
```json
{
  "success": false,
  "errors": [
    {
      "type": "ErrorTypeName",
      "variable": "VARIABLE_NAME",
      "description": "Error description",
      "line": 15
    }
  ],
  "provided": ["VAR1", "VAR2"],
  "missing": ["VAR3", "VAR4"]
}
```

**Characteristics:**
- `success`: Always `false`
- `errors`: Array of error objects (never null, may be empty)
- `provided`: Optional array of variables that were provided
- `missing`: Optional array of variables that are missing
- Pretty-printed for readability

### Error Types and Formats

#### 1. MissingRequiredVariable

```json
{
  "success": false,
  "errors": [
    {
      "type": "MissingRequiredVariable",
      "variable": "APP_NAME",
      "description": "Application to deploy",
      "line": null
    },
    {
      "type": "MissingRequiredVariable",
      "variable": "USER.EMAIL",
      "description": "User's email address",
      "line": 42
    }
  ],
  "provided": ["BRANCH", "PORT"],
  "missing": ["APP_NAME", "USER.EMAIL"]
}
```

#### 2. InvalidYamlHeader

```json
{
  "success": false,
  "errors": [
    {
      "type": "InvalidYamlHeader",
      "variable": null,
      "description": "YAML frontmatter parsing failed: Invalid indentation at line 5",
      "line": 5
    }
  ]
}
```

#### 3. InvalidJsonArgs

```json
{
  "success": false,
  "errors": [
    {
      "type": "InvalidJsonArgs",
      "variable": null,
      "description": "Failed to parse args.json: Unexpected token at position 127",
      "line": null
    }
  ]
}
```

#### 4. FileNotFound

```json
{
  "success": false,
  "errors": [
    {
      "type": "FileNotFound",
      "variable": null,
      "description": "Input file not found: /path/to/template.md",
      "line": null
    }
  ]
}
```

#### 5. InvalidVariableFormat

```json
{
  "success": false,
  "errors": [
    {
      "type": "InvalidVariableFormat",
      "variable": "user-name",
      "description": "Variable name must be uppercase with underscores (e.g., USER_NAME)",
      "line": 23
    },
    {
      "type": "InvalidVariableFormat",
      "variable": "{{UNCLOSED",
      "description": "Malformed variable syntax: missing closing }}}",
      "line": 45
    }
  ]
}
```

#### 6. FileSizeExceeded

```json
{
  "success": false,
  "errors": [
    {
      "type": "FileSizeExceeded",
      "variable": null,
      "description": "File size (12.5 MB) exceeds maximum allowed size (10 MB): large-file.md",
      "line": null
    }
  ]
}
```

#### 7. PathTraversalAttempt

```json
{
  "success": false,
  "errors": [
    {
      "type": "PathTraversalAttempt",
      "variable": null,
      "description": "Path contains directory traversal: ../../../etc/passwd",
      "line": null
    }
  ]
}
```

### Pretty-Print Configuration

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
```

**Settings:**
- `WriteIndented`: 2-space indentation
- `PropertyNamingPolicy`: camelCase for JSON properties
- `DefaultIgnoreCondition`: Omit null values from output
- `Encoder`: Allow unicode characters (for international text)

### Serialization Error Handling

```csharp
public static string Success(object result)
{
    try
    {
        var response = new { success = true, result };
        return JsonSerializer.Serialize(response, JsonOptions);
    }
    catch (JsonException ex)
    {
        // Fallback to simple error message if serialization fails
        return $$"""
        {
          "success": false,
          "errors": [{
            "type": "SerializationError",
            "description": "Failed to serialize result: {{ex.Message}}"
          }]
        }
        """;
    }
}
```

**Error Recovery Strategy:**
1. Attempt to serialize the result with configured options
2. If serialization fails (e.g., circular reference, unsupported type):
   - Catch `JsonException`
   - Return a manually constructed error JSON
   - Include the original exception message
3. Never throw exceptions from JsonOutput methods

### Integration with Result Pattern

MDTool uses the Result pattern for error collection. JsonOutput integrates seamlessly:

```csharp
// In command handler
var parseResult = await MarkdownParser.Parse(filePath);

if (!parseResult.Success)
{
    Console.WriteLine(JsonOutput.Failure(parseResult.Errors));
    return 1; // Error exit code
}

// Process successfully
var schema = SchemaGenerator.Generate(parseResult.Value.Variables);
Console.WriteLine(JsonOutput.Success(schema));
return 0; // Success exit code
```

**Benefits:**
- Consistent error reporting across all commands
- Type-safe result handling
- No exception-based control flow for business logic
- Easy to test and mock

---

## FileHelper Class

The `FileHelper` class provides secure file I/O operations with comprehensive validation and error handling.

### Class Definition

```csharp
namespace MDTool.Utilities;

public static class FileHelper
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Reads a file with validation and UTF-8 decoding.
    /// </summary>
    public static Task<ProcessingResult<string>> ReadFileAsync(string path);

    /// <summary>
    /// Writes content to a file with overwrite protection.
    /// </summary>
    public static Task<ProcessingResult<Unit>> WriteFileAsync(string path, string content, bool force = false);

    /// <summary>
    /// Validates a file path for security issues.
    /// strictForMacros enforces extra traversal rules for Phase 2 expansions.
    /// </summary>
    public static ProcessingResult<string> ValidatePath(string path, bool strictForMacros = false);

    /// <summary>
    /// Checks if a file's size is within the allowed limit.
    /// </summary>
    public static ProcessingResult<Unit> CheckFileSize(string path);

    /// <summary>
    /// Resolves a relative path from the current working directory.
    /// </summary>
    public static string ResolvePathFromCwd(string path);
}
```

### Method: ReadFile

**Purpose:** Safely read file contents with validation and error handling.

**Implementation:**

```csharp
public static Result<string> ReadFile(string path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return Result<string>.Failure(new ValidationError
        {
            Type = ErrorType.InvalidPath,
            Description = "File path cannot be null or empty"
        });
    }

    // Validate path security
    var pathResult = ValidatePath(path);
    if (!pathResult.Success)
    {
        return Result<string>.Failure(pathResult.Errors);
    }

    var validatedPath = pathResult.Value;

    // Check file exists
    if (!File.Exists(validatedPath))
    {
        return Result<string>.Failure(new ValidationError
        {
            Type = ErrorType.FileNotFound,
            Description = $"File not found: {validatedPath}"
        });
    }

    // Check file size
    var sizeResult = CheckFileSize(validatedPath);
    if (!sizeResult.Success)
    {
        return Result<string>.Failure(sizeResult.Errors);
    }

    // Read file with timeout
    try
    {
        using var cts = new CancellationTokenSource(FileOperationTimeoutMs);
        var content = File.ReadAllText(validatedPath, new UTF8Encoding(false));
        return Result<string>.Success(content);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Result<string>.Failure(new ValidationError
        {
            Type = ErrorType.FileAccessDenied,
            Description = $"Access denied: {validatedPath}"
        });
    }
    catch (IOException ex)
    {
        return Result<string>.Failure(new ValidationError
        {
            Type = ErrorType.FileReadError,
            Description = $"Failed to read file: {ex.Message}"
        });
    }
}
```

**Validation Steps:**
1. Check path is not null/empty
2. Validate path security (no traversal)
3. Check file exists
4. Check file size within limit
5. Read with UTF-8 encoding (no BOM)
6. Handle I/O exceptions gracefully

### Method: WriteFile

**Purpose:** Write content to a file with overwrite protection and validation.

**Implementation:**

```csharp
public static async Task<ProcessingResult<Unit>> WriteFileAsync(string path, string content, bool force = false)
{
    if (string.IsNullOrWhiteSpace(path))
        return ProcessingResult<Unit>.Fail(ValidationError.InvalidPath("File path cannot be null or empty"));

    if (content is null)
        return ProcessingResult<Unit>.Fail(ValidationError.ProcessingError("Content cannot be null"));

    var pathResult = ValidatePath(path);
    if (!pathResult.Success)
        return ProcessingResult<Unit>.Fail(pathResult.Errors);

    var validatedPath = pathResult.Value!;

    if (File.Exists(validatedPath) && !force)
        return ProcessingResult<Unit>.Fail(ValidationError.ProcessingError($"File '{validatedPath}' already exists. Use --force to overwrite."));

    var directory = Path.GetDirectoryName(validatedPath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            return ProcessingResult<Unit>.Fail(ValidationError.ProcessingError($"Failed to create directory: {ex.Message}"));
        }
    }

    try
    {
        await File.WriteAllTextAsync(validatedPath, content, new UTF8Encoding(false));
        return ProcessingResult<Unit>.Ok(Unit.Value);
    }
    catch (UnauthorizedAccessException)
    {
        return ProcessingResult<Unit>.Fail(ValidationError.ProcessingError($"Access denied: {validatedPath}"));
    }
    catch (IOException ex)
    {
        return ProcessingResult<Unit>.Fail(ValidationError.ProcessingError($"Failed to write file: {ex.Message}"));
    }
}
```
}
```

**Features:**
- Overwrite protection (requires `--force` flag)
- Automatic directory creation
- UTF-8 encoding without BOM
- Timeout protection
- Comprehensive error handling

### Method: ValidatePath

**Purpose:** Prevent directory traversal attacks and validate path security.

**Implementation:**

```csharp
public static ProcessingResult<string> ValidatePath(string path, bool strictForMacros = false)
{
    if (string.IsNullOrWhiteSpace(path))
        return ProcessingResult<string>.Fail(ValidationError.InvalidPath("Path cannot be null or empty"));

    try
    {
        var absolutePath = Path.GetFullPath(path);

        if (strictForMacros)
        {
            if (path.Contains("..") || path.Contains("~"))
                return ProcessingResult<string>.Fail(ValidationError.PathTraversalAttempt($"Path traversal not allowed: {path}"));
        }

        var invalidChars = Path.GetInvalidPathChars();
        if (path.Any(invalidChars.Contains))
            return ProcessingResult<string>.Fail(ValidationError.InvalidPath($"Path contains invalid characters: {path}"));

        return ProcessingResult<string>.Ok(absolutePath);
    }
    catch (Exception ex)
    {
        return ProcessingResult<string>.Fail(ValidationError.InvalidPath(ex.Message));
    }
}
```

**Security Checks:**
1. Null/Empty check and absolute normalization
2. Traversal detection (only when strictForMacros=true)
3. Invalid character detection (OS-specific)
4. Exception handling for path APIs

**Rationale:**
- Normal CLI file I/O accepts absolute/relative paths with validation
- strictForMacros=true hardens only file-expansion contexts (Phase 2)
- Prevents traversal while avoiding unnecessary CWD constraints

### Method: CheckFileSize

**Purpose:** Enforce the 10MB file size limit to prevent resource exhaustion.

**Implementation:**

```csharp
public static Result<Unit> CheckFileSize(string path)
{
    if (!File.Exists(path))
    {
        return Result<Unit>.Failure(new ValidationError
        {
            Type = ErrorType.FileNotFound,
            Description = $"File not found: {path}"
        });
    }

    try
    {
        var fileInfo = new FileInfo(path);
        var fileSizeBytes = fileInfo.Length;

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
            var maxSizeMB = MaxFileSizeBytes / (1024.0 * 1024.0);

            return Result<Unit>.Failure(new ValidationError
            {
                Type = ErrorType.FileSizeExceeded,
                Description = $"File size ({fileSizeMB:F1} MB) exceeds maximum allowed size ({maxSizeMB:F1} MB): {path}"
            });
        }

        return Result<Unit>.Success(Unit.Value);
    }
    catch (UnauthorizedAccessException)
    {
        return Result<Unit>.Failure(new ValidationError
        {
            Type = ErrorType.FileAccessDenied,
            Description = $"Access denied: {path}"
        });
    }
    catch (IOException ex)
    {
        return Result<Unit>.Failure(new ValidationError
        {
            Type = ErrorType.FileReadError,
            Description = $"Failed to check file size: {ex.Message}"
        });
    }
}
```

**Features:**
- Check file size before reading
- Prevent memory exhaustion attacks
- User-friendly error message with sizes in MB
- Handle access and I/O exceptions

### Method: ResolvePathFromCwd

**Purpose:** Resolve relative paths from the current working directory (CWD).

**Implementation:**

```csharp
public static string ResolvePathFromCwd(string path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return Directory.GetCurrentDirectory();
    }

    // If already absolute, return as-is
    if (Path.IsPathRooted(path))
    {
        return Path.GetFullPath(path);
    }

    // Resolve relative to CWD
    var cwd = Directory.GetCurrentDirectory();
    return Path.GetFullPath(Path.Combine(cwd, path));
}
```

**Behavior:**
- Null/empty path returns CWD
- Absolute paths returned unchanged
- Relative paths resolved from CWD
- Always returns normalized absolute path

**Use Cases:**
- Command-line arguments (e.g., `mdtool process template.md`)
- Phase 2 file expansion (e.g., `file:templates/continue.md`)
- YAML/JSON path references

---

## Helper Methods Design

### Static vs Instance Methods

**Decision: Use Static Methods**

**Rationale:**
- Utilities are **stateless** - no instance data to maintain
- **Thread-safe** - no shared mutable state
- **Simple API** - no need to instantiate objects
- **Performance** - no allocation overhead
- **Testing** - easy to test without setup/teardown

**Example:**
```csharp
// Static - Clean and simple
var content = FileHelper.ReadFile("template.md");

// Instance - Unnecessary complexity
var helper = new FileHelper();
var content = helper.ReadFile("template.md");
```

### Dependency Injection Considerations

**Static Methods and DI:**

While static methods don't support traditional DI, this is acceptable for utilities:

**Pros:**
- No external dependencies (only .NET BCL)
- Pure functions (input → output)
- No need to mock file system in most tests
- Can use `System.IO.Abstractions` if needed for advanced scenarios

**Cons:**
- Cannot mock for unit tests (requires integration tests)
- Cannot swap implementations

**Mitigation:**
- Use integration tests with real file system
- Test with temporary directories
- Validate behavior in real conditions

**Alternative for Testing:**

If mocking is needed, use interfaces:

```csharp
public interface IFileHelper
{
    Result<string> ReadFile(string path);
    Result<Unit> WriteFile(string path, string content, bool force);
}

public class FileHelper : IFileHelper
{
    // Implementation...
}
```

**Recommendation for Phase 1:** Keep static methods for simplicity. Refactor to interfaces only if testing becomes difficult.

### Testability

**Testing Strategies:**

#### 1. Integration Tests (Recommended)

```csharp
[Fact]
public void ReadFile_ValidFile_ReturnsContent()
{
    // Arrange
    var tempFile = Path.GetTempFileName();
    var expectedContent = "Test content";
    File.WriteAllText(tempFile, expectedContent);

    try
    {
        // Act
        var result = FileHelper.ReadFile(tempFile);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedContent, result.Value);
    }
    finally
    {
        File.Delete(tempFile);
    }
}
```

#### 2. Path Validation Tests (Unit)

```csharp
[Theory]
[InlineData("../etc/passwd")]
[InlineData("../../secrets.txt")]
[InlineData("~/private/data")]
public void ValidatePath_TraversalAttempt_ReturnsError(string maliciousPath)
{
    // Act
    var result = FileHelper.ValidatePath(maliciousPath);

    // Assert
    Assert.False(result.Success);
    Assert.Contains("traversal", result.Errors[0].Description, StringComparison.OrdinalIgnoreCase);
}
```

#### 3. JsonOutput Tests (Unit)

```csharp
[Fact]
public void Success_ValidObject_ReturnsFormattedJson()
{
    // Arrange
    var result = new { name = "test", value = 42 };

    // Act
    var json = JsonOutput.Success(result);

    // Assert
    Assert.Contains("\"success\": true", json);
    Assert.Contains("\"name\": \"test\"", json);
    Assert.Contains("\"value\": 42", json);
}
```

**Test Coverage Goals:**
- **JsonOutput**: 100% (pure functions, easy to test)
- **FileHelper**: 80%+ (integration tests cover main paths)
- **Security Validation**: 100% (critical functionality)

---

## Security Patterns

### Path Validation Rules

**Rules Enforced:**

1. **No Directory Traversal**
   - Block `..` in paths
   - Block `~` (home directory) references
   - Validate against CWD containment

2. **CWD Containment**
   - All paths must be within current working directory
   - Use `Path.GetFullPath()` to resolve
   - Compare with `StartsWith(cwdPath)`

3. **Invalid Character Detection**
   - Check `Path.GetInvalidPathChars()`
   - Platform-specific validation (Windows vs Unix)

4. **Absolute Path Normalization**
   - Convert all paths to absolute
   - Prevent ambiguity from relative paths

**Example Attack Scenarios:**

```csharp
// Attack: Access system files
ValidatePath("/etc/passwd") → Error: Outside CWD

// Attack: Parent directory traversal
ValidatePath("../../secrets.txt") → Error: Contains ".."

// Attack: Home directory access
ValidatePath("~/.ssh/id_rsa") → Error: Contains "~"

// Valid: Within project
ValidatePath("templates/header.md") → Success: /project/templates/header.md
```

### File Size Limit Enforcement

**Purpose:** Prevent denial-of-service through resource exhaustion.

**Enforcement Points:**

1. **Before Reading**
   - Check size with `FileInfo.Length`
   - Reject if > 10MB
   - Fail fast before loading into memory

2. **Phase 2 File Expansion**
   - Check each expanded file
   - Track cumulative size across expansions
   - Enforce depth limit (10 levels)

**Limit Rationale:**
- **10MB**: Large enough for templates, small enough to prevent abuse
- Typical markdown files: < 100KB
- Protection against: Billion laughs attack, memory exhaustion

**Error Message:**
```json
{
  "type": "FileSizeExceeded",
  "description": "File size (12.5 MB) exceeds maximum allowed size (10 MB): large-file.md"
}
```

### Timeout Handling

**Purpose:** Prevent hanging operations on slow or network file systems.

**Implementation:**

```csharp
private const int FileOperationTimeoutMs = 30_000; // 30 seconds

// In ReadFile and WriteFile methods
using var cts = new CancellationTokenSource(FileOperationTimeoutMs);
```

**Scenarios Protected:**
- Network file systems (slow reads)
- Very large files (near 10MB limit)
- Locked files (waiting for access)
- System resource contention

**Behavior on Timeout:**
- Operation cancelled
- Exception caught and converted to error result
- User receives clear timeout message

**Note:** Synchronous file operations (`File.ReadAllText`, `File.WriteAllText`) don't natively support cancellation tokens. This is a known limitation. For Phase 1, rely on OS-level timeouts. For future enhancement, consider async I/O with cancellation.

### UTF-8 Encoding Without BOM

**Specification:**

```csharp
new UTF8Encoding(false) // false = no BOM
```

**Rationale:**
- **Compatibility**: BOM causes issues with some parsers
- **Unix Convention**: Files without BOM are standard
- **Git-Friendly**: No invisible characters in diffs
- **Markdown Standard**: Markdown processors expect UTF-8 without BOM

**What is BOM?**
- Byte Order Mark: `EF BB BF` at file start
- Used to indicate UTF-8 encoding
- Unnecessary for UTF-8 (byte order is fixed)
- Can break parsers expecting plain text

---

## Code Examples

### Example 1: Using JsonOutput in Commands

```csharp
public class GetSchemaCommand : Command
{
    public GetSchemaCommand() : base("get-schema", "Extract JSON schema from markdown")
    {
        var fileArg = new Argument<string>("file", "Markdown file path");
        var outputOption = new Option<string?>("--output", "Output file path (default: stdout)");

        AddArgument(fileArg);
        AddOption(outputOption);

        this.SetHandler(ExecuteAsync, fileArg, outputOption);
    }

    private async Task<int> ExecuteAsync(string filePath, string? outputPath)
    {
        // Parse markdown file
        var parseResult = await MarkdownParser.Parse(filePath);
        if (!parseResult.Success)
        {
            Console.WriteLine(JsonOutput.Failure(parseResult.Errors));
            return 1;
        }

        // Generate schema
        var schema = SchemaGenerator.Generate(parseResult.Value.Variables);

        // Output
        var json = JsonOutput.Success(schema);

        if (string.IsNullOrEmpty(outputPath))
        {
            Console.WriteLine(json);
        }
        else
        {
            var writeResult = FileHelper.WriteFile(outputPath, json, force: false);
            if (!writeResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(writeResult.Errors));
                return 1;
            }
        }

        return 0;
    }
}
```

### Example 2: Using FileHelper for Safe File Operations

```csharp
public class ProcessCommand : Command
{
    private async Task<int> ExecuteAsync(string filePath, string argsPath, string? outputPath, bool force)
    {
        // Read markdown file
        var contentResult = FileHelper.ReadFile(filePath);
        if (!contentResult.Success)
        {
            Console.WriteLine(JsonOutput.Failure(contentResult.Errors));
            return 1;
        }

        // Read arguments file
        var argsResult = FileHelper.ReadFile(argsPath);
        if (!argsResult.Success)
        {
            Console.WriteLine(JsonOutput.Failure(argsResult.Errors));
            return 1;
        }

        // Parse arguments JSON
        Dictionary<string, object> args;
        try
        {
            args = JsonSerializer.Deserialize<Dictionary<string, object>>(
                argsResult.Value,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            var error = new ValidationError
            {
                Type = ErrorType.InvalidJsonArgs,
                Description = $"Failed to parse args.json: {ex.Message}"
            };
            Console.WriteLine(JsonOutput.Failure(new List<ValidationError> { error }));
            return 1;
        }

        // Process document
        var document = MarkdownParser.Parse(contentResult.Value);
        var substituteResult = VariableSubstitutor.Substitute(document.Value, args);

        if (!substituteResult.Success)
        {
            Console.WriteLine(JsonOutput.Failure(
                substituteResult.Errors,
                GetProvidedVariables(args),
                GetMissingVariables(substituteResult.Errors)
            ));
            return 1;
        }

        // Write output
        if (string.IsNullOrEmpty(outputPath))
        {
            // Output to stdout
            Console.WriteLine(substituteResult.Value);
        }
        else
        {
            // Write to file with overwrite protection
            var writeResult = FileHelper.WriteFile(outputPath, substituteResult.Value, force);
            if (!writeResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(writeResult.Errors));
                return 1;
            }

            Console.WriteLine(JsonOutput.Success($"Output written to: {outputPath}"));
        }

        return 0;
    }
}
```

### Example 3: Path Validation in File Expansion (Phase 2)

```csharp
public class FileExpander
{
    private const int MaxRecursionDepth = 10;

    public Result<string> ExpandFileReference(string fileReference, int depth = 0)
    {
        // Check recursion depth
        if (depth >= MaxRecursionDepth)
        {
            return Result<string>.Failure(new ValidationError
            {
                Type = ErrorType.RecursionDepthExceeded,
                Description = $"Maximum recursion depth ({MaxRecursionDepth}) exceeded during file expansion"
            });
        }

        // Extract path from "file:path/to/file.md"
        var path = fileReference.Replace("file:", "").Trim();

        // Resolve from CWD
        var absolutePath = FileHelper.ResolvePathFromCwd(path);

        // Validate path security
        var validationResult = FileHelper.ValidatePath(absolutePath);
        if (!validationResult.Success)
        {
            return Result<string>.Failure(validationResult.Errors);
        }

        // Read file
        var readResult = FileHelper.ReadFile(validationResult.Value);
        if (!readResult.Success)
        {
            return Result<string>.Failure(readResult.Errors);
        }

        // Recursively expand any nested file references
        var content = readResult.Value;
        var fileReferenceRegex = new Regex(@"\{\{file:([^}]+)\}\}");

        foreach (Match match in fileReferenceRegex.Matches(content))
        {
            var nestedResult = ExpandFileReference(match.Value, depth + 1);
            if (!nestedResult.Success)
            {
                return nestedResult;
            }

            content = content.Replace(match.Value, nestedResult.Value);
        }

        return Result<string>.Success(content);
    }
}
```

### Example 4: Comprehensive Error Collection

```csharp
public class ValidationService
{
    public ValidationResult ValidateDocument(MarkdownDocument document, Dictionary<string, object> args)
    {
        var errors = new List<ValidationError>();
        var provided = new List<string>();
        var missing = new List<string>();

        foreach (var variable in document.Variables)
        {
            var variableName = variable.Key;
            var definition = variable.Value;

            // Check if variable is provided (case-insensitive)
            var isProvided = args.Keys.Any(k =>
                string.Equals(k, variableName, StringComparison.OrdinalIgnoreCase)
            );

            if (isProvided)
            {
                provided.Add(variableName);
            }
            else if (definition.Required && definition.DefaultValue == null)
            {
                // Required and no default
                missing.Add(variableName);
                errors.Add(new ValidationError
                {
                    Type = ErrorType.MissingRequiredVariable,
                    Variable = variableName,
                    Description = definition.Description
                });
            }
        }

        if (errors.Any())
        {
            return new ValidationResult
            {
                Success = false,
                Errors = errors,
                ProvidedVariables = provided,
                MissingVariables = missing
            };
        }

        return new ValidationResult
        {
            Success = true,
            ProvidedVariables = provided
        };
    }
}
```

### Example 5: Testing FileHelper

```csharp
public class FileHelperTests
{
    [Fact]
    public void ReadFile_ValidFile_ReturnsContent()
    {
        // Arrange
        var testContent = "# Test Markdown\n\n{{NAME}}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, testContent, new UTF8Encoding(false));

        try
        {
            // Act
            var result = FileHelper.ReadFile(tempFile);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(testContent, result.Value);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteFile_ExistingFile_WithoutForce_ReturnsError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "original");

        try
        {
            // Act
            var result = FileHelper.WriteFile(tempFile, "new content", force: false);

            // Assert
            Assert.False(result.Success);
Assert.Equal(ErrorType.FileExists, result.Errors[0].Type);
            Assert.Contains("--force", result.Errors[0].Description);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteFile_ExistingFile_WithForce_Overwrites()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "original");
        var newContent = "new content";

        try
        {
            // Act
            var result = FileHelper.WriteFile(tempFile, newContent, force: true);

            // Assert
            Assert.True(result.Success);
            var actualContent = File.ReadAllText(tempFile);
            Assert.Equal(newContent, actualContent);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("../../secrets.txt")]
    [InlineData("~/private/key")]
    public void ValidatePath_DirectoryTraversal_ReturnsError(string maliciousPath)
    {
        // Act
        var result = FileHelper.ValidatePath(maliciousPath);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ErrorType.PathTraversalAttempt, result.Errors[0].Type);
    }

    [Fact]
    public void CheckFileSize_FileTooLarge_ReturnsError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var largeContent = new string('x', 11 * 1024 * 1024); // 11 MB
        File.WriteAllText(tempFile, largeContent);

        try
        {
            // Act
            var result = FileHelper.CheckFileSize(tempFile);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(ErrorType.FileSizeExceeded, result.Errors[0].Type);
            Assert.Contains("10", result.Errors[0].Description); // Max size mentioned
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ResolvePathFromCwd_RelativePath_ReturnsAbsolute()
    {
        // Arrange
        var relativePath = "templates/header.md";
        var cwd = Directory.GetCurrentDirectory();
        var expected = Path.GetFullPath(Path.Combine(cwd, relativePath));

        // Act
        var actual = FileHelper.ResolvePathFromCwd(relativePath);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolvePathFromCwd_AbsolutePath_ReturnsUnchanged()
    {
        // Arrange
        var absolutePath = Path.GetTempPath();

        // Act
        var actual = FileHelper.ResolvePathFromCwd(absolutePath);

        // Assert
        Assert.Equal(Path.GetFullPath(absolutePath), actual);
    }
}
```

---

## Testing Considerations

### Unit Test Strategy

**JsonOutput:**
- ✅ Test success responses with various data types
- ✅ Test failure responses with single error
- ✅ Test failure responses with multiple errors
- ✅ Test provided/missing arrays included correctly
- ✅ Test JSON format (pretty-print, camelCase)
- ✅ Test serialization error handling (circular references)

**FileHelper:**
- ✅ Path validation (valid, traversal attempts, invalid chars)
- ✅ File size checks (within limit, exceeds limit)
- ✅ Path resolution (relative, absolute, CWD)
- ⚠️ File I/O requires integration tests (see below)

### Integration Test Strategy

**FileHelper:**
- ✅ Read valid file
- ✅ Read non-existent file
- ✅ Read file with UTF-8 content
- ✅ Write new file
- ✅ Write with overwrite protection (without force)
- ✅ Overwrite file (with force)
- ✅ Create directory structure automatically
- ✅ Handle file access permissions

**Test Data Setup:**
```csharp
public class FileHelperIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public FileHelperIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mdtool-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void ReadFile_WithUtf8Content_ReturnsCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.md");
        var content = "# Test 测试 テスト\n\n{{NAME}}";
        File.WriteAllText(filePath, content, new UTF8Encoding(false));

        // Act
        var result = FileHelper.ReadFile(filePath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(content, result.Value);
    }
}
```

### Edge Cases to Test

1. **Empty files**
2. **Files with only whitespace**
3. **Files with BOM (should still read correctly)**
4. **Very long file paths (OS limits)**
5. **Files with special characters in name**
6. **Symbolic links (should they be followed?)**
7. **Network paths (if supported)**
8. **Read-only files (write should fail gracefully)**
9. **Concurrent access (multiple readers/writers)**
10. **Unicode in file paths**

### Test Coverage Goals

| Component | Target Coverage | Rationale |
|-----------|----------------|-----------|
| JsonOutput | 100% | Pure functions, critical for all commands |
| FileHelper.ValidatePath | 100% | Security-critical |
| FileHelper.CheckFileSize | 100% | Security-critical |
| FileHelper.ResolvePathFromCwd | 100% | Used in all file operations |
| FileHelper.ReadFile | 90% | Integration tests cover most paths |
| FileHelper.WriteFile | 90% | Integration tests cover most paths |

---

## Summary

The Utilities namespace provides essential cross-cutting functionality for MDTool:

**JsonOutput:**
- Standardized JSON output for all commands
- Consistent success/failure format
- Comprehensive error reporting
- Integration with Result pattern

**FileHelper:**
- Secure file I/O with validation
- Path traversal prevention
- File size limits
- Overwrite protection
- UTF-8 encoding without BOM

**Design Principles:**
- Static methods for simplicity
- Result pattern for error collection
- Fail-fast with clear error messages
- Security-first approach
- Testable and maintainable

**Next Steps:**
1. Implement `JsonOutput` class with serialization options
2. Implement `FileHelper` class with all security checks
3. Add comprehensive unit and integration tests
4. Integrate with command classes
5. Document security considerations in README

---

**References:**
- Master Checklist: `/Users/randlee/Documents/github/mdtool/docs/master-checklist.md`
- Implementation Plan: `/Users/randlee/Documents/github/mdtool/docs/mdtool-implementation-plan.md`
- Result Pattern: `MDTool.Models.ProcessingResult<T>`
- Error Types: `MDTool.Models.ValidationError`

**Last Updated:** 2025-10-25
**Status:** Design Complete - Ready for Implementation
