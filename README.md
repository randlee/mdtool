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

### Phase 1 (Current) - MVP
- [x] Variable substitution
- [x] YAML frontmatter parsing
- [x] Schema generation
- [x] Validation
- [x] Nested variables
- [x] Optional variables
- [x] Case-insensitive matching

### Phase 2 - Macro System
- [ ] Built-in macros: `{{DATE}}`, `{{TIME}}`, `{{DATETIME}}`
- [ ] Environment variable expansion: `{{env:VAR_NAME}}`
- [ ] File expansion: `{{file:path}}` in markdown
- [ ] File references: `"file:path"` in JSON

### Phase 3 - Advanced Features
- [ ] Loop support: `{{FOREACH items}}`
- [ ] Conditional logic: `{{IF condition}}`
- [ ] Built-in functions: `{{UPPER:var}}`, `{{LOWER:var}}`
- [ ] String functions: `{{LEN:var}}`, `{{JOIN:array:,}}`

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
