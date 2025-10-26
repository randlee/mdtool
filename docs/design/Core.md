# Core Namespace Design Document

**Project:** MDTool - Markdown Processing Tool
**Namespace:** MDTool.Core
**Version:** 1.0.0
**Last Updated:** 2025-10-25

---

## Table of Contents

1. [Overview](#overview)
2. [MarkdownParser Class](#markdownparser-class)
3. [VariableExtractor Class](#variableextractor-class)
4. [SchemaGenerator Class](#schemagenerator-class)
5. [VariableSubstitutor Class](#variablesubstitutor-class)
6. [Processing Pipeline](#processing-pipeline)
7. [Error Handling Strategy](#error-handling-strategy)
8. [Code Examples](#code-examples)

---

## Overview

The **Core** namespace contains the fundamental parsing, extraction, generation, and substitution logic that powers MDTool. These classes work together to transform markdown templates with YAML frontmatter into processed documents with substituted variables.

### Purpose

The Core namespace provides:

1. **Parsing** - Extract and parse YAML frontmatter from markdown files
2. **Extraction** - Find all variable references (`{{VARIABLE_NAME}}`) in content
3. **Generation** - Create JSON schemas from variable definitions
4. **Substitution** - Replace variable placeholders with actual values from JSON arguments

### Design Principles

- **Separation of Concerns** - Each class has a single, well-defined responsibility
- **Result Pattern** - All operations return `Result<T>` with success/error information
- **Error Collection** - Collect all errors instead of failing on the first one
- **Extensibility** - Designed for Phase 2 enhancements (macros, file expansion, env vars)
- **Testability** - Pure functions with minimal side effects

### Dependencies

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.Json;
using System.Text.RegularExpressions;
```

---

## MarkdownParser Class

### Responsibility

Parse markdown files to extract YAML frontmatter and content, converting variable definitions into structured `VariableDefinition` objects.

### Public Interface

```csharp
public class MarkdownParser
{
    public Result<MarkdownDocument> Parse(string filePath);
}
```

### Method: Parse(string filePath)

**Purpose:** Read and parse a markdown file, extracting YAML frontmatter and content.

**Returns:** `Result<MarkdownDocument>` containing parsed data or errors.

#### Algorithm

```
1. Read file contents
2. Detect YAML frontmatter boundaries
   - Look for opening "---" at start of file
   - Look for closing "---" after YAML content
3. Split into frontmatter and content sections
4. Parse YAML frontmatter using YamlDotNet
5. Convert YAML variables to VariableDefinition objects
6. Validate variable definitions
7. Return Result with MarkdownDocument or errors
```

#### Detailed Implementation

```csharp
public class MarkdownParser
{
    private readonly IDeserializer _yamlDeserializer;

    public MarkdownParser()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public Result<MarkdownDocument> Parse(string filePath)
    {
        var errors = new List<ValidationError>();

        // Step 1: Read file
        string content;
        try
        {
            content = File.ReadAllText(filePath);
        }
        catch (FileNotFoundException)
        {
            errors.Add(new ValidationError
            {
                Type = ErrorType.FileNotFound,
                Description = $"File not found: {filePath}"
            });
            return Result<MarkdownDocument>.Failure(errors);
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError
            {
                Type = ErrorType.FileReadError,
                Description = $"Error reading file: {ex.Message}"
            });
            return Result<MarkdownDocument>.Failure(errors);
        }

        // Step 2: Detect frontmatter boundaries
        var (hasYaml, yamlContent, markdownContent) = ExtractFrontmatter(content);

        // Step 3: Handle missing frontmatter
        if (!hasYaml)
        {
            return Result<MarkdownDocument>.Success(new MarkdownDocument
            {
                Variables = new Dictionary<string, VariableDefinition>(),
                Content = content,
                RawYaml = string.Empty
            });
        }

        // Step 4: Parse YAML
        Dictionary<string, object> yamlData;
        try
        {
            var yamlObject = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            // Look for "variables" section
            if (!yamlObject.ContainsKey("variables"))
            {
                errors.Add(new ValidationError
                {
                    Type = ErrorType.InvalidYamlHeader,
                    Description = "YAML frontmatter missing 'variables:' section"
                });
                return Result<MarkdownDocument>.Failure(errors);
            }

            yamlData = yamlObject["variables"] as Dictionary<string, object>;
            if (yamlData == null)
            {
                errors.Add(new ValidationError
                {
                    Type = ErrorType.InvalidYamlHeader,
                    Description = "'variables' must be a dictionary"
                });
                return Result<MarkdownDocument>.Failure(errors);
            }
        }
        catch (YamlException ex)
        {
            errors.Add(new ValidationError
            {
                Type = ErrorType.InvalidYamlHeader,
                Description = $"YAML parsing error: {ex.Message}"
            });
            return Result<MarkdownDocument>.Failure(errors);
        }

        // Step 5: Convert to VariableDefinitions
        var variables = new Dictionary<string, VariableDefinition>();
        foreach (var kvp in yamlData)
        {
            var varName = kvp.Key;

            // Validate variable name format
            if (!IsValidVariableName(varName))
            {
                errors.Add(new ValidationError
                {
                    Type = ErrorType.InvalidVariableFormat,
                    Variable = varName,
                    Description = $"Variable name '{varName}' must be uppercase with underscores"
                });
                continue;
            }

            var varDef = ParseVariableDefinition(varName, kvp.Value);
            if (varDef.Error != null)
            {
                errors.Add(varDef.Error);
                continue;
            }

            variables[varName] = varDef.Definition;
        }

        // Step 6: Validate optional variables have defaults
        foreach (var varDef in variables.Values)
        {
            if (!varDef.Required && varDef.DefaultValue == null)
            {
                errors.Add(new ValidationError
                {
                    Type = ErrorType.InvalidYamlHeader,
                    Variable = varDef.Name,
                    Description = $"Optional variable '{varDef.Name}' must have a default value"
                });
            }
        }

        // Step 7: Return result
        if (errors.Any())
        {
            return Result<MarkdownDocument>.Failure(errors);
        }

        return Result<MarkdownDocument>.Success(new MarkdownDocument
        {
            Variables = variables,
            Content = markdownContent,
            RawYaml = yamlContent
        });
    }

    private (bool hasYaml, string yaml, string content) ExtractFrontmatter(string content)
    {
        // Check if file starts with ---
        if (!content.TrimStart().StartsWith("---"))
        {
            return (false, string.Empty, content);
        }

        var lines = content.Split('\n');
        int startIndex = -1;
        int endIndex = -1;

        // Find first --- (skip empty lines)
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
        var yamlLines = lines.Skip(startIndex + 1).Take(endIndex - startIndex - 1);
        var contentLines = lines.Skip(endIndex + 1);

        var yaml = string.Join('\n', yamlLines);
        var markdown = string.Join('\n', contentLines);

        return (true, yaml, markdown);
    }

    private bool IsValidVariableName(string name)
    {
        // Must start with letter, contain only A-Z, 0-9, underscore
        // Can have dots for nesting (handled in extraction)
        var regex = new Regex(@"^[A-Z][A-Z0-9_]*$");
        return regex.IsMatch(name);
    }

    private (VariableDefinition Definition, ValidationError Error) ParseVariableDefinition(
        string name,
        object value)
    {
        // Simple string format: NAME: "description"
        if (value is string stringValue)
        {
            return (new VariableDefinition
            {
                Name = name,
                Description = stringValue,
                Required = true,
                DefaultValue = null
            }, null);
        }

        // Object format: NAME: { description, required, default }
        if (value is Dictionary<object, object> objValue)
        {
            var dict = objValue.ToDictionary(
                k => k.Key.ToString(),
                v => v.Value);

            if (!dict.ContainsKey("description"))
            {
                return (null, new ValidationError
                {
                    Type = ErrorType.InvalidYamlHeader,
                    Variable = name,
                    Description = $"Variable '{name}' object must have 'description' field"
                });
            }

            var description = dict["description"].ToString();
            var required = dict.ContainsKey("required")
                ? Convert.ToBoolean(dict["required"])
                : true;
            var defaultValue = dict.ContainsKey("default")
                ? dict["default"]
                : null;

            return (new VariableDefinition
            {
                Name = name,
                Description = description,
                Required = required,
                DefaultValue = defaultValue
            }, null);
        }

        return (null, new ValidationError
        {
            Type = ErrorType.InvalidYamlHeader,
            Variable = name,
            Description = $"Variable '{name}' must be a string or object"
        });
    }
}
```

### Supported YAML Formats

#### Format 1: Simple String (Required Variable)

```yaml
---
variables:
  NAME: "The application name"
  EMAIL: "User's email address"
---
```

This format:
- Assumes variable is **required** (cannot be omitted)
- String value becomes the **description**
- No default value

#### Format 2: Object with Properties

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

This format allows:
- `description` (required field)
- `required` (boolean, defaults to `true`)
- `default` (any value, required if `required: false`)

### Validation Rules

1. **Variable names** must match `[A-Z][A-Z0-9_]*` pattern
2. **Optional variables** (`required: false`) must have a `default` value
3. **Object format** must include `description` field
4. **Frontmatter** must have `variables:` section
5. **YAML** must be valid and parseable

### Edge Cases

| Case | Behavior |
|------|----------|
| No frontmatter | Returns empty variables dictionary, full content |
| Unclosed frontmatter | Treats entire file as content (no YAML) |
| Malformed YAML | Collects error, returns failure result |
| Invalid variable name | Collects error, continues parsing other variables |
| Optional without default | Collects error for that variable |
| Empty frontmatter | Returns empty variables dictionary |
| Multiple `---` markers | Uses first pair, rest treated as content |

---

## VariableExtractor Class

### Responsibility

Find all variable references in markdown content using pattern matching, supporting both simple and nested (dot notation) variables.

### Public Interface

```csharp
public class VariableExtractor
{
    public List<ExtractedVariable> Extract(string content);
}
```

### Method: Extract(string content)

**Purpose:** Find all `{{VARIABLE_NAME}}` patterns in content, tracking locations.

**Returns:** List of unique variables found with line numbers.

#### Algorithm

```
1. Compile regex pattern for variable matching
2. Find all matches in content
3. For each match:
   - Extract variable name (without braces)
   - Calculate line number
   - Store variable and location
4. Deduplicate variables (keep first occurrence)
5. Return sorted list of unique variables
```

#### Detailed Implementation

```csharp
public class VariableExtractor
{
    // Regex pattern: matches {{VARIABLE_NAME}} and {{USER.NAME.FIELD}}
    // - Starts with {{
    // - Captures uppercase letter followed by letters/numbers/underscores
    // - Supports dot notation for nesting (up to 5 levels)
    // - Ends with }}
    private static readonly Regex VariablePattern = new Regex(
        @"\{\{([A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*)\}\}",
        RegexOptions.Compiled | RegexOptions.Multiline
    );

    private const int MaxNestingDepth = 5;

    public List<ExtractedVariable> Extract(string content)
    {
        var variables = new Dictionary<string, ExtractedVariable>();
        var matches = VariablePattern.Matches(content);

        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value;

            // Validate nesting depth
            var nestingLevel = variableName.Count(c => c == '.') + 1;
            if (nestingLevel > MaxNestingDepth)
            {
                // Could collect as error, but for extraction we skip
                continue;
            }

            // Calculate line number
            var lineNumber = CalculateLineNumber(content, match.Index);

            // Store first occurrence only (for error reporting)
            if (!variables.ContainsKey(variableName))
            {
                variables[variableName] = new ExtractedVariable
                {
                    Name = variableName,
                    Line = lineNumber,
                    Position = match.Index,
                    Length = match.Length
                };
            }
        }

        // Return sorted list (deterministic order)
        return variables.Values
            .OrderBy(v => v.Name)
            .ToList();
    }

    private int CalculateLineNumber(string content, int position)
    {
        if (position < 0 || position >= content.Length)
        {
            return 0;
        }

        var lineNumber = 1;
        for (int i = 0; i < position; i++)
        {
            if (content[i] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    public List<string> ExtractVariableNames(string content)
    {
        // Convenience method that returns just names
        return Extract(content).Select(v => v.Name).ToList();
    }
}

public class ExtractedVariable
{
    public string Name { get; set; }
    public int Line { get; set; }
    public int Position { get; set; }
    public int Length { get; set; }
}
```

### Regex Pattern Breakdown

```regex
\{\{([A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*)\}\}
```

Breaking it down:
- `\{\{` - Literal opening braces (escaped)
- `(...)` - Capture group for variable name
- `[A-Z]` - Must start with uppercase letter
- `[A-Z0-9_]*` - Followed by zero or more uppercase letters, digits, or underscores
- `(?:\.[A-Z][A-Z0-9_]*)*` - Non-capturing group for dot notation
  - `\.` - Literal dot (escaped)
  - `[A-Z][A-Z0-9_]*` - Another segment (same rules)
  - `*` - Zero or more repetitions (allows any nesting depth)
- `\}\}` - Literal closing braces (escaped)

### Supported Variable Formats

| Format | Valid | Example |
|--------|-------|---------|
| Simple | Yes | `{{NAME}}` |
| Nested (1 level) | Yes | `{{USER.NAME}}` |
| Nested (multi-level) | Yes | `{{USER.PROFILE.ADDRESS.CITY}}` |
| Deep nesting (>5 levels) | No | `{{A.B.C.D.E.F}}` (skipped) |
| Lowercase | No | `{{name}}` (not matched) |
| Mixed case | No | `{{userName}}` (not matched) |
| With spaces | No | `{{ NAME }}` (not matched) |
| Special chars | No | `{{USER-NAME}}` (not matched) |

### Nesting Examples

```markdown
{{USER.NAME}}           → ["USER", "NAME"]
{{USER.PROFILE.EMAIL}}  → ["USER", "PROFILE", "EMAIL"]
{{CONFIG.DB.HOST}}      → ["CONFIG", "DB", "HOST"]
```

### Edge Cases

| Case | Behavior |
|------|----------|
| Unclosed braces `{{NAME` | Not matched (requires closing `}}`) |
| Partial braces `{NAME}` | Not matched (requires double braces) |
| Empty braces `{{}}` | Not matched (requires letter after `{{`) |
| Multiple variables on one line | All matched and extracted |
| Variable at start/end of file | Matched correctly |
| Unicode characters `{{NAÏVE}}` | Not matched (only ASCII allowed) |
| Nested braces `{{{{NAME}}}}` | Outer braces matched as literal text |
| Line spanning | Works (regex is multiline-aware) |

---

## SchemaGenerator Class

### Responsibility

Convert variable definitions into a JSON schema structure that can be used as a template for providing variable values.

### Public Interface

```csharp
public class SchemaGenerator
{
    public string GenerateSchema(Dictionary<string, VariableDefinition> variables);
}
```

### Method: GenerateSchema(Dictionary<string, VariableDefinition>)

**Purpose:** Create a JSON schema object with descriptions as placeholder values.

**Returns:** Pretty-printed JSON string representing the schema.

#### Algorithm

```
1. Create root JSON object
2. For each variable definition:
   - Split variable name by dots (nested paths)
   - Navigate/create nested structure
   - For required variables: use description as value
   - For optional variables: use default value
3. Serialize to JSON with indentation
4. Return formatted string
```

#### Detailed Implementation

```csharp
public class SchemaGenerator
{
    public string GenerateSchema(Dictionary<string, VariableDefinition> variables)
    {
        var schema = new Dictionary<string, object>();

        foreach (var variable in variables.Values.OrderBy(v => v.Name))
        {
            var path = variable.Name.Split('.');
            var current = schema;

            // Navigate/create nested structure
            for (int i = 0; i < path.Length - 1; i++)
            {
                var segment = path[i].ToLowerInvariant();

                if (!current.ContainsKey(segment))
                {
                    current[segment] = new Dictionary<string, object>();
                }

                current = current[segment] as Dictionary<string, object>;
            }

            // Set final value
            var finalKey = path[^1].ToLowerInvariant();
            var value = GetSchemaValue(variable);
            current[finalKey] = value;
        }

        // Serialize to JSON
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(schema, options);
    }

    private object GetSchemaValue(VariableDefinition variable)
    {
        // For optional variables, use the default value
        if (!variable.Required && variable.DefaultValue != null)
        {
            return variable.DefaultValue;
        }

        // For required variables, use description as placeholder
        return variable.Description;
    }
}
```

### Nested Object Generation

The schema generator converts dot notation into nested JSON objects:

#### Input Variables

```csharp
{
    "USER.NAME": { Description: "User's full name", Required: true },
    "USER.EMAIL": { Description: "User's email", Required: true },
    "USER.PROFILE.BIO": { Description: "User bio", Required: false, Default: "" }
}
```

#### Output JSON Schema

```json
{
  "user": {
    "name": "User's full name",
    "email": "User's email",
    "profile": {
      "bio": ""
    }
  }
}
```

### Case Conversion

Variable names in markdown are **UPPERCASE**, but JSON keys are converted to **lowercase** for conventional JSON formatting:

- `{{NAME}}` → `"name": "..."`
- `{{USER.EMAIL}}` → `"user": { "email": "..." }`

This allows natural JSON syntax while maintaining the uppercase convention in markdown.

### Value Assignment Rules

| Variable Type | Schema Value |
|--------------|-------------|
| Required variable | Description string (placeholder) |
| Optional with default | Default value (actual value) |
| Optional with null default | `null` |
| Optional with numeric default | Numeric value (preserves type) |
| Optional with boolean default | Boolean value (preserves type) |

### Examples

#### Example 1: Simple Variables

**Input:**
```csharp
variables = {
    "NAME": { Description: "Application name", Required: true },
    "PORT": { Description: "Server port", Required: true }
}
```

**Output:**
```json
{
  "name": "Application name",
  "port": "Server port"
}
```

#### Example 2: Mixed Required/Optional

**Input:**
```csharp
variables = {
    "APP_NAME": { Description: "App name", Required: true },
    "ENVIRONMENT": { Description: "Deploy env", Required: false, Default: "staging" },
    "DEBUG": { Description: "Debug mode", Required: false, Default: false }
}
```

**Output:**
```json
{
  "app_name": "App name",
  "environment": "staging",
  "debug": false
}
```

#### Example 3: Nested Structure

**Input:**
```csharp
variables = {
    "DATABASE.HOST": { Description: "DB host", Required: true },
    "DATABASE.PORT": { Description: "DB port", Required: false, Default: 5432 },
    "DATABASE.NAME": { Description: "DB name", Required: true },
    "CACHE.ENABLED": { Description: "Enable cache", Required: false, Default: true }
}
```

**Output:**
```json
{
  "database": {
    "host": "DB host",
    "port": 5432,
    "name": "DB name"
  },
  "cache": {
    "enabled": true
  }
}
```

---

## VariableSubstitutor Class

### Responsibility

Replace variable placeholders in content with actual values from JSON arguments, handling optional variables, nested objects, and comprehensive error collection.

### Public Interface

```csharp
public class VariableSubstitutor
{
    public Result<string> Substitute(
        string content,
        Dictionary<string, VariableDefinition> variables,
        Dictionary<string, object> args
    );
}
```

### Method: Substitute(content, variables, args)

**Purpose:** Replace all `{{VARIABLE}}` references with values from args.

**Returns:** `Result<string>` with substituted content or errors.

#### Algorithm

```
1. Extract all variables from content
2. Build complete args dictionary (merging defaults)
3. Validate all required variables present
4. For each variable in content:
   - Resolve value from args (case-insensitive)
   - Handle dot notation (nested objects)
   - Replace placeholder with value
5. Collect any errors (missing variables)
6. Return result with substituted content or errors
```

#### Detailed Implementation

```csharp
public class VariableSubstitutor
{
    private readonly VariableExtractor _extractor;

    public VariableSubstitutor()
    {
        _extractor = new VariableExtractor();
    }

    public Result<string> Substitute(
        string content,
        Dictionary<string, VariableDefinition> variables,
        Dictionary<string, object> args)
    {
        var errors = new List<ValidationError>();

        // Step 1: Extract all variables from content
        var extractedVars = _extractor.Extract(content);

        // Step 2: Build complete args with defaults
        var completeArgs = BuildCompleteArgs(variables, args);

        // Step 3: Validate required variables
        var missingVars = ValidateRequiredVariables(variables, completeArgs);
        if (missingVars.Any())
        {
            foreach (var varName in missingVars)
            {
                var varDef = variables[varName];
                errors.Add(new ValidationError
                {
                    Type = ErrorType.MissingRequiredVariable,
                    Variable = varName,
                    Description = varDef.Description,
                    Line = extractedVars.FirstOrDefault(v => v.Name == varName)?.Line ?? 0
                });
            }

            return Result<string>.Failure(errors);
        }

        // Step 4: Perform substitution
        var result = content;
        var sortedVars = extractedVars.OrderByDescending(v => v.Position);

        foreach (var variable in sortedVars)
        {
            var value = ResolveVariable(variable.Name, completeArgs);

            if (value == null)
            {
                // Should not happen after validation, but be safe
                errors.Add(new ValidationError
                {
                    Type = ErrorType.MissingRequiredVariable,
                    Variable = variable.Name,
                    Line = variable.Line,
                    Description = $"Could not resolve variable: {variable.Name}"
                });
                continue;
            }

            // Replace from end to start (preserves positions)
            var placeholder = $"{{{{{variable.Name}}}}}";
            var startPos = variable.Position;
            result = result.Remove(startPos, placeholder.Length)
                          .Insert(startPos, value.ToString());
        }

        if (errors.Any())
        {
            return Result<string>.Failure(errors);
        }

        return Result<string>.Success(result);
    }

    private Dictionary<string, object> BuildCompleteArgs(
        Dictionary<string, VariableDefinition> variables,
        Dictionary<string, object> providedArgs)
    {
        var complete = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Add provided args (case-insensitive)
        foreach (var kvp in providedArgs)
        {
            complete[kvp.Key] = kvp.Value;
        }

        // Add defaults for optional variables not provided
        foreach (var varDef in variables.Values)
        {
            if (!varDef.Required && varDef.DefaultValue != null)
            {
                var varName = varDef.Name;
                if (!complete.ContainsKey(varName))
                {
                    complete[varName] = varDef.DefaultValue;
                }
            }
        }

        return complete;
    }

    private List<string> ValidateRequiredVariables(
        Dictionary<string, VariableDefinition> variables,
        Dictionary<string, object> args)
    {
        var missing = new List<string>();

        foreach (var varDef in variables.Values)
        {
            if (varDef.Required)
            {
                var resolved = ResolveVariable(varDef.Name, args);
                if (resolved == null)
                {
                    missing.Add(varDef.Name);
                }
            }
        }

        return missing;
    }

    private object ResolveVariable(string variableName, Dictionary<string, object> args)
    {
        // Handle simple variables (no dots)
        if (!variableName.Contains('.'))
        {
            return args.ContainsKey(variableName) ? args[variableName] : null;
        }

        // Handle nested variables (dot notation)
        var segments = variableName.Split('.');
        object current = args;

        foreach (var segment in segments)
        {
            if (current is Dictionary<string, object> dict)
            {
                if (!dict.ContainsKey(segment))
                {
                    return null;
                }
                current = dict[segment];
            }
            else if (current is JsonElement element)
            {
                // Handle System.Text.Json deserialized objects
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (!element.TryGetProperty(segment, out var prop))
                    {
                        // Try case-insensitive
                        var found = false;
                        foreach (var property in element.EnumerateObject())
                        {
                            if (string.Equals(property.Name, segment, StringComparison.OrdinalIgnoreCase))
                            {
                                current = property.Value;
                                found = true;
                                break;
                            }
                        }
                        if (!found) return null;
                    }
                    else
                    {
                        current = prop;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return current;
    }
}
```

### Case-Insensitive Matching

JSON property names are matched case-insensitively, allowing flexible input formats:

**Markdown:**
```markdown
{{USER.NAME}} lives in {{USER.CITY}}
```

**Valid JSON formats (all equivalent):**
```json
// Lowercase (recommended)
{ "user": { "name": "John", "city": "NYC" } }

// Uppercase
{ "USER": { "NAME": "John", "CITY": "NYC" } }

// Mixed case
{ "User": { "Name": "John", "City": "NYC" } }

// Snake case
{ "user_name": "John", "user_city": "NYC" }
```

### Dot Notation Navigation

The substitutor navigates nested JSON structures using dot notation:

**Variable:** `{{CONFIG.DATABASE.HOST}}`

**JSON traversal:**
```
args["CONFIG"] → object
  ↓
["DATABASE"] → object
  ↓
["HOST"] → "localhost"
```

### Optional Variable Handling

Optional variables use defaults when not provided:

**Variables:**
```yaml
ENVIRONMENT:
  description: "Deploy environment"
  required: false
  default: "staging"
```

**JSON args:** `{}` (empty)

**Result:** `{{ENVIRONMENT}}` → `"staging"`

### Substitution Order and Precedence

1. **Required variables** - Must be in args (no fallback)
2. **Optional with args value** - Use provided value
3. **Optional without args value** - Use default from YAML
4. **Missing required** - Collect error, fail substitution

### Phase 2 Preparation: Extension Points

The substitutor is designed for Phase 2 enhancements:

```csharp
public class VariableSubstitutor
{
    // Phase 2: Macro expansion
    private string ExpandMacros(string content)
    {
        // {{DATE}}, {{TIME}}, {{DATETIME}}
        // Expand BEFORE variable substitution
        return content;
    }

    // Phase 2: File expansion
    private string ExpandFileReferences(string content, Dictionary<string, object> args)
    {
        // Handle {{file:PATH}}
        // Expand files and process nested variables
        return content;
    }

    // Phase 2: Environment variables
    private object ResolveEnvironmentVariable(string varName)
    {
        // Precedence: env → JSON args → YAML defaults → error
        // 1. Check Environment.GetEnvironmentVariable(varName) [case-sensitive]
        // 2. Fall back to args (case-insensitive)
        // 3. Fall back to YAML defaults
        // 4. Return null (error)
        return null;
    }

    // Phase 2: Full substitution pipeline
    public Result<string> SubstituteWithExtensions(
        string content,
        Dictionary<string, VariableDefinition> variables,
        Dictionary<string, object> args,
        SubstitutionOptions options)
    {
        // 1. Expand macros (DATE, TIME, DATETIME)
        // 2. Expand environment variables ({{env:VAR}})
        // 3. Expand file references ({{file:PATH}})
        // 4. Perform standard variable substitution
        // 5. Return result
        return Result<string>.Success(content);
    }
}
```

### Error Collection Strategy

The substitutor collects **all** missing variables before failing:

**Content:**
```markdown
Welcome {{USER}}!
Your account: {{ACCOUNT_ID}}
Email: {{EMAIL}}
Status: {{STATUS}}
```

**Args:** `{ "user": "John", "email": "john@example.com" }`

**Errors collected:**
```json
{
  "success": false,
  "errors": [
    {
      "type": "MissingRequiredVariable",
      "variable": "ACCOUNT_ID",
      "description": "Account identifier",
      "line": 2
    },
    {
      "type": "MissingRequiredVariable",
      "variable": "STATUS",
      "description": "Account status",
      "line": 4
    }
  ],
  "provided": ["USER", "EMAIL"],
  "missing": ["ACCOUNT_ID", "STATUS"]
}
```

This allows users to fix all issues in one iteration.

---

## Processing Pipeline

### How Core Classes Work Together

The Core classes form a processing pipeline for markdown transformation:

```
┌─────────────────────┐
│   Markdown File     │
│   (template.md)     │
└──────────┬──────────┘
           │
           ▼
    ┌──────────────┐
    │ Markdown     │
    │ Parser       │
    └──────┬───────┘
           │
           ▼
    ┌──────────────────────┐
    │ MarkdownDocument     │
    │ - Variables (defs)   │
    │ - Content (template) │
    └──────┬───────────────┘
           │
           ├─────────────────┐
           │                 │
           ▼                 ▼
    ┌──────────────┐  ┌─────────────┐
    │ Schema       │  │ Variable    │
    │ Generator    │  │ Extractor   │
    └──────┬───────┘  └──────┬──────┘
           │                 │
           ▼                 ▼
    ┌──────────────┐  ┌─────────────┐
    │ JSON Schema  │  │ Variables   │
    │ (template)   │  │ Found       │
    └──────────────┘  └──────┬──────┘
                             │
                             ▼
                      ┌──────────────┐
                      │ Variable     │
    ┌─────────────────┤ Substitutor  │
    │ JSON Args       └──────┬───────┘
    │ (values.json)          │
    └────────────────────────┘
                             │
                             ▼
                      ┌──────────────┐
                      │ Processed    │
                      │ Markdown     │
                      └──────────────┘
```

### Pipeline Stages

#### Stage 1: Parsing

**Input:** File path
**Class:** `MarkdownParser`
**Output:** `MarkdownDocument` with variable definitions and content
**Errors:** YAML parsing, validation errors

```csharp
var parser = new MarkdownParser();
var result = parser.Parse("template.md");

if (!result.Success)
{
    // Handle parsing errors
    Console.WriteLine(JsonOutput.Failure(result.Errors));
    return 1;
}

var document = result.Value;
```

#### Stage 2A: Schema Generation (get-schema command)

**Input:** Variable definitions
**Class:** `SchemaGenerator`
**Output:** JSON schema string
**Errors:** None (pure transformation)

```csharp
var generator = new SchemaGenerator();
var schema = generator.GenerateSchema(document.Variables);

Console.WriteLine(schema);
```

#### Stage 2B: Variable Extraction (generate-header command)

**Input:** Content
**Class:** `VariableExtractor`
**Output:** List of variables found
**Errors:** None (extraction only)

```csharp
var extractor = new VariableExtractor();
var variables = extractor.Extract(document.Content);

// Generate YAML header from extracted variables
var yaml = GenerateYamlHeader(variables);
Console.WriteLine(yaml);
```

#### Stage 3: Validation (validate command)

**Input:** Variables, args
**Class:** `VariableSubstitutor` (validation only)
**Output:** Validation result
**Errors:** Missing required variables

```csharp
var substitutor = new VariableSubstitutor();

// Load JSON args
var args = JsonSerializer.Deserialize<Dictionary<string, object>>(
    File.ReadAllText("args.json"));

// Validate without substituting
var validation = substitutor.Validate(document.Variables, args);

if (!validation.Success)
{
    Console.WriteLine(JsonOutput.Failure(validation.Errors));
    return 1;
}

Console.WriteLine(JsonOutput.Success(new
{
    provided = validation.ProvidedVariables,
    missing = validation.MissingVariables
}));
```

#### Stage 4: Substitution (process command)

**Input:** Content, variables, args
**Class:** `VariableSubstitutor`
**Output:** Processed markdown
**Errors:** Missing variables, resolution failures

```csharp
var result = substitutor.Substitute(
    document.Content,
    document.Variables,
    args);

if (!result.Success)
{
    Console.WriteLine(JsonOutput.Failure(result.Errors));
    return 1;
}

Console.WriteLine(result.Value);
```

### Complete Processing Example

```csharp
public class MarkdownProcessor
{
    public Result<string> ProcessFile(string templatePath, string argsPath)
    {
        // Stage 1: Parse
        var parser = new MarkdownParser();
        var parseResult = parser.Parse(templatePath);

        if (!parseResult.Success)
        {
            return Result<string>.Failure(parseResult.Errors);
        }

        var document = parseResult.Value;

        // Load args
        var argsJson = File.ReadAllText(argsPath);
        var args = JsonSerializer.Deserialize<Dictionary<string, object>>(argsJson);

        // Stage 2: Substitute
        var substitutor = new VariableSubstitutor();
        var substituteResult = substitutor.Substitute(
            document.Content,
            document.Variables,
            args);

        return substituteResult;
    }
}
```

---

## Error Handling Strategy

### Result Pattern

All Core operations return `Result<T>` instead of throwing exceptions:

```csharp
public class Result<T>
{
    public bool Success { get; set; }
    public T Value { get; set; }
    public List<ValidationError> Errors { get; set; }

    public static Result<T> Success(T value)
    {
        return new Result<T>
        {
            Success = true,
            Value = value,
            Errors = new List<ValidationError>()
        };
    }

    public static Result<T> Failure(List<ValidationError> errors)
    {
        return new Result<T>
        {
            Success = false,
            Value = default(T),
            Errors = errors
        };
    }
}

public class ValidationError
{
    public ErrorType Type { get; set; }
    public string Variable { get; set; }
    public string Description { get; set; }
    public int Line { get; set; }
}

public enum ErrorType
{
    MissingRequiredVariable,
    InvalidYamlHeader,
    InvalidJsonArgs,
    FileNotFound,
    InvalidVariableFormat,
    FileReadError
}
```

### Error Collection vs Fail-Fast

Core classes use **error collection** approach:

**Don't do this (fail-fast):**
```csharp
// ❌ Bad: Stops at first error
foreach (var variable in variables)
{
    if (!IsValid(variable))
    {
        throw new Exception($"Invalid: {variable}");
    }
}
```

**Do this (error collection):**
```csharp
// ✅ Good: Collects all errors
var errors = new List<ValidationError>();

foreach (var variable in variables)
{
    if (!IsValid(variable))
    {
        errors.Add(new ValidationError
        {
            Type = ErrorType.InvalidVariableFormat,
            Variable = variable,
            Description = "Variable format is invalid"
        });
    }
}

if (errors.Any())
{
    return Result<T>.Failure(errors);
}
```

### Benefits of Error Collection

1. **Better UX** - Users see all issues at once, not one at a time
2. **Faster iteration** - Fix multiple issues in single iteration
3. **AI-friendly** - LLMs can address all errors simultaneously
4. **Comprehensive feedback** - Complete error context

### Exception vs Result Pattern

**Use exceptions for:**
- File I/O failures (disk full, permissions)
- System failures (out of memory)
- Unrecoverable errors

**Use Result pattern for:**
- Business logic validation
- Missing data
- Format errors
- User input errors

### Error Reporting Example

```csharp
// Command-level error handling
public class ProcessCommand : Command
{
    public int Execute(string file, string args)
    {
        var processor = new MarkdownProcessor();
        var result = processor.ProcessFile(file, args);

        if (!result.Success)
        {
            // Convert to JSON and output
            var json = JsonOutput.Failure(result.Errors);
            Console.WriteLine(json);
            return 1; // Exit code for error
        }

        var output = JsonOutput.Success(result.Value);
        Console.WriteLine(output);
        return 0; // Exit code for success
    }
}
```

### Structured Error Output

```json
{
  "success": false,
  "errors": [
    {
      "type": "InvalidYamlHeader",
      "variable": "PORT",
      "description": "Optional variable 'PORT' must have a default value",
      "line": 5
    },
    {
      "type": "MissingRequiredVariable",
      "variable": "NAME",
      "description": "The application name",
      "line": 12
    }
  ],
  "provided": ["BRANCH", "ENVIRONMENT"],
  "missing": ["NAME", "EMAIL"]
}
```

---

## Code Examples

### Example 1: Complete Parsing Flow

```csharp
using MDTool.Core;
using MDTool.Models;

public class Example1_Parsing
{
    public static void Run()
    {
        // Create parser
        var parser = new MarkdownParser();

        // Parse markdown file
        var result = parser.Parse("template.md");

        if (!result.Success)
        {
            Console.WriteLine("Parsing failed:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error.Type}: {error.Description}");
            }
            return;
        }

        var document = result.Value;

        // Access parsed data
        Console.WriteLine($"Found {document.Variables.Count} variables:");
        foreach (var varDef in document.Variables.Values)
        {
            Console.WriteLine($"  {varDef.Name}:");
            Console.WriteLine($"    Description: {varDef.Description}");
            Console.WriteLine($"    Required: {varDef.Required}");
            if (varDef.DefaultValue != null)
            {
                Console.WriteLine($"    Default: {varDef.DefaultValue}");
            }
        }

        Console.WriteLine($"\nContent length: {document.Content.Length} chars");
    }
}
```

### Example 2: Schema Generation

```csharp
public class Example2_SchemaGeneration
{
    public static void Run()
    {
        // Define variables (would normally come from parser)
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["APP_NAME"] = new VariableDefinition
            {
                Name = "APP_NAME",
                Description = "Application name",
                Required = true
            },
            ["ENVIRONMENT"] = new VariableDefinition
            {
                Name = "ENVIRONMENT",
                Description = "Deployment environment",
                Required = false,
                DefaultValue = "staging"
            },
            ["DATABASE.HOST"] = new VariableDefinition
            {
                Name = "DATABASE.HOST",
                Description = "Database hostname",
                Required = true
            },
            ["DATABASE.PORT"] = new VariableDefinition
            {
                Name = "DATABASE.PORT",
                Description = "Database port",
                Required = false,
                DefaultValue = 5432
            }
        };

        // Generate schema
        var generator = new SchemaGenerator();
        var schema = generator.GenerateSchema(variables);

        Console.WriteLine("Generated schema:");
        Console.WriteLine(schema);

        // Output:
        // {
        //   "app_name": "Application name",
        //   "environment": "staging",
        //   "database": {
        //     "host": "Database hostname",
        //     "port": 5432
        //   }
        // }
    }
}
```

### Example 3: Variable Extraction

```csharp
public class Example3_VariableExtraction
{
    public static void Run()
    {
        var content = @"
# Welcome {{USER.NAME}}

Your account {{ACCOUNT_ID}} is active.

Settings:
- Environment: {{ENVIRONMENT}}
- Database: {{DATABASE.HOST}}:{{DATABASE.PORT}}
";

        // Extract variables
        var extractor = new VariableExtractor();
        var variables = extractor.Extract(content);

        Console.WriteLine($"Found {variables.Count} unique variables:");
        foreach (var variable in variables)
        {
            Console.WriteLine($"  {variable.Name} (line {variable.Line})");
        }

        // Output:
        // Found 5 unique variables:
        //   ACCOUNT_ID (line 3)
        //   DATABASE.HOST (line 7)
        //   DATABASE.PORT (line 7)
        //   ENVIRONMENT (line 6)
        //   USER.NAME (line 2)
    }
}
```

### Example 4: Variable Substitution

```csharp
public class Example4_Substitution
{
    public static void Run()
    {
        var content = "Welcome {{USER.NAME}}! Your status: {{STATUS}}";

        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER.NAME"] = new VariableDefinition
            {
                Name = "USER.NAME",
                Description = "User's full name",
                Required = true
            },
            ["STATUS"] = new VariableDefinition
            {
                Name = "STATUS",
                Description = "Account status",
                Required = false,
                DefaultValue = "active"
            }
        };

        var args = new Dictionary<string, object>
        {
            ["user"] = new Dictionary<string, object>
            {
                ["name"] = "John Doe"
            }
            // STATUS not provided, will use default
        };

        // Perform substitution
        var substitutor = new VariableSubstitutor();
        var result = substitutor.Substitute(content, variables, args);

        if (!result.Success)
        {
            Console.WriteLine("Substitution failed:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error.Description}");
            }
            return;
        }

        Console.WriteLine("Result:");
        Console.WriteLine(result.Value);

        // Output:
        // Result:
        // Welcome John Doe! Your status: active
    }
}
```

### Example 5: Error Collection

```csharp
public class Example5_ErrorCollection
{
    public static void Run()
    {
        var content = @"
Name: {{NAME}}
Email: {{EMAIL}}
Account: {{ACCOUNT_ID}}
Status: {{STATUS}}
";

        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition
            {
                Name = "NAME",
                Description = "User name",
                Required = true
            },
            ["EMAIL"] = new VariableDefinition
            {
                Name = "EMAIL",
                Description = "Email address",
                Required = true
            },
            ["ACCOUNT_ID"] = new VariableDefinition
            {
                Name = "ACCOUNT_ID",
                Description = "Account ID",
                Required = true
            },
            ["STATUS"] = new VariableDefinition
            {
                Name = "STATUS",
                Description = "Status",
                Required = true
            }
        };

        // Only provide 2 out of 4 required variables
        var args = new Dictionary<string, object>
        {
            ["name"] = "John",
            ["email"] = "john@example.com"
            // Missing: ACCOUNT_ID, STATUS
        };

        var substitutor = new VariableSubstitutor();
        var result = substitutor.Substitute(content, variables, args);

        if (!result.Success)
        {
            Console.WriteLine("Errors found:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  Line {error.Line}: {error.Variable} - {error.Description}");
            }

            // Output:
            // Errors found:
            //   Line 3: ACCOUNT_ID - Account ID
            //   Line 4: STATUS - Status
        }
    }
}
```

### Example 6: Complete Processing Pipeline

```csharp
public class Example6_CompletePipeline
{
    public static void Main(string[] args)
    {
        var templatePath = "template.md";
        var argsPath = "args.json";
        var outputPath = "output.md";

        // Stage 1: Parse markdown
        var parser = new MarkdownParser();
        var parseResult = parser.Parse(templatePath);

        if (!parseResult.Success)
        {
            Console.Error.WriteLine("Parse failed:");
            Console.Error.WriteLine(JsonOutput.Failure(parseResult.Errors));
            Environment.Exit(1);
        }

        var document = parseResult.Value;

        // Stage 2: Load JSON args
        Dictionary<string, object> args;
        try
        {
            var argsJson = File.ReadAllText(argsPath);
            args = JsonSerializer.Deserialize<Dictionary<string, object>>(
                argsJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load args: {ex.Message}");
            Environment.Exit(1);
        }

        // Stage 3: Validate
        var substitutor = new VariableSubstitutor();
        var substituteResult = substitutor.Substitute(
            document.Content,
            document.Variables,
            args);

        if (!substituteResult.Success)
        {
            Console.Error.WriteLine("Substitution failed:");
            Console.Error.WriteLine(JsonOutput.Failure(substituteResult.Errors));
            Environment.Exit(1);
        }

        // Stage 4: Output
        var processedContent = substituteResult.Value;
        File.WriteAllText(outputPath, processedContent);

        Console.WriteLine($"Successfully processed: {outputPath}");
        Console.WriteLine(JsonOutput.Success(new
        {
            template = templatePath,
            output = outputPath,
            variables_substituted = document.Variables.Count
        }));
    }
}
```

### Example 7: Nested Object Handling

```csharp
public class Example7_NestedObjects
{
    public static void Run()
    {
        var content = @"
Database Configuration:
- Host: {{DATABASE.CONNECTION.HOST}}
- Port: {{DATABASE.CONNECTION.PORT}}
- Username: {{DATABASE.AUTH.USERNAME}}
- Database: {{DATABASE.NAME}}
";

        var variables = new Dictionary<string, VariableDefinition>
        {
            ["DATABASE.CONNECTION.HOST"] = new VariableDefinition
            {
                Name = "DATABASE.CONNECTION.HOST",
                Description = "DB host",
                Required = true
            },
            ["DATABASE.CONNECTION.PORT"] = new VariableDefinition
            {
                Name = "DATABASE.CONNECTION.PORT",
                Description = "DB port",
                Required = false,
                DefaultValue = 5432
            },
            ["DATABASE.AUTH.USERNAME"] = new VariableDefinition
            {
                Name = "DATABASE.AUTH.USERNAME",
                Description = "DB username",
                Required = true
            },
            ["DATABASE.NAME"] = new VariableDefinition
            {
                Name = "DATABASE.NAME",
                Description = "Database name",
                Required = true
            }
        };

        // JSON with nested structure
        var argsJson = @"
{
  ""database"": {
    ""connection"": {
      ""host"": ""localhost""
    },
    ""auth"": {
      ""username"": ""admin""
    },
    ""name"": ""myapp""
  }
}";

        var args = JsonSerializer.Deserialize<Dictionary<string, object>>(
            argsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var substitutor = new VariableSubstitutor();
        var result = substitutor.Substitute(content, variables, args);

        Console.WriteLine(result.Value);

        // Output:
        // Database Configuration:
        // - Host: localhost
        // - Port: 5432
        // - Username: admin
        // - Database: myapp
    }
}
```

---

## Summary

The **Core** namespace provides the foundational building blocks for MDTool:

1. **MarkdownParser** - Extracts and validates YAML frontmatter
2. **VariableExtractor** - Finds variable references in content
3. **SchemaGenerator** - Creates JSON templates from definitions
4. **VariableSubstitutor** - Replaces variables with actual values

These classes work together in a processing pipeline, using the Result pattern for error handling and collecting all errors for comprehensive feedback.

The design is extensible for Phase 2 enhancements (macros, file expansion, environment variables) while maintaining clean separation of concerns and testability.

---

**References:**
- Master Checklist: `/Users/randlee/Documents/github/mdtool/docs/master-checklist.md`
- Implementation Plan: `/Users/randlee/Documents/github/mdtool/docs/mdtool-implementation-plan.md`
- Phase 1 Core Parsing Tasks: Lines 97-136 of master checklist
