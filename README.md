# MDTool

**Markdown processing tool with YAML-defined variable substitution**

MDTool is a .NET command-line tool that enables powerful variable substitution in markdown files using YAML frontmatter definitions. Perfect for generating documentation, deployment plans, configuration files, and more from templates.

## Features

- **Variable Substitution**: Replace `{{VARIABLES}}` in markdown with actual values from JSON
- **YAML Frontmatter**: Define variables, descriptions, and defaults in markdown frontmatter
- **Nested Variables**: Support dot-notation for complex nested structures (`{{USER.EMAIL}}`)
- **Case-Insensitive Matching**: JSON keys can use any casing (camelCase, snake_case, PascalCase)
- **Optional Variables**: Define default values for optional variables
- **Schema Generation**: Extract variable schemas as JSON templates
- **Validation**: Pre-flight validation of JSON arguments
- **Auto-Header Generation**: Generate YAML frontmatter from existing markdown
- **Overwrite Protection**: Prevent accidental file overwrites with `--force` flag
- **Structured Errors**: All errors returned as structured JSON
- **Conditional Sections (v1.1.0+)**: Include/exclude content based on variables (e.g., role-based templates)

## Installation

### Install as Global Tool

```bash
dotnet tool install --global MDTool
```

### Build from Source

```bash
git clone https://github.com/yourusername/mdtool.git
cd mdtool
dotnet build
dotnet pack -c Release -o nupkg
dotnet tool install --global --add-source ./nupkg MDTool
```

### Verify Installation

```bash
mdtool --help
```

## Quick Start

### 1. Create a Template

Create `template.md`:

```markdown
---
variables:
  APP_NAME: "Application name"
  ENVIRONMENT:
    description: "Deployment environment"
    default: "staging"
---

# Deployment Plan: {{APP_NAME}}

Deploying to **{{ENVIRONMENT}}** environment.
```

### 2. Extract Schema

```bash
mdtool get-schema template.md
```

Output:
```json
{
  "appName": "Application name",
  "environment": "staging"
}
```

### 3. Create Arguments File

Create `args.json`:

```json
{
  "appName": "MyWebApp",
  "environment": "production"
}
```

### 4. Process Template

```bash
mdtool process template.md --args args.json
```

Output:
```markdown
# Deployment Plan: MyWebApp

Deploying to **production** environment.
```

## Commands

### get-schema

Extract variable definitions from markdown and generate a JSON schema template.

**Syntax:**
```bash
mdtool get-schema <file> [--output <path>]
```

**Arguments:**
- `<file>` - Path to markdown template file (required)

**Options:**
- `--output, -o` - Output file path (default: stdout)

**Example:**
```bash
mdtool get-schema template.md --output schema.json
```

### validate

Validate that JSON arguments contain all required variables defined in the template.

**Syntax:**
```bash
mdtool validate <file> --args <path>
```

**Arguments:**
- `<file>` - Path to markdown template file (required)

**Options:**
- `--args, -a` - Path to JSON arguments file (required)

**Example:**
```bash
mdtool validate template.md --args args.json
```

**Success Output:**
```json
{
  "success": true,
  "provided": ["APP_NAME", "ENVIRONMENT"],
  "missing": []
}
```

**Failure Output:**
```json
{
  "success": false,
  "errors": [
    {
      "type": "MissingRequiredVariable",
      "variable": "APP_NAME",
      "description": "Application name"
    }
  ],
  "provided": ["ENVIRONMENT"],
  "missing": ["APP_NAME"]
}
```

### process

Perform variable substitution and generate processed markdown output.

**Syntax:**
```bash
mdtool process <file> --args <path> [--output <path>] [--force]
```

**Arguments:**
- `<file>` - Path to markdown template file (required)

**Options:**
- `--args, -a` - Path to JSON arguments file (required)
- `--output, -o` - Output file path (default: stdout)
- `--force, -f` - Overwrite existing output file without error

**Example:**
```bash
mdtool process template.md --args args.json --output result.md
```

**Overwrite Protection:**
```bash
# First run succeeds
mdtool process template.md --args args.json --output result.md

# Second run fails
mdtool process template.md --args args.json --output result.md
# Error: File exists, use --force to overwrite

# Use --force to overwrite
mdtool process template.md --args args.json --output result.md --force
```

### generate-header

Automatically generate YAML frontmatter by extracting all `{{VARIABLES}}` from a markdown document.

**Syntax:**
```bash
mdtool generate-header <file> [--output <path>]
```

**Arguments:**
- `<file>` - Path to markdown file (required)

**Options:**
- `--output, -o` - Output file path (default: stdout)

**Example:**
```bash
mdtool generate-header document.md > header.yaml
```

**Input (document.md):**
```markdown
# Welcome {{USER_NAME}}!

Your account {{ACCOUNT_ID}} is ready.
```

**Output:**
```yaml
---
variables:
  ACCOUNT_ID: "Description for ACCOUNT_ID"
  USER_NAME: "Description for USER_NAME"
---
```

## Variable Format

### Variable Naming Rules

Variables in markdown must follow these rules:

- **UPPERCASE**: All letters must be uppercase (`{{NAME}}`, not `{{name}}`)
- **Alphanumeric + Underscores**: Only letters, numbers, and underscores allowed
- **Dot Notation**: Use dots for nested structures (`{{USER.EMAIL}}`)
- **Start with Letter**: Must start with a letter (not a number or underscore)

**Valid Examples:**
```markdown
{{NAME}}
{{USER_EMAIL}}
{{USER.PROFILE.BIO}}
{{API.KEY.TOKEN}}
{{CONFIG_DB_HOST}}
```

**Invalid Examples:**
```markdown
{{name}}           # lowercase not allowed
{{123_VAR}}        # cannot start with number
{{USER-EMAIL}}     # hyphens not allowed
{{_PRIVATE}}       # cannot start with underscore
```

### Nested Variables

Use dot notation to create hierarchical structures:

**Template:**
```markdown
---
variables:
  USER.NAME: "User's full name"
  USER.EMAIL: "User's email"
  USER.PROFILE.BIO: "User biography"
---

Name: {{USER.NAME}}
Email: {{USER.EMAIL}}
Bio: {{USER.PROFILE.BIO}}
```

**JSON Arguments:**
```json
{
  "user": {
    "name": "John Doe",
    "email": "john@example.com",
    "profile": {
      "bio": "Software engineer"
    }
  }
}
```

## YAML Frontmatter Format

### Simple String Format

For required variables, use simple string format:

```yaml
---
variables:
  NAME: "User's name"
  EMAIL: "User's email address"
---
```

### Object Format

For optional variables or additional metadata, use object format:

```yaml
---
variables:
  NAME: "User's name"
  VERSION:
    description: "Version number"
    required: false
    default: "1.0.0"
  DEBUG_MODE:
    description: "Enable debug mode"
    default: false
---
```

**Properties:**
- `description` - Variable description (required)
- `required` - Whether variable is required (default: true if no default, false if has default)
- `default` - Default value if not provided in JSON arguments

### Nested Variable Definition

Both flat and nested definitions are supported:

**Flat (Recommended for Phase 1):**
```yaml
---
variables:
  USER.NAME: "User name"
  USER.EMAIL: "User email"
---
```

**Nested (Future enhancement):**
```yaml
---
variables:
  USER:
    NAME: "User name"
    EMAIL: "User email"
---
```

## JSON Arguments Format

### Case-Insensitive Matching

JSON keys are matched case-insensitively to variable names:

**Variable:** `{{USER_NAME}}`

**All of these work:**
```json
{"user_name": "value"}   // snake_case
{"userName": "value"}    // camelCase
{"UserName": "value"}    // PascalCase
{"USER_NAME": "value"}   // UPPER_SNAKE_CASE
```

### Nested Objects

Use nested JSON objects for dot-notation variables:

**Variables:** `{{USER.NAME}}`, `{{USER.EMAIL}}`

**JSON:**
```json
{
  "user": {
    "name": "John Doe",
    "email": "john@example.com"
  }
}
```

### Optional Variables

Optional variables (with defaults) can be omitted from JSON:

**Template:**
```yaml
---
variables:
  NAME: "Name"
  VERSION:
    description: "Version"
    default: "1.0.0"
---
```

**Minimal JSON (uses default for VERSION):**
```json
{
  "name": "MyApp"
}
```

**Full JSON (overrides default):**
```json
{
  "name": "MyApp",
  "version": "2.0.0"
}
```

## Conditional Sections (v1.1.0+)

As of v1.1.0, MDTool supports conditional sections that allow a single template to target multiple roles or scenarios. This is perfect for QA agent roles (TEST vs REPORT), environment-specific content (DEV vs PROD), or any scenario where content should vary based on variables.

### Overview

Conditional sections use familiar syntax to include or exclude content based on boolean expressions:

```markdown
{{#if ROLE == 'TEST'}}
  Content shown only when ROLE is TEST
{{else if ROLE == 'REPORT'}}
  Content shown only when ROLE is REPORT
{{else}}
  Content shown when neither condition is true
{{/if}}
```

**Key Features:**
- Expressions evaluated against provided variables
- Nested conditionals supported (up to 10 levels)
- Type-aware comparisons (string, number, boolean)
- Built-in functions: contains, startsWith, endsWith, in, exists
- Content-scoped validation (only variables in effective content are required)
- Debug trace output for complex logic

### Syntax

#### Basic Conditional

```markdown
{{#if ENVIRONMENT == 'PROD'}}
This content appears only in production.
{{/if}}
```

#### If-Else

```markdown
{{#if DEBUG == true}}
Debug mode is enabled.
{{else}}
Debug mode is disabled.
{{/if}}
```

#### If-Else If-Else

```markdown
{{#if ROLE == 'TEST'}}
## Running Tests
Execute unit test suite.
{{else if ROLE == 'REPORT'}}
## Generating Report
Gather metrics and create quality report.
{{else}}
## Unknown Role
No action defined for this role.
{{/if}}
```

#### Nested Conditionals

```markdown
{{#if ROLE == 'TEST'}}
## Test Execution

{{#if ENVIRONMENT == 'DEV'}}
Running in development environment.
{{else}}
Running in {{ENVIRONMENT}} environment.
{{/if}}

{{/if}}
```

### Expression Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `==` | Equality (type-aware) | `ROLE == 'TEST'` |
| `!=` | Inequality (type-aware) | `ROLE != 'REPORT'` |
| `&&` | Logical AND | `ROLE == 'TEST' && DEBUG == true` |
| `\|\|` | Logical OR | `ROLE == 'TEST' \|\| ROLE == 'REPORT'` |
| `!` | Logical NOT | `!DEBUG` |
| `()` | Grouping | `(A \|\| B) && C` |

**Type Awareness:**
- String comparisons: `ROLE == 'TEST'`
- Number comparisons: `PORT == 8080`
- Boolean comparisons: `DEBUG == true`
- Mismatched types: `PORT == '8080'` → false (number != string)

### Functions

#### contains(haystack, needle)

Tests if a string contains a substring.

```markdown
{{#if ROLE.Contains('TEST')}}
Role contains 'TEST'
{{/if}}

{{#if contains(AGENT, 'QA')}}
Agent name contains 'QA'
{{/if}}
```

**Case Behavior:**
- Default: Case-insensitive
- With `--strict-conditions`: Case-sensitive

#### startsWith(text, prefix)

Tests if a string starts with a prefix.

```markdown
{{#if AGENT.StartsWith('QA')}}
Agent name starts with 'QA'
{{/if}}

{{#if startsWith(ROLE, 'TEST')}}
Role starts with 'TEST'
{{/if}}
```

#### endsWith(text, suffix)

Tests if a string ends with a suffix.

```markdown
{{#if ENVIRONMENT.EndsWith('PROD')}}
Production environment detected
{{/if}}
```

#### in(value, array)

Tests if a value is in an array.

```markdown
{{#if in(ROLE, ['TEST', 'REPORT', 'AUDIT'])}}
Role is one of: TEST, REPORT, or AUDIT
{{/if}}

{{#if in(PORT, [8080, 3000, 5000])}}
Port is a standard port
{{/if}}
```

#### exists(VAR)

Tests if a variable is present in arguments.

```markdown
{{#if exists(AGENT)}}
Agent is defined: {{AGENT}}
{{else}}
No agent specified
{{/if}}
```

### CLI Options

Both `process` and `validate` commands support conditional options:

#### --no-conditions

Disable conditional evaluation; treat tags as literal text.

```bash
mdtool process template.md --args args.json --no-conditions
```

**Use Case:** Backward compatibility or generating output for another system that will process conditionals.

#### --strict-conditions

Enable strict mode for conditional evaluation.

```bash
mdtool process template.md --args args.json --strict-conditions
```

**Behavior:**
- Unknown variables cause errors (instead of evaluating to false)
- String comparisons are case-sensitive (instead of case-insensitive)

**Use Case:** Catch typos in variable names, enforce exact string matching.

#### --conditions-trace-out

Write a JSON trace of conditional decisions to a file for debugging.

```bash
mdtool process template.md --args args.json --conditions-trace-out trace.json
```

**Trace Output:**
```json
{
  "blocks": [
    {
      "startLine": 12,
      "endLine": 26,
      "branches": [
        { "kind": "if", "expr": "ROLE == 'TEST'", "taken": false },
        { "kind": "else-if", "expr": "ROLE == 'REPORT'", "taken": true },
        { "kind": "else", "taken": false }
      ]
    }
  ]
}
```

#### --require-all-yaml (validate only)

Require all YAML-declared required variables, even if not in effective content.

```bash
mdtool validate template.md --args args.json --require-all-yaml
```

**Default (Content-Scoped):** Only variables in effective content (kept branches) are required.

**With --require-all-yaml:** All YAML-required variables must be provided.

### Validation Modes

#### Content-Scoped Validation (Default)

Only variables referenced in effective content are required.

**Template:**
```yaml
---
variables:
  ROLE:
    description: "Agent role"
    required: true
  TEST_VAR:
    description: "Test variable"
    required: true
  REPORT_VAR:
    description: "Report variable"
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

**Result:** Success (REPORT_VAR not required because it's in excluded branch)

#### All-YAML Validation Mode

With `--require-all-yaml`, all YAML-required variables must be provided.

**Same template and args as above**

**Result:** Failure (REPORT_VAR is required by YAML even though it's in excluded branch)

### Debugging with Trace Output

Use `--conditions-trace-out` to debug complex conditional logic:

**Template:**
```markdown
{{#if ROLE == 'TEST'}}
Test content
{{else if ROLE == 'REPORT' && exists(AGENT)}}
Report content with agent
{{else if ROLE == 'REPORT'}}
Report content without agent
{{else}}
Unknown role
{{/if}}
```

**Command:**
```bash
mdtool process template.md --args args.json --conditions-trace-out trace.json
```

**Trace shows:**
- Which branch was taken
- Why other branches were skipped
- Expression evaluation results
- Line numbers for each block

### Code Fence Protection

**Important:** Conditional tags inside code fences are **still parsed**. MDTool does not perform language-sensitive parsing.

**Example:**
```markdown
{{#if ROLE == 'TEST'}}
```bash
echo "{{VARIABLE}}"
```
{{/if}}
```

**Behavior:**
- Outer `{{#if}}` is evaluated
- Inner `{{VARIABLE}}` is treated as a variable placeholder
- Code fences do not create "literal zones"

### Complete Example: Role-Based Template

**Template (template.md):**
```markdown
---
variables:
  ROLE:
    description: "Agent role (TEST or REPORT)"
    required: true
  AGENT:
    description: "Agent identifier"
    required: false
    default: "qa-1"
  DEBUG:
    description: "Enable debug output"
    default: false
  ENVIRONMENT:
    description: "Environment (DEV or PROD)"
    default: "DEV"
---

# QA Execution Agent: {{AGENT}}

{{#if ROLE == 'TEST' || ROLE == 'REPORT'}}
## Shared Setup
- Initialize environment: {{ENVIRONMENT}}
- Agent: {{AGENT}}
{{/if}}

{{#if ROLE == 'TEST'}}
## Unit Test Execution
- Run test suite
- Collect coverage metrics
- Report failures

{{#if DEBUG}}
Debug mode enabled: Verbose output active
{{/if}}

{{else if ROLE == 'REPORT'}}
## Quality Report Generation
- Gather metrics
- Analyze trends
- Generate report

{{#if ENVIRONMENT == 'PROD'}}
Publishing report to production dashboard
{{else}}
Generating local report only
{{/if}}

{{else}}
## No-op Mode
No action defined for role: {{ROLE}}
{{/if}}

---
Execution complete.
```

**Args for TEST role (test-args.json):**
```json
{
  "role": "TEST",
  "agent": "qa-test-1",
  "debug": true,
  "environment": "DEV"
}
```

**Args for REPORT role (report-args.json):**
```json
{
  "role": "REPORT",
  "agent": "qa-report-1",
  "debug": false,
  "environment": "PROD"
}
```

**Process TEST scenario:**
```bash
mdtool process template.md --args test-args.json --output test-output.md
```

**Process REPORT scenario:**
```bash
mdtool process template.md --args report-args.json --output report-output.md
```

**Validate both scenarios:**
```bash
mdtool validate template.md --args test-args.json
mdtool validate template.md --args report-args.json
```

See `examples/conditionals.md` for a complete working example.

---

## Error Handling

All errors are returned as structured JSON with clear messages.

### Error Format

```json
{
  "success": false,
  "errors": [
    {
      "type": "ErrorTypeName",
      "variable": "VARIABLE_NAME",
      "description": "Human-readable error message",
      "line": 5
    }
  ]
}
```

### Common Error Types

| Error Type | Description |
|------------|-------------|
| `FileNotFound` | Input file does not exist |
| `FileTooLarge` | File exceeds 10MB size limit |
| `FileExists` | Output file exists and `--force` not specified |
| `InvalidYamlHeader` | YAML frontmatter parsing failed |
| `InvalidJsonArgs` | JSON arguments file is malformed |
| `MissingRequiredVariable` | Required variable not provided in JSON |
| `InvalidVariableName` | Variable name doesn't match naming rules |
| `ConflictingVariableDefinition` | Cannot have both `X` and `X.Y` defined |
| `OptionalVariableWithoutDefault` | Optional variable must have default value |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Error (validation failed, file error, etc.) |

## Examples

See the `examples/` directory for complete working examples:

- **simple-template.md** - Basic variable substitution
- **nested-template.md** - Nested variables with dot notation
- **optional-vars-template.md** - Optional variables with defaults
- **no-frontmatter.md** - Document for header generation

### Example 1: Simple Workflow

```bash
# 1. Create template
cat > template.md << 'EOF'
---
variables:
  NAME: "Project name"
---
# {{NAME}}
EOF

# 2. Get schema
mdtool get-schema template.md

# 3. Create args
echo '{"name": "MyProject"}' > args.json

# 4. Validate
mdtool validate template.md --args args.json

# 5. Process
mdtool process template.md --args args.json
```

### Example 2: CI/CD Pipeline

```bash
#!/bin/bash

# Generate deployment plan
mdtool process deploy-template.md \
  --args production-config.json \
  --output deploy-plan.md \
  --force

# Validate output exists
if [ ! -f deploy-plan.md ]; then
  echo "Failed to generate deployment plan"
  exit 1
fi

# Continue with deployment
./deploy.sh deploy-plan.md
```

### Example 3: Template Creation Workflow

```bash
# Start with plain markdown containing variables
cat > document.md << 'EOF'
# Welcome {{USER}}
Your account {{ACCOUNT_ID}} is ready.
Contact: {{SUPPORT_EMAIL}}
EOF

# Generate YAML header
mdtool generate-header document.md > header.yaml

# Edit descriptions in header.yaml (manually)
vim header.yaml

# Combine into template
cat header.yaml document.md > template.md

# Create args and process
echo '{
  "user": "Alice",
  "accountId": "12345",
  "supportEmail": "support@example.com"
}' > args.json

mdtool process template.md --args args.json --output output.md
```

## Common Use Cases

### 1. Documentation Generation

Generate project documentation from templates:

```markdown
---
variables:
  PROJECT_NAME: "Project name"
  VERSION: "Version number"
  AUTHOR: "Author name"
---

# {{PROJECT_NAME}} Documentation

Version: {{VERSION}}
Author: {{AUTHOR}}

## Installation
...
```

### 2. Configuration Files

Generate environment-specific configuration:

```markdown
---
variables:
  ENV: "Environment name"
  DB.HOST: "Database host"
  DB.PORT: "Database port"
  API.URL: "API endpoint URL"
---

# Configuration - {{ENV}}

Database: {{DB.HOST}}:{{DB.PORT}}
API: {{API.URL}}
```

### 3. Deployment Plans

Create deployment plans with environment details:

```markdown
---
variables:
  APP_NAME: "Application name"
  VERSION: "Version to deploy"
  ENVIRONMENT: "Target environment"
  DEPLOY_DATE: "Deployment date"
---

# Deployment Plan: {{APP_NAME}} v{{VERSION}}

Deploying to {{ENVIRONMENT}} on {{DEPLOY_DATE}}
```

### 4. Report Templates

Generate reports from data:

```markdown
---
variables:
  REPORT_DATE: "Report date"
  USER.NAME: "User name"
  STATS.TOTAL: "Total count"
  STATS.SUCCESS: "Success count"
---

# Report for {{REPORT_DATE}}

User: {{USER.NAME}}
Total: {{STATS.TOTAL}}
Success: {{STATS.SUCCESS}}
```

## Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Create Package

```bash
dotnet pack -c Release -o nupkg
```

### Install Local Package

```bash
dotnet tool install --global --add-source ./nupkg MDTool
```

### Uninstall

```bash
dotnet tool uninstall --global MDTool
```

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Write tests for new functionality
4. Ensure all tests pass (`dotnet test`)
5. Ensure zero build warnings (`dotnet build`)
6. Commit your changes (`git commit -m 'Add amazing feature'`)
7. Push to the branch (`git push origin feature/amazing-feature`)
8. Open a Pull Request

### Code Style

- Follow C# naming conventions
- Add XML documentation to public APIs
- Write unit tests for all new functionality
- Maintain >80% code coverage
- Use async/await patterns for I/O operations

## Roadmap

### Phase 1 - MVP (Complete)
- [x] Variable substitution
- [x] YAML frontmatter parsing
- [x] Schema generation
- [x] Validation
- [x] Nested variables
- [x] Optional variables
- [x] Case-insensitive matching

### Phase 2 - Conditional Sections (Complete v1.1.0)
- [x] Conditional logic: `{{#if}}`, `{{else if}}`, `{{else}}`, `{{/if}}`
- [x] Expression operators: `==`, `!=`, `&&`, `||`, `!`, `()`
- [x] Built-in functions: `contains`, `startsWith`, `endsWith`, `in`, `exists`
- [x] Type-aware comparisons (string, number, boolean)
- [x] Content-scoped validation
- [x] Debug trace output
- [x] Strict mode for error checking

### Phase 3 - Macro System (Planned)
- [ ] Built-in macros: `{{DATE}}`, `{{TIME}}`, `{{DATETIME}}`
- [ ] Environment variable expansion: `{{env:VAR_NAME}}`
- [ ] File expansion: `{{file:path}}` in markdown
- [ ] File references: `"file:path"` in JSON

### Phase 4 - Advanced Features (Planned)
- [ ] Loop support: `{{#each items}}`
- [ ] Built-in functions: `{{UPPER:var}}`, `{{LOWER:var}}`
- [ ] String functions: `{{LEN:var}}`, `{{JOIN:array:,}}`
- [ ] Array filtering and transformations

## License

MIT License - see LICENSE file for details

## Support

- **Issues**: https://github.com/yourusername/mdtool/issues
- **Documentation**: https://github.com/yourusername/mdtool/wiki
- **Examples**: See `examples/` directory

## Version

Current version: **1.0.0**

## Acknowledgments

Built with:
- .NET 8.0
- System.CommandLine
- YamlDotNet
- xUnit

---

**Made with ❤️ for developers who love markdown and automation**
