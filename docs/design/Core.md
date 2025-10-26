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
    public ProcessingResult<MarkdownDocument> ParseContent(string content);
    public static (bool hasYaml, string yaml, string body) SplitFrontmatter(string content);
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
    private static readonly Regex VarNameRegex = new(@"^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$", RegexOptions.Compiled);

    public MarkdownParser()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    // Core parses content only; file I/O is handled by Utilities/Commands
    public ProcessingResult<MarkdownDocument> ParseContent(string content)
    {
        var errors = new List<ValidationError>();

        // Step 1: Detect frontmatter boundaries
        var (hasYaml, yamlContent, markdownContent) = ExtractFrontmatter(content);

        // Step 2: Handle missing frontmatter
        if (!hasYaml)
        {
            return ProcessingResult<MarkdownDocument>.Ok(new MarkdownDocument(
                new Dictionary<string, VariableDefinition>(),
                markdownContent,
                null
            ));
        }

        // Step 3: Parse YAML
        Dictionary<string, object> yamlData;
        try
        {
            var yamlObject = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent) ?? new();

            if (!yamlObject.ContainsKey("variables"))
            {
                errors.Add(ValidationError.InvalidYaml("YAML frontmatter missing 'variables:' section"));
                return ProcessingResult<MarkdownDocument>.Fail(errors);
            }

            yamlData = yamlObject["variables"] as Dictionary<string, object>;
            if (yamlData == null)
            {
                errors.Add(ValidationError.InvalidYaml("'variables' must be a dictionary"));
                return ProcessingResult<MarkdownDocument>.Fail(errors);
            }
        }
        catch (YamlException ex)
        {
            errors.Add(ValidationError.InvalidYaml($"YAML parsing error: {ex.Message}"));
            return ProcessingResult<MarkdownDocument>.Fail(errors);
        }

        // Step 4: Convert to VariableDefinitions and validate
        var variables = new Dictionary<string, VariableDefinition>(StringComparer.Ordinal);
        var rawNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, val) in yamlData)
        {
            if (!VarNameRegex.IsMatch(key))
            {
                errors.Add(ValidationError.InvalidFormat(key, "Variable name must be UPPERCASE with optional dot-separated segments"));
                continue;
            }
            rawNames.Add(key);

            var parsed = ParseVariableDefinition(key, val);
            if (parsed.Error != null)
            {
                errors.Add(parsed.Error);
                continue;
            }
            variables[key] = parsed.Definition;
        }

        // Conflict rule: disallow both X and X.*
        foreach (var name in rawNames)
        {
            var prefix = name + ".";
            if (rawNames.Any(n => n.StartsWith(prefix, StringComparison.Ordinal)))
            {
                errors.Add(ValidationError.InvalidFormat(name, $"Conflicting variable paths: '{name}' and '{name}*'"));
            }
        }

        // Optional variables must have defaults
        foreach (var v in variables.Values)
        {
            if (!v.Required && v.DefaultValue == null)
            {
                errors.Add(ValidationError.InvalidYaml($"Optional variable '{v.Name}' must have a default value"));
            }
        }

        if (errors.Any())
        {
            return ProcessingResult<MarkdownDocument>.Fail(errors);
        }

        return ProcessingResult<MarkdownDocument>.Ok(new MarkdownDocument(variables, markdownContent, yamlContent));
    }

    public static (bool hasYaml, string yaml, string content) ExtractFrontmatter(string content)
    {
        if (!content.TrimStart().StartsWith("---"))
            return (false, string.Empty, content);

        var lines = content.Split('\n');
        int startIndex = -1, endIndex = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---") { startIndex = i; break; }
        }
        if (startIndex == -1) return (false, string.Empty, content);

        for (int i = startIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---") { endIndex = i; break; }
        }
        if (endIndex == -1) return (false, string.Empty, content);

        var yaml = string.Join('\n', lines.Skip(startIndex + 1).Take(endIndex - startIndex - 1));
        var body = string.Join('\n', lines.Skip(endIndex + 1));
        return (true, yaml, body);
    }

    private (VariableDefinition Definition, ValidationError Error) ParseVariableDefinition(string name, object value)
    {
        if (value is string s)
        {
            return (new VariableDefinition(name, s, required: true, defaultValue: null), null);
        }
        if (value is Dictionary<object, object> obj)
        {
            var dict = obj.ToDictionary(k => k.Key.ToString(), v => v.Value);
            if (!dict.ContainsKey("description"))
            {
                return (null, ValidationError.InvalidYaml($"Variable '{name}' object must have 'description' field"));
            }
            var description = dict["description"]?.ToString() ?? string.Empty;
            var required = dict.ContainsKey("required") ? Convert.ToBoolean(dict["required"]) : true;
            var defaultValue = dict.ContainsKey("default") ? dict["default"] : null;
            if (!required && defaultValue == null)
            {
                return (null, ValidationError.InvalidYaml($"Optional variable '{name}' must have a default value"));
            }
            return (new VariableDefinition(name, description, required, defaultValue), null);
        }
        return (null, ValidationError.InvalidYaml($"Variable '{name}' must be a string or object"));
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

private const int MaxNestingDepth = 10;

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
        var schema = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var variable in variables.Values.OrderBy(v => v.Name))
        {
            var path = variable.Name.Split('.');
            var current = schema;

            // Navigate/create nested structure
            for (int i = 0; i < path.Length - 1; i++)
            {
                var segmentKey = ToLowerCamel(path[i]);
                if (!current.TryGetValue(segmentKey, out var next) || next is not Dictionary<string, object> dict)
                {
                    dict = new Dictionary<string, object>(StringComparer.Ordinal);
                    current[segmentKey] = dict;
                }
                current = dict;
            }

            // Set final value
            var finalKey = ToLowerCamel(path[^1]);
            var value = GetSchemaValue(variable);
            current[finalKey] = value;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(schema, options);
    }

    private static string ToLowerCamel(string segment)
    {
        if (string.IsNullOrEmpty(segment)) return segment;
        // Convert UPPER or UPPER_SNAKE to lowerCamel
        var parts = segment.Split('_', StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.ToLowerInvariant()).ToArray();
        if (parts.Length == 0) return string.Empty;
        return parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private object GetSchemaValue(VariableDefinition variable)
    {
        if (!variable.Required && variable.DefaultValue != null)
            return variable.DefaultValue;
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

Variable names in markdown are **UPPERCASE**, but JSON keys are converted to **lowerCamelCase** for conventional JSON formatting:

- `{{NAME}}` → `"name": "..."`
- `{{USER.EMAIL}}` → `"user": { "email": "..." }`

Conflict rule: if both `X` and any `X.*` variables exist in YAML, parsing fails with InvalidVariableFormat.

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

## ConditionalEvaluator Class

### Responsibility

Evaluate conditional blocks (`{{#if}}`, `{{else if}}`, `{{else}}`, `{{/if}}`) in markdown content and prune sections based on boolean expressions evaluated against provided arguments. This enables a single template to target multiple roles or scenarios (e.g., QA agent roles like TEST vs REPORT).

### Overview

The ConditionalEvaluator processes markdown content in the following order:

1. **Load args and defaults**: Merge JSON arguments with YAML defaults
2. **Evaluate conditionals**: Process `{{#if}}` blocks to produce "effective content"
3. **Variable extraction/substitution**: Operate only on effective content

This ensures that:
- Only variables referenced in effective content are required
- Content is deterministic and side-effect-free
- Nested conditionals are supported up to MaxNesting depth

### Public Interface

```csharp
public class ConditionalEvaluator
{
    // Basic evaluation (returns pruned content only)
    public ProcessingResult<string> Evaluate(
        string content,
        IArgsAccessor args,
        ConditionalOptions options);

    // Detailed evaluation (returns pruned content and a machine-readable trace)
    public ProcessingResult<(string Content, ConditionalTrace Trace)> EvaluateDetailed(
        string content,
        IArgsAccessor args,
        ConditionalOptions options);
}

public record ConditionalOptions(
    bool Strict = false,
    bool CaseSensitiveStrings = false,
    int MaxNesting = 10
);

public interface IArgsAccessor
{
    bool TryGet(string path, out object? value);
}

public sealed class ArgsJsonAccessor : IArgsAccessor
{
    // Wraps JsonDocument/JsonElement and supports case-insensitive, dot-path lookups
    public ArgsJsonAccessor(JsonDocument document);
    public bool TryGet(string path, out object? value);
}

public sealed class ConditionalTrace
{
    public List<ConditionalBlockTrace> Blocks { get; init; } = new();
}

public sealed class ConditionalBlockTrace
{
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public List<ConditionalBranchTrace> Branches { get; init; } = new();
}

public sealed class ConditionalBranchTrace
{
    public string Kind { get; init; } = "if"; // if | else-if | else
    public string? Expr { get; init; }
    public bool Taken { get; init; }
}
```

### IArgsAccessor Abstraction

The `IArgsAccessor` interface provides a clean abstraction for variable lookups:

**Purpose:**
- Case-insensitive key lookups (ROLE, role, Role are equivalent)
- Dot-path navigation for nested values (`USER.NAME`)
- Decouples conditional evaluation from JSON implementation

**Implementation:**
```csharp
public interface IArgsAccessor
{
    bool TryGet(string path, out object? value);
}
```

**ArgsJsonAccessor:**
- Wraps `JsonDocument` for JSON-backed arguments
- Supports case-insensitive property matching
- Handles dot notation for nested objects
- Returns typed values (string, number, boolean)

### Tag Syntax

Conditional blocks use the following syntax:

```markdown
{{#if EXPR}}
  ... content when EXPR is true ...
{{else if EXPR2}}
  ... content when EXPR2 is true ...
{{else}}
  ... content when none of the above are true ...
{{/if}}
```

**Rules:**
- Tags must be balanced and properly nested
- Maximum nesting depth: 10 (configurable via `MaxNesting`)
- Whitespace inside tags is ignored: `{{#if   EXPR}}` is valid
- Tags may span multiple lines; content preserves original formatting
- Multiple `{{else if}}` branches are supported
- `{{else}}` is optional

**Errors:**
- Mismatched or improperly nested tags → `InvalidVariableFormat` (with line number)
- Expression parse/evaluation failure → `ProcessingError` (with description and line)
- Nesting beyond limit → `RecursionDepthExceeded`

### Expression Syntax and Operators

**Supported Operators:**

| Operator | Type | Precedence | Description |
|----------|------|------------|-------------|
| `!` | Unary | Highest | Logical NOT |
| `&&` | Binary | Medium | Logical AND |
| `\|\|` | Binary | Lowest | Logical OR |
| `==` | Binary | Medium | Equality (type-aware) |
| `!=` | Binary | Medium | Inequality (type-aware) |
| `()` | Grouping | Highest | Parentheses for precedence |

**Literal Values:**
- **Strings**: Single or double quotes: `'TEST'`, `"QA"`
- **Numbers**: Integer or decimal: `123`, `45.67`
- **Booleans**: `true`, `false`

**Variable References:**
- Variables are referenced by name: `ROLE`, `AGENT.NAME`
- Case-insensitive lookup: `ROLE`, `role`, `Role` are equivalent
- Dot notation supported for nested values

**Examples:**
```
ROLE == 'TEST'
ROLE == 'TEST' || ROLE == 'REPORT'
ROLE == 'TEST' && !DEBUG
in(ROLE, ['TEST', 'REPORT']) && ENVIRONMENT == 'DEV'
(ROLE == 'TEST' || ROLE == 'REPORT') && exists(AGENT)
```

### Functions

The ConditionalEvaluator supports the following built-in functions:

#### contains(haystack, needle)

Tests if a string contains a substring.

**Aliases:** `haystack.Contains(needle)` (method-style)

**Parameters:**
- `haystack` - String to search in
- `needle` - Substring to search for

**Returns:** Boolean

**Case Behavior:**
- Default: Case-insensitive
- With `CaseSensitiveStrings`: Case-sensitive

**Examples:**
```
ROLE.Contains('TEST')
contains(AGENT, 'QA')
```

#### startsWith(text, prefix)

Tests if a string starts with a prefix.

**Aliases:** `text.StartsWith(prefix)` (method-style)

**Parameters:**
- `text` - String to test
- `prefix` - Prefix to check

**Returns:** Boolean

**Case Behavior:**
- Default: Case-insensitive
- With `CaseSensitiveStrings`: Case-sensitive

**Examples:**
```
AGENT.StartsWith('QA')
startsWith(ROLE, 'TEST')
```

#### endsWith(text, suffix)

Tests if a string ends with a suffix.

**Aliases:** `text.EndsWith(suffix)` (method-style)

**Parameters:**
- `text` - String to test
- `suffix` - Suffix to check

**Returns:** Boolean

**Case Behavior:**
- Default: Case-insensitive
- With `CaseSensitiveStrings`: Case-sensitive

**Examples:**
```
AGENT.EndsWith('1')
endsWith(ENVIRONMENT, 'PROD')
```

#### in(value, array)

Tests if a value is in an array of values.

**Parameters:**
- `value` - Value to search for
- `array` - Array literal: `['a', 'b', 'c']`

**Returns:** Boolean

**Type Behavior:**
- Array elements can be strings, numbers, or booleans
- Comparisons are type-safe (string != number)
- Strings compared per current case mode

**Examples:**
```
in(ROLE, ['TEST', 'REPORT'])
in(PORT, [8080, 3000, 5000])
in(DEBUG, [true])
```

#### exists(VAR)

Tests if a variable is present in the arguments (after merging defaults).

**Parameters:**
- `VAR` - Variable name (unquoted)

**Returns:** Boolean

**Behavior:**
- Returns `true` if variable is present (even if value is null/empty)
- Returns `false` if variable is not in args or defaults

**Examples:**
```
exists(AGENT)
exists(DEBUG)
!exists(OPTIONAL_VAR)
```

### Type Awareness and Case Modes

**Type Semantics:**
- MarkdownParser preserves YAML default value types (int, double, bool, string)
- Args JSON is parsed with native types
- Expression evaluation is type-aware; no implicit string<->number coercion
- Mismatched type comparisons evaluate to `false` (or error in Strict mode)

**Supported Value Types:**
- `string`
- `number` (int or double)
- `boolean`

**Case Behavior:**

| Context | Default | With `--strict-conditions` |
|---------|---------|----------------------------|
| Variable lookup | Case-insensitive | Case-insensitive |
| String comparisons | Case-insensitive | Case-sensitive |
| Function arguments | Case-insensitive | Case-sensitive |

**Variable Lookup:**
- Keys are always case-insensitive: `ROLE`, `role`, `Role` equivalent

**String Comparisons:**
- Default mode: `"TEST" == "test"` → true
- Strict mode (`CaseSensitiveStrings`): `"TEST" == "test"` → false

**Type Examples:**
```
ROLE == 'TEST'          // String equality
PORT == 8080            // Number equality
DEBUG == true           // Boolean equality
PORT == '8080'          // false (number != string)
```

### Unknown Variables in Expressions

**Default Behavior:**
- Unknown variables evaluate to `false`
- No error is raised
- Allows optional variables in conditionals

**Strict Mode (`--strict-conditions`):**
- Unknown variables cause `ProcessingError`
- Error includes variable name and context
- Forces explicit declaration of all variables used in expressions

**Examples:**

Default mode:
```
{{#if OPTIONAL_VAR == 'value'}}
  This won't show if OPTIONAL_VAR is undefined
{{/if}}
```

Strict mode:
```
{{#if OPTIONAL_VAR == 'value'}}
  ERROR: Unknown variable 'OPTIONAL_VAR'
{{/if}}
```

**Best Practice:**
Use `exists(VAR)` to explicitly check for presence:
```
{{#if exists(OPTIONAL_VAR) && OPTIONAL_VAR == 'value'}}
  This is safe in both modes
{{/if}}
```

### Evaluation Algorithm

**High-Level Flow:**

1. **Tokenize**: Find all `{{#if}}`, `{{else if}}`, `{{else}}`, `{{/if}}` tags
2. **Build Block Stack**: Track nesting level, enforce MaxNesting
3. **Verify Structure**: Ensure balanced tags
4. **Evaluate Blocks**: For each block:
   - Parse expressions left-to-right
   - Evaluate expressions until a true branch is found
   - Keep content from true branch, drop others
5. **Return**: Concatenation of kept content segments

**Expression Engine:**
- Shunting-yard or Pratt parser for precedence and parentheses
- Operators and functions per spec
- Values resolve via `IArgsAccessor`

**Precedence Rules:**
1. `()` - Parentheses (highest)
2. `!` - Unary NOT
3. `==`, `!=` - Equality
4. `&&` - Logical AND
5. `||` - Logical OR (lowest)

### Error Handling

**Error Types:**

| Error Type | Trigger | Example |
|------------|---------|---------|
| `InvalidVariableFormat` | Mismatched tags | `{{#if}}` without `{{/if}}` |
| `ProcessingError` | Bad expression | `{{#if ROLE =}}` (invalid syntax) |
| `RecursionDepthExceeded` | Nesting > MaxNesting | 11 levels of nested `{{#if}}` |
| `ProcessingError` | Unknown variable (strict) | `{{#if UNDEFINED == 'x'}}` |

**Error Context:**
- All errors include line numbers
- Expression errors include the expression text
- Tag mismatch errors show the offending tag

**JsonOutput Format:**
```json
{
  "success": false,
  "errors": [
    {
      "type": "ProcessingError",
      "description": "Expression parsing failed: unexpected token '='",
      "line": 12,
      "context": "ROLE ="
    }
  ]
}
```

### Code Fence Protection

**Important:** Conditional tags inside code fences are **still parsed**. MDTool does not perform language-sensitive parsing.

**Example:**
```markdown
{{#if ROLE == 'TEST'}}
This shows for TEST role.

```bash
echo "{{VARIABLE}}"
```
{{/if}}
```

**Behavior:**
- The outer `{{#if}}` is evaluated
- The inner `{{VARIABLE}}` is treated as a variable placeholder
- Code fences do not create "literal zones"

**Workaround:**
If you need literal `{{` in code, use HTML entities or escape sequences (future enhancement).

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

The Core classes form a processing pipeline for markdown transformation. As of v1.1.0, conditional sections are evaluated after args+defaults are merged and before variable extraction/substitution.

### How Core Classes Work Together

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
    ┌──────────────┐  ┌─────────────────────┐
    │ Schema       │  │ 1. Load Args+Defaults│
    │ Generator    │  │ 2. Conditional Eval  │
    └──────┬───────┘  │    (optional)        │
           │          └──────┬──────────────┘
           ▼                 │
    ┌──────────────┐         ▼
    │ JSON Schema  │  ┌─────────────────┐
    │ (template)   │  │ Effective       │
    └──────────────┘  │ Content         │
                      └──────┬──────────┘
                             │
                             ▼
                      ┌──────────────┐
                      │ Variable     │
                      │ Extractor    │
                      └──────┬───────┘
                             │
                             ▼
                      ┌──────────────┐
                      │ Variables    │
                      │ Found        │
                      └──────┬───────┘
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
