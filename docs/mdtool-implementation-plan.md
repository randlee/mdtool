# MDTool Implementation Plan

## Project Overview

**mdtool** is a .NET CLI tool for processing markdown files with variable substitution and validation. It enables templating workflows for AI agents (like Claude Code) and automation scripts.

### Core Capabilities
- Parse YAML frontmatter to define markdown schemas
- Extract JSON schema for variable requirements
- Process markdown files with JSON variable substitution
- Auto-generate YAML headers from variables found in documents
- Validate arguments before processing

### Technology Stack
- **.NET 8.0** (LTS)
- **System.CommandLine** for CLI
- **YamlDotNet** for YAML parsing
- **System.Text.Json** for JSON handling
- **NuGet Package** distribution

---

## Phase 1: MVP (Core Functionality)

### Features

#### 1. YAML Frontmatter Parsing
Parse markdown files with YAML headers defining variables:

```yaml
---
variables:
  NAME: "The application name"
  BRANCH: 
    description: "Git branch to deploy"
    required: false
    default: "main"
  PORT:
    description: "Server port number"
    default: 8080
---
```

**Rules:**
- Simple string value = description (required=true by default)
- Object form allows `required: false` and `default` value
- Optional variables MUST have defaults
- Variable names must be ALL CAPS
- Type is implied from default value

#### 2. Variable Substitution
- Variables in markdown: `{{NAME}}`, `{{BRANCH}}`, `{{USER.NAME}}`
- Variables must be ALL CAPS in markdown
- JSON property names are case-insensitive (typically lowercase)
- Supports nested objects via dot notation: `{{USER.NAME}}` → `{"user": {"name": "John"}}`

#### 3. CLI Commands

```bash
# Get JSON schema for required variables
mdtool get-schema <file.md> [--output <path>]

# Validate args.json against schema without processing
mdtool validate <file.md> --args <args.json>

# Process markdown with variable substitution
mdtool process <file.md> --args <args.json> [--output <path>]

# Auto-generate YAML header from {{VARIABLES}} in document
mdtool generate-header <file.md> [--output <path>]
```

#### 4. JSON Output Format

**Success response:**
```json
{
  "success": true,
  "result": "...processed content or schema..."
}
```

**Error response:**
```json
{
  "success": false,
  "errors": [
    {
      "type": "MissingRequiredVariable",
      "variable": "NAME",
      "description": "The application name",
      "line": 15
    }
  ],
  "provided": ["BRANCH", "PORT"],
  "missing": ["NAME"]
}
```

**Error Types:**
- `MissingRequiredVariable` - Required variable not provided
- `InvalidYamlHeader` - YAML frontmatter parsing failed
- `InvalidJsonArgs` - Args file is not valid JSON
- `FileNotFound` - Input file doesn't exist
- `InvalidVariableFormat` - Variable format in markdown is malformed

---

## Architecture

### Project Structure

```
mdtool/
├── src/
│   └── MDTool/
│       ├── MDTool.csproj
│       ├── Program.cs                    # Entry point
│       ├── Commands/
│       │   ├── GetSchemaCommand.cs
│       │   ├── ValidateCommand.cs
│       │   ├── ProcessCommand.cs
│       │   └── GenerateHeaderCommand.cs
│       ├── Core/
│       │   ├── MarkdownParser.cs         # Parse MD + YAML
│       │   ├── VariableExtractor.cs      # Find {{VARS}}
│       │   ├── VariableSubstitutor.cs    # Replace {{VARS}}
│       │   └── SchemaGenerator.cs        # Generate JSON schema
│       ├── Models/
│       │   ├── MarkdownDocument.cs
│       │   ├── VariableDefinition.cs
│       │   ├── ValidationResult.cs
│       │   └── ProcessingResult.cs
│       └── Utilities/
│           ├── JsonOutput.cs             # Standardized JSON output
│           └── FileHelper.cs
├── tests/
│   └── MDTool.Tests/
│       ├── MDTool.Tests.csproj
│       ├── ParserTests.cs
│       ├── SubstitutionTests.cs
│       └── CommandTests.cs
└── README.md
```

### Key Classes

#### MarkdownDocument
```csharp
public class MarkdownDocument
{
    public Dictionary<string, VariableDefinition> Variables { get; set; }
    public string Content { get; set; }
    public string RawYaml { get; set; }
}
```

#### VariableDefinition
```csharp
public class VariableDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Required { get; set; } = true;
    public object? DefaultValue { get; set; }
}
```

#### ValidationResult
```csharp
public class ValidationResult
{
    public bool Success { get; set; }
    public List<ValidationError> Errors { get; set; }
    public List<string> ProvidedVariables { get; set; }
    public List<string> MissingVariables { get; set; }
}
```

---

## Phase 1 Implementation Steps

### Step 1: Project Setup
1. Create .NET 8.0 console application
2. Add NuGet dependencies:
   - `System.CommandLine` (2.0.0-beta4 or later)
   - `YamlDotNet` (latest)
3. Configure as global tool in `.csproj`:
```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net8.0</TargetFramework>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>mdtool</ToolCommandName>
  <PackageId>MDTool</PackageId>
  <Version>1.0.0</Version>
  <Authors>Your Name</Authors>
  <Description>Markdown processing tool with variable substitution</Description>
</PropertyGroup>
```

### Step 2: YAML Parsing (MarkdownParser.cs)
- Split markdown into frontmatter and content
- Parse YAML using YamlDotNet
- Support both simple string and object forms for variables
- Validate that optional variables have defaults
- Handle missing frontmatter gracefully

### Step 3: Variable Extraction (VariableExtractor.cs)
- Use regex to find all `{{VARIABLE_NAME}}` patterns
- Support nested paths: `{{USER.NAME}}`
- Extract unique variable names (case-insensitive)
- Track line numbers for error reporting

**Regex pattern:** `\{\{([A-Z_][A-Z0-9_]*(?:\.[A-Z_][A-Z0-9_]*)*)\}\}`

### Step 4: Schema Generation (SchemaGenerator.cs)
- Convert VariableDefinition dictionary to JSON schema
- Use description as placeholder value
- Include all required and optional variables
- Support nested object structure

**Example output:**
```json
{
  "name": "The application name",
  "branch": "Git branch to deploy",
  "user": {
    "name": "User's full name"
  }
}
```

### Step 5: Variable Substitution (VariableSubstitutor.cs)
- Accept JSON arguments (case-insensitive matching)
- Navigate nested objects via dot notation
- Replace `{{VAR}}` with actual values
- Handle missing optional variables (use default)
- Detect missing required variables

### Step 6: Command Implementation

#### GetSchemaCommand
```csharp
public class GetSchemaCommand : Command
{
    // Parse markdown
    // Generate schema from variables
    // Output JSON to stdout or file
}
```

#### ValidateCommand
```csharp
public class ValidateCommand : Command
{
    // Parse markdown
    // Load args JSON
    // Check all required variables present
    // Output validation result
}
```

#### ProcessCommand
```csharp
public class ProcessCommand : Command
{
    // Validate first
    // Perform substitution
    // Output processed markdown
}
```

#### GenerateHeaderCommand
```csharp
public class GenerateHeaderCommand : Command
{
    // Extract all {{VARIABLES}} from document
    // Generate YAML frontmatter
    // Optionally prepend to file or output separately
}
```

### Step 7: JSON Output Standardization
- Create helper methods for consistent JSON responses
- Always include `success` boolean
- Include appropriate error details
- Pretty-print JSON output

### Step 8: Error Handling
- Catch all exceptions at command level
- Convert to structured JSON errors
- Provide helpful error messages
- Include context (line numbers, variable names)

### Step 9: Testing
- Unit tests for parser
- Unit tests for substitution logic
- Integration tests for each command
- Test error scenarios
- Test edge cases (empty files, missing headers, nested variables)

### Step 10: Documentation
- README with installation instructions
- Usage examples for each command
- Sample markdown files
- Sample JSON args files

---

## Phase 2: Macro System

### Features to Add

#### 1. Built-in System Macros
```markdown
{{DATE}}      → 2025-10-25
{{TIME}}      → 14:30:45
{{DATETIME}}  → 2025-10-25T14:30:45Z
```

Implementation:
- Expand before variable substitution
- Use ISO 8601 format
- UTC timezone by default

#### 2. File Expansion
Support `file:` prefix in JSON:

```json
{
  "prompt": "file:templates/continue.md",
  "header": "file:header.md"
}
```

Then `{{PROMPT}}` expands to file contents.

Implementation:
- Detect `file:` prefix during arg loading
- Read file contents (relative to args.json location)
- Replace value with contents
- Process recursively (expanded files can have variables)

#### 3. EXPAND() Function
```markdown
{{EXPAND(PATH)}}
```

Where `{"path": "templates/continue.md"}`

Implementation:
- Parse function syntax during substitution
- Load file from path variable
- Replace with contents
- Process nested variables

---

## Phase 3: Advanced Features

### Loop Support
```markdown
{{FOREACH FILES}}
- Process file: {{FILE.NAME}} at {{FILE.PATH}}
{{END}}
```

JSON:
```json
{
  "files": [
    {"name": "app.cs", "path": "/src"},
    {"name": "test.cs", "path": "/tests"}
  ]
}
```

### Conditional Logic

Testability notes:
- ConditionalEvaluator lives in Core; no CLI dependency
- Provide ArgsJsonAccessor; EvaluateDetailed for unit tests; --conditions-trace-out for integration tests

Updated syntax and evaluation:

```markdown
{{#if DEBUG}}
Debug mode enabled
{{/if}}

{{#if ROLE == 'TEST' || ROLE == 'REPORT'}}
Shared setup
{{else if ROLE.Contains('REPORT') || AGENT.StartsWith('QA')}}
Report-specific content
{{else}}
Other
{{/if}}
```

Processing order:
1) Args + defaults → 2) Evaluate conditionals (prune content) → 3) Substitute variables

Flags:
- --no-conditions (skip evaluation)
- --strict-conditions (unknown vars error; case-sensitive)
- --require-all-yaml (Validate: require all YAML-required vars)

### Additional Built-in Functions
- `{{UPPER:name}}` - Uppercase transformation
- `{{LOWER:name}}` - Lowercase transformation
- `{{LEN:collection}}` - Length/count
- `{{JOIN:collection:,}}` - Join with delimiter

---

## Example Use Cases

### 1. Template for Deployment Script

**deploy-template.md:**
```markdown
---
variables:
  APP_NAME: "Application to deploy"
  ENVIRONMENT:
    description: "Deployment environment"
    default: "staging"
  BRANCH:
    description: "Git branch"
    default: "main"
---

# Deployment Plan: {{APP_NAME}}

Deploying to **{{ENVIRONMENT}}** from branch `{{BRANCH}}`.

## Steps
1. Pull latest from {{BRANCH}}
2. Build {{APP_NAME}}
3. Deploy to {{ENVIRONMENT}}
```

**Commands:**
```bash
# Get schema
mdtool get-schema deploy-template.md > schema.json

# Edit schema.json with actual values
# {"app_name": "MyApp", "environment": "production"}

# Process
mdtool process deploy-template.md --args values.json --output deploy-plan.md
```

### 2. AI Prompt Template

**code-review.md:**
```markdown
---
variables:
  CODE_FILE: "File to review"
  LANGUAGE: "Programming language"
  FOCUS:
    description: "Review focus area"
    default: "security and performance"
---

Please review the following {{LANGUAGE}} code with focus on {{FOCUS}}:

{{CODE_FILE}}

Provide:
1. Security concerns
2. Performance issues
3. Best practice violations
```

### 3. Auto-generate Header

**existing.md:**
```markdown
# Welcome {{USER}}

Your account {{ACCOUNT_ID}} is active.
Email: {{EMAIL}}
```

**Command:**
```bash
mdtool generate-header existing.md
```

**Output:**
```yaml
---
variables:
  USER: "Description for USER"
  ACCOUNT_ID: "Description for ACCOUNT_ID"
  EMAIL: "Description for EMAIL"
---
```

---

## Testing Strategy

### Unit Tests
- YAML parsing (valid/invalid)
- Variable extraction (simple/nested/malformed)
- Substitution logic (all scenarios)
- Schema generation
- Case-insensitive matching

### Integration Tests
- End-to-end command execution
- File I/O operations
- Error handling flows
- JSON output format validation

### Test Data
Create `test-fixtures/` directory with:
- Valid markdown templates
- Invalid YAML samples
- Various JSON args files
- Edge cases (empty files, no variables, etc.)

---

## Packaging & Distribution

### NuGet Package
```bash
# Pack
dotnet pack -c Release

# Install globally
dotnet tool install -g Rand.MDTool

# Uninstall
dotnet tool uninstall -g Rand.MDTool
```

### Version Strategy
- 1.0.0 - Phase 1 (MVP)
- 1.1.0 - Phase 2 (Macros)
- 1.2.0 - Phase 3 (Loops)

---

## Success Criteria

### Phase 1 Complete When:
- ✅ All four commands work correctly
- ✅ YAML parsing handles both formats
- ✅ Variable substitution supports nested objects
- ✅ Error handling returns structured JSON
- ✅ Unit tests pass with >80% coverage
- ✅ Package installs as global tool
- ✅ README documents all features

### Phase 2 Complete When:
- ✅ DATE/TIME macros work
- ✅ File expansion via `file:` prefix works
- ✅ EXPAND() function works
- ✅ Recursive processing works correctly

### Phase 3 Complete When:
- ✅ FOREACH loops work
- ✅ Nested loops supported
- ✅ Additional built-in functions implemented

---

## Notes for Implementation

### Design Principles
1. **AI-Friendly**: JSON I/O, predictable errors, clear schemas
2. **Scriptable**: All output to stdout by default, --output for files
3. **Composable**: Each command does one thing well
4. **Extensible**: Plugin architecture for future macros/functions

### Performance Considerations
- Lazy loading for large files
- Streaming for file expansion (Phase 2)
- Limit recursion depth to prevent infinite loops

### Security Considerations
- Validate file paths (no directory traversal)
- Limit file expansion size
- Timeout for complex operations
- Sanitize JSON output

### Edge Cases to Handle
- Files without YAML frontmatter
- Variables with no matching args
- Malformed `{{VARIABLES}}`
- Circular file references (Phase 2)
- Unicode characters in variable names
- Empty files
- Very large files (>10MB)

---

## Getting Started with Claude Code

1. Load this plan into Claude Code
2. Start with: "Please implement Phase 1 of the mdtool project according to the plan"
3. Claude Code will create the project structure and implement features incrementally
4. Test each component as it's built
5. Proceed to Phase 2 after Phase 1 is complete and tested

## Questions to Resolve During Implementation

- Should `generate-header` add descriptions as comments or leave blank?
- Should variable names allow underscores? (Currently: yes)
- Should there be a max nesting depth for dot notation?
- Output encoding (UTF-8 with BOM or without)?
- Should --output overwrite without confirmation?
