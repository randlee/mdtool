# Commands Namespace Design Document

**Project:** MDTool - Markdown processing with variable substitution
**Version:** 1.0.0
**Last Updated:** 2025-10-25

---

## Table of Contents

1. [Overview](#overview)
2. [GetSchemaCommand Class](#getschemacommand-class)
3. [ValidateCommand Class](#validatecommand-class)
4. [ProcessCommand Class](#processcommand-class)
5. [GenerateHeaderCommand Class](#generateheadercommand-class)
6. [Program.cs Integration](#programcs-integration)
7. [Command Patterns](#command-patterns)
8. [Code Examples](#code-examples)

---

## Overview

The Commands namespace contains the CLI command implementations for MDTool. Each command is implemented as a **separate class** that inherits from `System.CommandLine.Command`. This design follows the Single Responsibility Principle and provides clear separation of concerns.

### Purpose

The Commands namespace serves as the presentation layer of MDTool, translating user input into operations performed by the Core classes. Each command:

- Defines its own arguments and options
- Validates user input
- Invokes Core processing logic
- Formats and outputs results
- Handles errors consistently
- Returns appropriate exit codes

### System.CommandLine Integration

MDTool uses the `System.CommandLine` library (2.0.0-beta4 or later) to provide a modern, standards-compliant CLI experience. Key features:

- **Strongly-typed arguments and options**: Type-safe parameter binding
- **Automatic help generation**: Built-in `--help` support for each command
- **Tab completion support**: Shell integration for improved UX
- **Consistent error handling**: Standardized error reporting
- **Async/await support**: Modern asynchronous programming patterns

### Command Architecture

```
Program.cs (RootCommand)
├── GetSchemaCommand.cs      → Extract variable schema from markdown
├── ValidateCommand.cs       → Validate JSON args against schema
├── ProcessCommand.cs        → Perform variable substitution
└── GenerateHeaderCommand.cs → Auto-generate YAML frontmatter
```

Each command is **completely independent** and can be tested, maintained, and extended separately.

---

## GetSchemaCommand Class

### Purpose

Extracts variable definitions from a markdown file's YAML frontmatter and generates a JSON schema. This schema serves as a template that users can populate with actual values.

### Command Signature

```
mdtool get-schema <file> [--output <path>]
```

### Arguments

| Argument | Type   | Required | Description                    |
|----------|--------|----------|--------------------------------|
| `<file>` | string | Yes      | Path to markdown template file |

### Options

| Option            | Type   | Required | Description                          |
|-------------------|--------|----------|--------------------------------------|
| `--output <path>` | string | No       | Output file path (default: stdout)   |

### Processing Flow

```
1. Parse markdown file
   ├── Read file contents
   ├── Split YAML frontmatter from content
   └── Parse YAML into VariableDefinition objects

2. Generate JSON schema
   ├── Convert variable definitions to JSON structure
   ├── Convert each path segment to lowerCamelCase
   ├── Use descriptions as placeholder values
   ├── Include default values for optional variables
   └── Support nested object structure (dot notation)

3. Output schema
   ├── Format as pretty-printed JSON
   ├── Write to stdout (default) or --output file
   └── Use JsonOutput format for errors
```

### Exit Codes

| Code | Meaning                                  |
|------|------------------------------------------|
| 0    | Success - schema generated               |
| 1    | Error - file not found, invalid YAML, etc|

### Error Handling

All errors are returned in structured JSON format:

```json
{
  "success": false,
  "errors": [
    {
      "type": "InvalidYamlHeader",
      "description": "YAML parsing failed at line 5: unexpected token",
      "line": 5
    }
  ]
}
```

### Usage Examples

**Example 1: Output to stdout**
```bash
mdtool get-schema template.md
```

**Example 2: Save to file**
```bash
mdtool get-schema template.md --output schema.json
```

**Example 3: Pipe to other tools**
```bash
mdtool get-schema template.md | jq '.user.name'
```

**Input file (template.md):**
```markdown
---
variables:
  NAME: "Application name"
  PORT:
    description: "Server port"
    default: 8080
  USER.EMAIL: "User email address"
---

# Welcome to {{NAME}}
```

**Output (schema.json):**
```json
{
  "name": "Application name",
  "port": 8080,
  "user": {
    "email": "User email address"
  }
}
```

---

## ValidateCommand Class

### Purpose

Validates that a JSON arguments file contains all required variables defined in the markdown template. This allows pre-flight validation before processing.

### Command Signature

```
mdtool validate <file> --args <path>
```

### Arguments

| Argument | Type   | Required | Description                    |
|----------|--------|----------|--------------------------------|
| `<file>` | string | Yes      | Path to markdown template file |

### Options

| Option          | Type   | Required | Description                     |
|-----------------|--------|----------|---------------------------------|
| `--args <path>` | string | Yes      | Path to JSON arguments file     |
| `--conditions-trace-out <path>` | string | No | Write JSON trace of conditional decisions |
| `--no-conditions` | flag | No | Skip conditional evaluation (tags remain as literals) |
| `--strict-conditions` | flag | No | Unknown variables in expressions cause errors; case-sensitive string comparisons |
| `--require-all-yaml` | flag | No | Require all YAML-required variables (not just content-scoped) |

### Conditional Evaluation Options (v1.1.0+)

As of v1.1.0, ValidateCommand supports conditional sections via the following options:

#### --conditions-trace-out

**Purpose:** Write a JSON trace of conditional evaluation decisions to a file for debugging.

**Usage:**
```bash
mdtool validate template.md --args args.json --conditions-trace-out trace.json
```

**Trace Output Format:**
```json
{
  "blocks": [
    {
      "startLine": 12,
      "endLine": 26,
      "branches": [
        { "kind": "if", "expr": "ROLE == 'TEST'", "taken": false },
        { "kind": "else-if", "expr": "ROLE.Contains('REPORT')", "taken": true },
        { "kind": "else", "taken": false }
      ]
    }
  ]
}
```

**Behavior:**
- Trace is written to the specified file path
- Validation output (JSON) remains on stdout
- Trace includes all conditional blocks with their evaluation results
- Useful for debugging complex conditional logic

#### --no-conditions

**Purpose:** Disable conditional evaluation; treat all conditional tags as literal text.

**Usage:**
```bash
mdtool validate template.md --args args.json --no-conditions
```

**Behavior:**
- All `{{#if}}`, `{{else if}}`, `{{else}}`, `{{/if}}` tags are ignored
- Content validation operates on the full template (no pruning)
- All variables in the template are required (unless optional with defaults)
- Useful for validating that args work with or without conditionals

**Backward Compatibility:**
- This flag ensures templates work with older versions that don't support conditionals
- Validates that non-conditional templates don't break

#### --strict-conditions

**Purpose:** Enable strict conditional evaluation mode.

**Usage:**
```bash
mdtool validate template.md --args args.json --strict-conditions
```

**Behavior Changes:**
1. **Unknown Variables:** Cause `ProcessingError` instead of evaluating to `false`
2. **String Comparisons:** Case-sensitive instead of case-insensitive
   - Default: `"TEST" == "test"` → true
   - Strict: `"TEST" == "test"` → false

**Use Cases:**
- Catching typos in variable names
- Enforcing exact string matching
- Preventing silent failures from undefined variables

**Example Error:**
```json
{
  "success": false,
  "errors": [
    {
      "type": "ProcessingError",
      "description": "Unknown variable 'TYPO_ROLE' in expression",
      "line": 15,
      "context": "TYPO_ROLE == 'TEST'"
    }
  ]
}
```

#### --require-all-yaml

**Purpose:** Require all YAML-declared required variables, regardless of which content is effective.

**Usage:**
```bash
mdtool validate template.md --args args.json --require-all-yaml
```

**Default Behavior (Content-Scoped):**
- Only variables referenced in **effective content** (after conditional evaluation) are required
- If a variable is only referenced in excluded branches, it's not required

**With --require-all-yaml:**
- All variables marked as `required: true` in YAML frontmatter must be provided
- Regardless of whether they appear in effective content or not

**Comparison:**

**Template:**
```yaml
---
variables:
  ROLE:
    description: "Agent role"
    required: true
  TEST_VAR:
    description: "Test-specific variable"
    required: true
  REPORT_VAR:
    description: "Report-specific variable"
    required: true
---

{{#if ROLE == 'TEST'}}
Use {{TEST_VAR}}
{{else if ROLE == 'REPORT'}}
Use {{REPORT_VAR}}
{{/if}}
```

**Args (ROLE=TEST):**
```json
{
  "role": "TEST",
  "testVar": "value"
}
```

**Default validation:**
- Required: ROLE, TEST_VAR
- Not required: REPORT_VAR (not in effective content)
- Result: Success

**With --require-all-yaml:**
- Required: ROLE, TEST_VAR, REPORT_VAR (all marked required in YAML)
- Result: Failure (REPORT_VAR missing)

### Processing Flow

```
1. Parse markdown file
   ├── Read markdown file
   ├── Extract YAML frontmatter
   └── Build variable definitions map

2. Load JSON arguments
   ├── Read JSON file
   ├── Parse JSON (case-insensitive keys)
   └── Handle invalid JSON errors

3. Evaluate conditionals (unless --no-conditions)
   ├── Merge args with defaults
   └── Produce effective content (prune excluded branches)

4. Extract variables from effective content
   └── Use VariableExtractor on effective content

5. Validate arguments
   ├── Default: require only variables referenced in effective content
   ├── If --require-all-yaml: require all YAML-required variables
   └── Collect all validation errors (don't fail on first)

6. Output validation result
   ├── Return structured JSON with success/failure
   ├── Include lists of provided and missing variables
   └── Include detailed error information
```

### Validation Result Format

**Success:**
```json
{
  "success": true,
  "provided": ["NAME", "PORT", "USER.EMAIL"],
  "missing": []
}
```

**Failure:**
```json
{
  "success": false,
  "errors": [
    {
      "type": "MissingRequiredVariable",
      "variable": "NAME",
      "description": "Application name",
      "line": null
    }
  ],
  "provided": ["PORT"],
  "missing": ["NAME", "USER.EMAIL"]
}
```

### Exit Codes

| Code | Meaning                                    |
|------|--------------------------------------------|
| 0    | Success - all required variables present   |
| 1    | Error - validation failed or file errors   |

### Usage Examples

**Example 1: Validate arguments**
```bash
mdtool validate template.md --args args.json
```

**Example 2: Use in scripts**
```bash
if mdtool validate template.md --args args.json > /dev/null 2>&1; then
    echo "Validation passed"
    mdtool process template.md --args args.json
else
    echo "Validation failed"
    exit 1
fi
```

**Example 3: Check validation result**
```bash
mdtool validate template.md --args args.json | jq '.missing'
```

**Input files:**

**template.md:**
```markdown
---
variables:
  NAME: "Application name"
  PORT:
    description: "Server port"
    default: 8080
---

# {{NAME}} running on port {{PORT}}
```

**args.json (valid):**
```json
{
  "name": "MyApp",
  "port": 3000
}
```

**args.json (invalid - missing NAME):**
```json
{
  "port": 3000
}
```

---

## ProcessCommand Class

### Purpose

Performs variable substitution in markdown files, replacing `{{VARIABLE}}` placeholders with actual values from JSON arguments. This is the core command that produces the final processed output.

### Command Signature

```
mdtool process <file> --args <path> [--output <path>] [--force]
```

### Arguments

| Argument | Type   | Required | Description                    |
|----------|--------|----------|--------------------------------|
| `<file>` | string | Yes      | Path to markdown template file |

### Options

| Option            | Type    | Required | Description                              |
|-------------------|---------|----------|------------------------------------------|
| `--args <path>`   | string  | Yes      | Path to JSON arguments file              |
| `--output <path>` | string  | No       | Output file path (default: stdout)       |
| `--force`         | flag    | No       | Overwrite existing output file           |
| `--conditions-trace-out <path>` | string | No | Write JSON trace of conditional decisions |
| `--no-conditions` | flag | No | Skip conditional evaluation (tags remain as literals) |
| `--strict-conditions` | flag | No | Unknown variables in expressions cause errors; case-sensitive string comparisons |

### Conditional Evaluation Options (v1.1.0+)

As of v1.1.0, ProcessCommand supports conditional sections via the following options. These options work identically to ValidateCommand options (see ValidateCommand documentation for detailed examples).

#### --conditions-trace-out

**Purpose:** Write a JSON trace of conditional evaluation decisions to a file for debugging.

**Usage:**
```bash
mdtool process template.md --args args.json --conditions-trace-out trace.json
```

**Behavior:**
- Trace is written to the specified file path
- Processed markdown output remains on stdout
- Trace includes all conditional blocks with their evaluation results

#### --no-conditions

**Purpose:** Disable conditional evaluation; treat all conditional tags as literal text.

**Usage:**
```bash
mdtool process template.md --args args.json --no-conditions
```

**Behavior:**
- All `{{#if}}`, `{{else if}}`, `{{else}}`, `{{/if}}` tags are left in the output as-is
- No conditional pruning occurs
- Variable substitution operates on the full template
- Useful for generating output that will be processed by another system

#### --strict-conditions

**Purpose:** Enable strict conditional evaluation mode.

**Usage:**
```bash
mdtool process template.md --args args.json --strict-conditions
```

**Behavior Changes:**
1. **Unknown Variables:** Cause `ProcessingError` instead of evaluating to `false`
2. **String Comparisons:** Case-sensitive instead of case-insensitive

**Note:** Unlike ValidateCommand, ProcessCommand does not have a `--require-all-yaml` option. This is because ProcessCommand validates based on effective content only (content-scoped validation) before performing substitution.

### Processing Flow

```
1. Parse markdown and load args
   ├── Parse YAML frontmatter and content
   ├── Load JSON arguments (case-insensitive)
   └── Merge YAML defaults

2. Evaluate conditionals (unless --no-conditions)
   ├── Evaluate {{#if}} / {{else if}} / {{else}} / {{/if}} blocks
   ├── Prune excluded branches based on expression results
   ├── Write trace to --conditions-trace-out file (if specified)
   └── Produce effective content

3. Validate (content-scoped)
   ├── Extract variables from effective content
   ├── Require only variables referenced in effective content
   ├── Optional variables use YAML defaults if not provided
   └── Exit with error if validation fails

4. Perform variable substitution on effective content
   ├── Replace {{VARIABLE}} with values from JSON args (case-insensitive)
   ├── Navigate nested objects via dot notation
   ├── Use defaults for missing optional variables
   └── Return substituted content

5. Check file overwrite protection (if --output specified)
   ├── If file exists and not --force → error
   └── Else continue

6. Output processed markdown
   ├── Write to stdout (default) or --output file (UTF-8, no BOM)
   └── Exit code 0 on success
```

**Key Points:**
- Conditionals are evaluated **before** variable extraction
- Only variables in effective content (kept branches) are required
- Trace output (if enabled) is written to a separate file, not stdout
- `--no-conditions` skips step 2 entirely

### File Overwrite Protection Logic

The `--force` flag is required to overwrite existing files:

```csharp
if (File.Exists(outputPath) && !forceOverwrite)
{
    var error = new ValidationError
    {
        Type = ValidationErrorType.FileExists,
        Description = $"File '{outputPath}' already exists. Use --force to overwrite."
    };

    Console.WriteLine(JsonOutput.Failure(new[] { error }));
    return 1;
}
```

### Exit Codes

| Code | Meaning                                       |
|------|-----------------------------------------------|
| 0    | Success - processing completed                |
| 1    | Error - validation failed, file errors, etc   |

### Usage Examples

**Example 1: Output to stdout**
```bash
mdtool process template.md --args args.json
```

**Example 2: Save to file**
```bash
mdtool process template.md --args args.json --output output.md
```

**Example 3: Overwrite existing file**
```bash
mdtool process template.md --args args.json --output output.md --force
```

**Example 4: Use in CI/CD pipeline**
```bash
# Generate deployment plan
mdtool process deploy-template.md \
  --args production-config.json \
  --output deploy-plan.md \
  --force

# If successful, continue with deployment
if [ $? -eq 0 ]; then
    ./deploy.sh deploy-plan.md
fi
```

**Input files:**

**template.md:**
```markdown
---
variables:
  APP_NAME: "Application name"
  ENVIRONMENT:
    description: "Deployment environment"
    default: "staging"
  BRANCH:
    description: "Git branch"
    default: "main"
---

# Deployment Plan: {{APP_NAME}}

Deploying to **{{ENVIRONMENT}}** from branch `{{BRANCH}}`.

Generated at: 2025-10-25
```

**args.json:**
```json
{
  "app_name": "MyWebApp",
  "environment": "production",
  "branch": "release/v2.0"
}
```

**output.md:**
```markdown
# Deployment Plan: MyWebApp

Deploying to **production** from branch `release/v2.0`.

Generated at: 2025-10-25
```

---

## GenerateHeaderCommand Class

### Purpose

Automatically generates YAML frontmatter by extracting all `{{VARIABLE}}` placeholders found in a markdown document. This is useful for creating templates from existing markdown files or when you forget to define variables.

### Command Signature

```
mdtool generate-header <file> [--output <path>]
```

### Arguments

| Argument | Type   | Required | Description                    |
|----------|--------|----------|--------------------------------|
| `<file>` | string | Yes      | Path to markdown file          |

### Options

| Option            | Type   | Required | Description                          |
|-------------------|--------|----------|--------------------------------------|
| `--output <path>` | string | No       | Output file path (default: stdout)   |

### Processing Flow

```
1. Extract variables from document
   ├── Read markdown file
   ├── Find all {{VARIABLE}} patterns using regex
   ├── Support nested paths: {{USER.NAME}}
   ├── Extract unique variable names (deduplicate)
   └── Sort variables alphabetically

2. Generate YAML frontmatter
   ├── Create YAML structure with "variables:" key
   ├── Add each variable with placeholder description
   ├── Format: VARIABLE: "Description for VARIABLE"
   ├── Preserve nested structure (convert dots to nested objects)
   └── Format as valid YAML

3. Output YAML header
   ├── Wrap in YAML delimiters (---)
   ├── Write to stdout (default) or --output file
   └── Ready to copy into template
```

### Placeholder Text Format

Each variable gets a generic placeholder description:

```yaml
VARIABLE_NAME: "Description for VARIABLE_NAME"
```

Users should replace these placeholders with meaningful descriptions.

### Exit Codes

| Code | Meaning                                  |
|------|------------------------------------------|
| 0    | Success - header generated               |
| 1    | Error - file not found, invalid format   |

### Usage Examples

**Example 1: Generate header to stdout**
```bash
mdtool generate-header document.md
```

**Example 2: Save header to file**
```bash
mdtool generate-header document.md --output header.yaml
```

**Example 3: Prepend to existing file**
```bash
mdtool generate-header document.md > temp.yaml
cat temp.yaml document.md > template.md
rm temp.yaml
```

**Example 4: Review variables before creating template**
```bash
# Check what variables are used
mdtool generate-header document.md

# Edit descriptions in editor
mdtool generate-header document.md > header.yaml
vim header.yaml

# Create complete template
cat header.yaml document.md > template.md
```

**Input file (document.md):**
```markdown
# Welcome {{USER}}

Your account {{ACCOUNT_ID}} is active.

Contact: {{USER.EMAIL}}
Region: {{USER.REGION}}
```

**Output:**
```yaml
---
variables:
  ACCOUNT_ID: "Description for ACCOUNT_ID"
  USER: "Description for USER"
  USER.EMAIL: "Description for USER.EMAIL"
  USER.REGION: "Description for USER.REGION"
---
```

**Advanced: Nested object format**

The command can optionally generate nested YAML structure:

```yaml
---
variables:
  ACCOUNT_ID: "Description for ACCOUNT_ID"
  USER: "Description for USER"
  USER.EMAIL: "Description for USER.EMAIL"
  USER.REGION: "Description for USER.REGION"
---
```

Or in nested form (future enhancement):

```yaml
---
variables:
  ACCOUNT_ID: "Description for ACCOUNT_ID"
  USER:
    description: "Description for USER"
    EMAIL: "Description for USER.EMAIL"
    REGION: "Description for USER.REGION"
---
```

---

## Program.cs Integration

### RootCommand Setup

The `Program.cs` file serves as the entry point and sets up the command hierarchy using System.CommandLine's `RootCommand` pattern.

### Command Registration

```csharp
var rootCommand = new RootCommand("MDTool - Markdown processing with variable substitution")
{
    new GetSchemaCommand(),
    new ValidateCommand(),
    new ProcessCommand(),
    new GenerateHeaderCommand()
};

rootCommand.Description = "Process markdown files with YAML-defined variable substitution";

return await rootCommand.InvokeAsync(args);
```

### Global Error Handling

All unhandled exceptions are caught at the root level:

```csharp
try
{
    return await rootCommand.InvokeAsync(args);
}
catch (Exception ex)
{
    Console.WriteLine(JsonOutput.Failure(new List<ValidationError>
    {
        new ValidationError
        {
            Type = ErrorType.UnhandledException,
            Description = ex.Message
        }
    }));
    return 1;
}
```

### Exit Code Return

The application returns the exit code from the invoked command:
- `0` = Success
- `1` = Error (validation failed, file not found, etc.)

### Complete Program.cs Structure

```
Program.cs
├── Main method (entry point)
├── RootCommand setup
│   ├── Add GetSchemaCommand
│   ├── Add ValidateCommand
│   ├── Add ProcessCommand
│   └── Add GenerateHeaderCommand
├── Global exception handler
└── Return exit code
```

---

## Command Patterns

### Shared Validation Logic

All commands that parse markdown files should use consistent validation:

```csharp
protected async Task<Result<MarkdownDocument>> ParseMarkdownFile(string filePath)
{
    // Validate file exists
    if (!File.Exists(filePath))
    {
        return Result<MarkdownDocument>.Failure(
            ValidationErrorType.FileNotFound,
            $"File not found: {filePath}"
        );
    }

    // Check file size limit (10MB)
    var fileInfo = new FileInfo(filePath);
    if (fileInfo.Length > 10 * 1024 * 1024)
    {
        return Result<MarkdownDocument>.Failure(
            ValidationErrorType.FileTooLarge,
            $"File exceeds 10MB limit: {filePath}"
        );
    }

    // Read and parse
    var content = await File.ReadAllTextAsync(filePath);
    return _markdownParser.Parse(content);
}
```

### Error Handling Consistency

All commands use the same error output format:

```csharp
protected int HandleError(Result result)
{
    Console.WriteLine(JsonOutput.Failure(result.Errors));
    return 1;
}

protected int HandleSuccess(object resultData)
{
    Console.WriteLine(JsonOutput.Success(resultData));
    return 0;
}
```

### Dependency Injection of Core Classes

Commands receive Core classes through constructor injection:

```csharp
public class ProcessCommand : Command
{
    private readonly MarkdownParser _parser;
    private readonly VariableSubstitutor _substitutor;
    private readonly FileHelper _fileHelper;

    public ProcessCommand(
        MarkdownParser parser,
        VariableSubstitutor substitutor,
        FileHelper fileHelper)
        : base("process", "Process markdown with variable substitution")
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _substitutor = substitutor ?? throw new ArgumentNullException(nameof(substitutor));
        _fileHelper = fileHelper ?? throw new ArgumentNullException(nameof(fileHelper));

        // Setup arguments and options
        // ...
    }
}
```

### Async Handler Pattern

All command handlers use async/await for I/O operations:

```csharp
this.SetHandler(async (file, args, output, force) =>
{
    try
    {
        var result = await ProcessAsync(file, args, output, force);
        return result.Success ? 0 : 1;
    }
    catch (Exception ex)
    {
        return HandleException(ex);
    }
},
fileArgument, argsOption, outputOption, forceOption);
```

### Common Patterns Summary

| Pattern                  | Purpose                                    |
|--------------------------|---------------------------------------------|
| Result<T>                | Structured error handling without exceptions|
| JsonOutput helper        | Consistent JSON formatting                  |
| Constructor injection    | Testable, loosely coupled design            |
| Async/await              | Non-blocking I/O operations                 |
| Validation early         | Fail fast with clear error messages         |
| Exit codes               | Shell scripting compatibility               |

---

## Code Examples

### Complete GetSchemaCommand Implementation

```csharp
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using MDTool.Core;
using MDTool.Utilities;

namespace MDTool.Commands
{
    /// <summary>
    /// Command to extract JSON schema from markdown template.
    /// </summary>
    public class GetSchemaCommand : Command
    {
        private readonly MarkdownParser _parser;
        private readonly SchemaGenerator _schemaGenerator;
        private readonly FileHelper _fileHelper;

        public GetSchemaCommand()
            : this(new MarkdownParser(), new SchemaGenerator(), new FileHelper())
        {
        }

        public GetSchemaCommand(
            MarkdownParser parser,
            SchemaGenerator schemaGenerator,
            FileHelper fileHelper)
            : base("get-schema", "Extract variable schema from markdown file")
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _schemaGenerator = schemaGenerator ?? throw new ArgumentNullException(nameof(schemaGenerator));
            _fileHelper = fileHelper ?? throw new ArgumentNullException(nameof(fileHelper));

            // Define arguments
            var fileArgument = new Argument<string>(
                name: "file",
                description: "Path to markdown template file");

            // Define options
            var outputOption = new Option<string?>(
                name: "--output",
                description: "Output file path (default: stdout)");
            outputOption.AddAlias("-o");

            // Add to command
            AddArgument(fileArgument);
            AddOption(outputOption);

            // Set handler
            this.SetHandler(async (file, output) =>
            {
                return await ExecuteAsync(file, output);
            },
            fileArgument, outputOption);
        }

        private async Task<int> ExecuteAsync(string filePath, string? outputPath)
        {
            try
            {
                // Step 1: Parse markdown file
                var parseResult = await ParseMarkdownFileAsync(filePath);
                if (!parseResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(parseResult.Errors));
                    return 1;
                }

                var document = parseResult.Value;

                // Step 2: Generate JSON schema
                var schemaResult = _schemaGenerator.GenerateSchema(document.Variables);
                if (!schemaResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(schemaResult.Errors));
                    return 1;
                }

                var schemaJson = schemaResult.Value;

                // Step 3: Output schema
                if (string.IsNullOrEmpty(outputPath))
                {
                    // Output to stdout
                    Console.WriteLine(schemaJson);
                }
                else
                {
                    // Write to file
                    var writeResult = await _fileHelper.WriteFileAsync(outputPath, schemaJson);
                    if (!writeResult.Success)
                    {
                        Console.WriteLine(JsonOutput.Failure(writeResult.Errors));
                        return 1;
                    }

                    // Success message
                    Console.WriteLine(JsonOutput.Success(new
                    {
                        message = $"Schema written to {outputPath}",
                        file = outputPath
                    }));
                }

                return 0;
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        private async Task<Result<MarkdownDocument>> ParseMarkdownFileAsync(string filePath)
        {
            // Validate file exists
            if (!File.Exists(filePath))
            {
                return Result<MarkdownDocument>.Failure(
                    ValidationErrorType.FileNotFound,
                    $"File not found: {filePath}");
            }

            // Validate file size
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 10 * 1024 * 1024) // 10MB limit
            {
                return Result<MarkdownDocument>.Failure(
                    ValidationErrorType.FileTooLarge,
                    $"File exceeds 10MB limit: {filePath}");
            }

            // Read and parse
            var content = await File.ReadAllTextAsync(filePath);
            return _parser.Parse(content);
        }

        private int HandleException(Exception ex)
        {
            var error = new ValidationError
            {
                Type = ValidationErrorType.UnhandledException,
                Description = $"Unexpected error: {ex.Message}",
                StackTrace = ex.StackTrace
            };

            Console.WriteLine(JsonOutput.Failure(new[] { error }));
            return 1;
        }
    }
}
```

### Complete ValidateCommand Implementation

```csharp
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MDTool.Core;
using MDTool.Utilities;

namespace MDTool.Commands
{
    /// <summary>
    /// Command to validate JSON arguments against markdown schema.
    /// </summary>
    public class ValidateCommand : Command
    {
        private readonly MarkdownParser _parser;
        private readonly VariableSubstitutor _substitutor;
        private readonly FileHelper _fileHelper;

        public ValidateCommand()
            : this(new MarkdownParser(), new VariableSubstitutor(), new FileHelper())
        {
        }

        public ValidateCommand(
            MarkdownParser parser,
            VariableSubstitutor substitutor,
            FileHelper fileHelper)
            : base("validate", "Validate JSON arguments against markdown schema")
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _substitutor = substitutor ?? throw new ArgumentNullException(nameof(substitutor));
            _fileHelper = fileHelper ?? throw new ArgumentNullException(nameof(fileHelper));

            // Define arguments
            var fileArgument = new Argument<string>(
                name: "file",
                description: "Path to markdown template file");

            // Define options
            var argsOption = new Option<string>(
                name: "--args",
                description: "Path to JSON arguments file");
            argsOption.AddAlias("-a");
            argsOption.IsRequired = true;

            // Add to command
            AddArgument(fileArgument);
            AddOption(argsOption);

            // Set handler
            this.SetHandler(async (file, args) =>
            {
                return await ExecuteAsync(file, args);
            },
            fileArgument, argsOption);
        }

        private async Task<int> ExecuteAsync(string filePath, string argsPath)
        {
            try
            {
                // Step 1: Parse markdown file
                var parseResult = await ParseMarkdownFileAsync(filePath);
                if (!parseResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(parseResult.Errors));
                    return 1;
                }

                var document = parseResult.Value;

                // Step 2: Load JSON arguments
                var argsResult = await LoadJsonArgsAsync(argsPath);
                if (!argsResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(argsResult.Errors));
                    return 1;
                }

                var args = argsResult.Value;

                // Step 3: Validate arguments
                var validationResult = _substitutor.Validate(document.Variables, args);

                // Step 4: Output validation result
                if (validationResult.Success)
                {
                    Console.WriteLine(JsonOutput.Success(new
                    {
                        success = true,
                        provided = validationResult.ProvidedVariables,
                        missing = validationResult.MissingVariables
                    }));
                    return 0;
                }
                else
                {
                    Console.WriteLine(JsonOutput.Failure(
                        validationResult.Errors,
                        validationResult.ProvidedVariables,
                        validationResult.MissingVariables));
                    return 1;
                }
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        private async Task<Result<MarkdownDocument>> ParseMarkdownFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return Result<MarkdownDocument>.Failure(
                    ValidationErrorType.FileNotFound,
                    $"File not found: {filePath}");
            }

            var content = await File.ReadAllTextAsync(filePath);
            return _parser.Parse(content);
        }

        private async Task<Result<JsonDocument>> LoadJsonArgsAsync(string argsPath)
        {
            if (!File.Exists(argsPath))
            {
                return Result<JsonDocument>.Failure(
                    ValidationErrorType.FileNotFound,
                    $"Args file not found: {argsPath}");
            }

            try
            {
                var json = await File.ReadAllTextAsync(argsPath);
                var jsonDoc = JsonDocument.Parse(json);
                return Result<JsonDocument>.Success(jsonDoc);
            }
            catch (JsonException ex)
            {
                return Result<JsonDocument>.Failure(
                    ValidationErrorType.InvalidJsonArgs,
                    $"Invalid JSON in args file: {ex.Message}");
            }
        }

        private int HandleException(Exception ex)
        {
            var error = new ValidationError
            {
                Type = ValidationErrorType.UnhandledException,
                Description = $"Unexpected error: {ex.Message}",
                StackTrace = ex.StackTrace
            };

            Console.WriteLine(JsonOutput.Failure(new[] { error }));
            return 1;
        }
    }
}
```

### Complete ProcessCommand Implementation

```csharp
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MDTool.Core;
using MDTool.Utilities;

namespace MDTool.Commands
{
    /// <summary>
    /// Command to process markdown with variable substitution.
    /// </summary>
    public class ProcessCommand : Command
    {
        private readonly MarkdownParser _parser;
        private readonly VariableSubstitutor _substitutor;
        private readonly FileHelper _fileHelper;

        public ProcessCommand()
            : this(new MarkdownParser(), new VariableSubstitutor(), new FileHelper())
        {
        }

        public ProcessCommand(
            MarkdownParser parser,
            VariableSubstitutor substitutor,
            FileHelper fileHelper)
            : base("process", "Process markdown with variable substitution")
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _substitutor = substitutor ?? throw new ArgumentNullException(nameof(substitutor));
            _fileHelper = fileHelper ?? throw new ArgumentNullException(nameof(fileHelper));

            // Define arguments
            var fileArgument = new Argument<string>(
                name: "file",
                description: "Path to markdown template file");

            // Define options
            var argsOption = new Option<string>(
                name: "--args",
                description: "Path to JSON arguments file");
            argsOption.AddAlias("-a");
            argsOption.IsRequired = true;

            var outputOption = new Option<string?>(
                name: "--output",
                description: "Output file path (default: stdout)");
            outputOption.AddAlias("-o");

            var forceOption = new Option<bool>(
                name: "--force",
                description: "Overwrite existing output file without prompt");
            forceOption.AddAlias("-f");

            // Add to command
            AddArgument(fileArgument);
            AddOption(argsOption);
            AddOption(outputOption);
            AddOption(forceOption);

            // Set handler
            this.SetHandler(async (file, args, output, force) =>
            {
                return await ExecuteAsync(file, args, output, force);
            },
            fileArgument, argsOption, outputOption, forceOption);
        }

        private async Task<int> ExecuteAsync(
            string filePath,
            string argsPath,
            string? outputPath,
            bool forceOverwrite)
        {
            try
            {
                // Step 1: Validate - Parse markdown
                var parseResult = await ParseMarkdownFileAsync(filePath);
                if (!parseResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(parseResult.Errors));
                    return 1;
                }

                var document = parseResult.Value;

                // Step 2: Validate - Load args
                var argsResult = await LoadJsonArgsAsync(argsPath);
                if (!argsResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(argsResult.Errors));
                    return 1;
                }

                var args = argsResult.Value;

                // Step 3: Validate - Check all required variables
                var validationResult = _substitutor.Validate(document.Variables, args);
                if (!validationResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(
                        validationResult.Errors,
                        validationResult.ProvidedVariables,
                        validationResult.MissingVariables));
                    return 1;
                }

                // Step 4: Perform substitution
                var substitutionResult = _substitutor.Substitute(
                    document.Content,
                    document.Variables,
                    args);

                if (!substitutionResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(substitutionResult.Errors));
                    return 1;
                }

                var processedContent = substitutionResult.Value;

                // Step 5: Check file overwrite protection
                if (!string.IsNullOrEmpty(outputPath))
                {
                    if (File.Exists(outputPath) && !forceOverwrite)
                    {
                        var error = new ValidationError
                        {
                            Type = ValidationErrorType.FileExists,
                            Description = $"File '{outputPath}' already exists. Use --force to overwrite."
                        };

                        Console.WriteLine(JsonOutput.Failure(new[] { error }));
                        return 1;
                    }
                }

                // Step 6: Output processed content
                if (string.IsNullOrEmpty(outputPath))
                {
                    // Output to stdout
                    Console.WriteLine(processedContent);
                }
                else
                {
                    // Write to file
                    var writeResult = await _fileHelper.WriteFileAsync(
                        outputPath,
                        processedContent,
                        forceOverwrite);

                    if (!writeResult.Success)
                    {
                        Console.WriteLine(JsonOutput.Failure(writeResult.Errors));
                        return 1;
                    }

                    // Success message to stderr (so stdout is clean)
                    Console.Error.WriteLine($"Successfully processed to: {outputPath}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        private async Task<Result<MarkdownDocument>> ParseMarkdownFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return Result<MarkdownDocument>.Failure(
                    ValidationErrorType.FileNotFound,
                    $"File not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 10 * 1024 * 1024)
            {
                return Result<MarkdownDocument>.Failure(
                    ValidationErrorType.FileTooLarge,
                    $"File exceeds 10MB limit: {filePath}");
            }

            var content = await File.ReadAllTextAsync(filePath);
            return _parser.Parse(content);
        }

        private async Task<Result<JsonDocument>> LoadJsonArgsAsync(string argsPath)
        {
            if (!File.Exists(argsPath))
            {
                return Result<JsonDocument>.Failure(
                    ValidationErrorType.FileNotFound,
                    $"Args file not found: {argsPath}");
            }

            try
            {
                var json = await File.ReadAllTextAsync(argsPath);
                var jsonDoc = JsonDocument.Parse(json);
                return Result<JsonDocument>.Success(jsonDoc);
            }
            catch (JsonException ex)
            {
                return Result<JsonDocument>.Failure(
                    ValidationErrorType.InvalidJsonArgs,
                    $"Invalid JSON in args file: {ex.Message}");
            }
        }

        private int HandleException(Exception ex)
        {
            var error = new ValidationError
            {
                Type = ValidationErrorType.UnhandledException,
                Description = $"Unexpected error: {ex.Message}",
                StackTrace = ex.StackTrace
            };

            Console.WriteLine(JsonOutput.Failure(new[] { error }));
            return 1;
        }
    }
}
```

### Complete GenerateHeaderCommand Implementation

```csharp
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MDTool.Core;
using MDTool.Utilities;

namespace MDTool.Commands
{
    /// <summary>
    /// Command to auto-generate YAML header from variables in document.
    /// </summary>
    public class GenerateHeaderCommand : Command
    {
        private readonly VariableExtractor _extractor;
        private readonly FileHelper _fileHelper;

        public GenerateHeaderCommand()
            : this(new VariableExtractor(), new FileHelper())
        {
        }

        public GenerateHeaderCommand(
            VariableExtractor extractor,
            FileHelper fileHelper)
            : base("generate-header", "Auto-generate YAML header from document variables")
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _fileHelper = fileHelper ?? throw new ArgumentNullException(nameof(fileHelper));

            // Define arguments
            var fileArgument = new Argument<string>(
                name: "file",
                description: "Path to markdown file");

            // Define options
            var outputOption = new Option<string?>(
                name: "--output",
                description: "Output file path (default: stdout)");
            outputOption.AddAlias("-o");

            // Add to command
            AddArgument(fileArgument);
            AddOption(outputOption);

            // Set handler
            this.SetHandler(async (file, output) =>
            {
                return await ExecuteAsync(file, output);
            },
            fileArgument, outputOption);
        }

        private async Task<int> ExecuteAsync(string filePath, string? outputPath)
        {
            try
            {
                // Step 1: Read markdown file
                if (!File.Exists(filePath))
                {
                    var error = new ValidationError
                    {
                        Type = ValidationErrorType.FileNotFound,
                        Description = $"File not found: {filePath}"
                    };
                    Console.WriteLine(JsonOutput.Failure(new[] { error }));
                    return 1;
                }

                var content = await File.ReadAllTextAsync(filePath);

                // Step 2: Extract all variables
                var extractResult = _extractor.Extract(content);
                if (!extractResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(extractResult.Errors));
                    return 1;
                }

                var variables = extractResult.Value;

                // Step 3: Generate YAML header
                var yamlHeader = GenerateYamlHeader(variables);

                // Step 4: Output header
                if (string.IsNullOrEmpty(outputPath))
                {
                    // Output to stdout
                    Console.WriteLine(yamlHeader);
                }
                else
                {
                    // Write to file
                    var writeResult = await _fileHelper.WriteFileAsync(outputPath, yamlHeader);
                    if (!writeResult.Success)
                    {
                        Console.WriteLine(JsonOutput.Failure(writeResult.Errors));
                        return 1;
                    }

                    Console.Error.WriteLine($"Header written to: {outputPath}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        private string GenerateYamlHeader(List<string> variables)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine("variables:");

            // Sort variables alphabetically for consistency
            var sortedVars = variables.OrderBy(v => v).ToList();

            foreach (var variable in sortedVars)
            {
                // Generate placeholder description
                var description = $"Description for {variable}";
                sb.AppendLine($"  {variable}: \"{description}\"");
            }

            sb.AppendLine("---");

            return sb.ToString();
        }

        private int HandleException(Exception ex)
        {
            var error = new ValidationError
            {
                Type = ValidationErrorType.UnhandledException,
                Description = $"Unexpected error: {ex.Message}",
                StackTrace = ex.StackTrace
            };

            Console.WriteLine(JsonOutput.Failure(new[] { error }));
            return 1;
        }
    }
}
```

### Complete Program.cs Implementation

```csharp
using System;
using System.CommandLine;
using System.Text.Json;
using System.Threading.Tasks;
using MDTool.Commands;

namespace MDTool
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                // Create root command
                var rootCommand = new RootCommand("MDTool - Markdown processing with variable substitution")
                {
                    new GetSchemaCommand(),
                    new ValidateCommand(),
                    new ProcessCommand(),
                    new GenerateHeaderCommand()
                };

                // Set version
                rootCommand.Description = "Process markdown files with YAML-defined variable substitution";

                // Add global options (future)
                // var verboseOption = new Option<bool>("--verbose", "Enable verbose output");
                // rootCommand.AddGlobalOption(verboseOption);

                // Invoke command
                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                // Global error handler for unhandled exceptions
                var error = new
                {
                    success = false,
                    errors = new[]
                    {
                        new
                        {
                            type = "UnhandledException",
                            description = ex.Message,
                            stackTrace = ex.StackTrace
                        }
                    }
                };

                var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                Console.WriteLine(json);
                return 1;
            }
        }
    }
}
```

### Example: Dependency Injection Setup (Future Enhancement)

For testability, commands can be registered with a DI container:

```csharp
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Get root command with injected dependencies
        var rootCommand = new RootCommand("MDTool")
        {
            serviceProvider.GetRequiredService<GetSchemaCommand>(),
            serviceProvider.GetRequiredService<ValidateCommand>(),
            serviceProvider.GetRequiredService<ProcessCommand>(),
            serviceProvider.GetRequiredService<GenerateHeaderCommand>()
        };

        return await rootCommand.InvokeAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register Core services
        services.AddSingleton<MarkdownParser>();
        services.AddSingleton<VariableExtractor>();
        services.AddSingleton<VariableSubstitutor>();
        services.AddSingleton<SchemaGenerator>();
        services.AddSingleton<FileHelper>();

        // Register Commands
        services.AddTransient<GetSchemaCommand>();
        services.AddTransient<ValidateCommand>();
        services.AddTransient<ProcessCommand>();
        services.AddTransient<GenerateHeaderCommand>();
    }
}
```

---

## Summary

The Commands namespace provides a clean, maintainable CLI interface for MDTool. Each command is:

- **Independent**: Separate class with single responsibility
- **Testable**: Constructor injection for dependencies
- **Consistent**: Shared patterns for validation and error handling
- **User-friendly**: Clear help text, validation, and error messages
- **Scriptable**: JSON output and exit codes for automation

This design enables easy extension with new commands while maintaining code quality and user experience.

---

**References:**
- Master Checklist: `/Users/randlee/Documents/github/mdtool/docs/master-checklist.md`
- Implementation Plan: `/Users/randlee/Documents/github/mdtool/docs/mdtool-implementation-plan.md`
- System.CommandLine Docs: https://learn.microsoft.com/en-us/dotnet/standard/commandline/

**Last Updated:** 2025-10-25
