# MDTool Master Implementation Checklist

**Project:** MDTool - Markdown processing with variable substitution
**Version:** 1.0.0 (targeting all phases)
**Last Updated:** 2025-10-25

---

## Sprint 1 Progress Summary

**Current Status:** Waves 1 & 2 Complete, Wave 3 Ready to Begin

| Wave | Component | Status | Tests | Coverage | QA |
|------|-----------|--------|-------|----------|-----|
| Wave 1 | Models | ✅ COMPLETE | 157/157 | 99%+ | CONDITIONAL PASS |
| Wave 2A | Utilities | ✅ COMPLETE | 62/62 | >80% | PASS |
| Wave 2B | Core | ✅ COMPLETE | 84/84 | >80% | PASS |
| Wave 2 | Integration | ✅ COMPLETE | 14/14 | N/A | PASS |
| Wave 3 | Commands | 🔄 NEXT | 0 | N/A | PENDING |
| Wave 4 | Integration & Docs | ⏸️ BLOCKED | 0 | N/A | PENDING |

**Total Tests Passing:** 317/317 (100%)
**Build Status:** Zero warnings, zero errors
**Ready for:** Wave 3 Commands implementation

---

## Project Decisions & Conventions

### Variable Naming
- ✅ **Format:** Uppercase snake-case with underscores
  - Markdown: `{{VARIABLE_NAME}}`, `{{USER_EMAIL}}`
  - JSON: Case-insensitive matching (`variable-name`, `variable_name`, `variableName` all work)
  - Regex: `\{\{([A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*)\}\}`
- ✅ **Nesting:** Maximum 5 levels (e.g., `{{USER.PROFILE.ADDRESS.CITY.NAME}}`)
- ✅ **Rationale:** Matches environment variable conventions, universally recognized by developers and AI

### File Operations
- ✅ **Overwrite Protection:** Requires `--force` flag to overwrite existing files
- ✅ **File Expansion Paths:** Relative to CWD (current working directory / git root)
- ✅ **Max File Size:** 10MB per file
- ✅ **Encoding:** UTF-8 without BOM
- ✅ **Recursion Depth:** Maximum 10 levels for file expansion chains

### Error Handling
- ✅ **Pattern:** Result/discriminated union pattern (collect errors)
- ✅ **Exceptions:** Only for file I/O and system failures
- ✅ **Validation:** Collect all errors and report as list
- ✅ **Timeout:** 30 seconds per operation

### Command Behavior
- ✅ **generate-header:** Uses placeholder text (`"Description for VARIABLE_NAME"`)
- ✅ **Output:** Defaults to stdout, `--output` writes to file
- ✅ **Dry Run:** Planned for future enhancement

### Phase 2 Enhancements
- ✅ **Environment Variables:** `{{env:ENV_VAR_NAME}}` expansion support
  - **Precedence:** env var → JSON args → YAML defaults → error
  - Environment variables override JSON args (standard config precedence)
  - Case-sensitive for env vars, case-insensitive for JSON fallback
  - Enables CI/CD overrides without modifying config files
- ✅ **File Expansion:** `file:` prefix in JSON and `{{file:PATH}}` in markdown
  - All paths relative to CWD (git root)
  - Recursive processing with depth limit (10 levels)

---

## Status Legend

- [ ] Not Started
- [🔄] In Progress
- [✅] Completed
- [⚠️] Blocked / Needs Discussion
- [🧪] Ready for Testing
- [📝] Needs Documentation

---

## Phase 1: MVP (Core Functionality)

### Project Setup
- [✅] Create .NET 8.0 console application
  - [✅] Initialize solution structure (`src/MDTool/`, `tests/MDTool.Tests/`)
  - [✅] Configure `.csproj` with global tool settings
  - [✅] Set `PackAsTool=true`, `ToolCommandName=mdtool`
- [✅] Add NuGet dependencies
  - [✅] `System.CommandLine` (2.0.0-beta4 or later)
  - [✅] `YamlDotNet` (latest stable)
  - [✅] `System.Text.Json` (built-in .NET 8)
- [✅] Configure project metadata
  - [✅] Set version to 1.0.0
  - [✅] Add package description
  - [✅] Configure authors and license
- [✅] Setup test project
  - [✅] Create xUnit test project
  - [✅] Add project references
  - [✅] Configure test fixtures directory

### Core Models (Models/) - ✅ COMPLETE (Wave 1)
- [✅] Create `MarkdownDocument.cs`
  - [✅] Properties: `Variables`, `Content`, `RawYaml`
  - [✅] Constructor and initialization
- [✅] Create `VariableDefinition.cs`
  - [✅] Properties: `Name`, `Description`, `Required`, `DefaultValue`
  - [✅] Support for type inference from default value
- [✅] Create `ValidationResult.cs`
  - [✅] Properties: `Success`, `Errors`, `ProvidedVariables`, `MissingVariables`
  - [✅] Helper methods for creating success/failure results
- [✅] Create `ProcessingResult.cs`
  - [✅] Generic Result<T> pattern implementation
  - [✅] Error collection support
- [✅] Create `ValidationError.cs`
  - [✅] Properties: `Type`, `Variable`, `Description`, `Line`
  - [✅] Error type enum: 17 error types (expanded from original 5)
- [✅] Create `Unit.cs` - Void type for ProcessingResult<Unit>
- [✅] **Tests:** 157 tests passing (100% pass rate, 99%+ coverage)
- [✅] **QA Status:** CONDITIONAL PASS (minor naming deviation documented)

### Utilities (Utilities/) - ✅ COMPLETE (Wave 2A)
- [✅] Implement `FileHelper.cs`
  - [✅] Async methods: `ReadFileAsync`, `WriteFileAsync`
  - [✅] `ValidatePath` with strictForMacros parameter
  - [✅] `CheckFileSize` with 10MB limit
  - [✅] UTF-8 encoding without BOM
  - [✅] Directory creation in WriteFileAsync
  - [✅] Overwrite protection (--force flag)
- [✅] Implement `JsonOutput.cs`
  - [✅] Success/Failure methods
  - [✅] lowerCamelCase property naming
  - [✅] Pretty-printed JSON output
- [✅] **Tests:** 62 tests passing (100% pass rate)
- [✅] **QA Status:** PASS

### Core Parsing (Core/) - ✅ COMPLETE (Wave 2B)
- [✅] Implement `MarkdownParser.cs`
  - [✅] Method: `ParseContent(string content)` returns `ProcessingResult<MarkdownDocument>` (content-based, not file-based)
  - [✅] Split markdown into YAML frontmatter and content
  - [✅] Detect frontmatter boundaries (`---`)
  - [✅] Handle files without frontmatter gracefully
  - [✅] Parse YAML using YamlDotNet
  - [✅] Support simple string format: `NAME: "description"`
  - [✅] Support object format: `NAME: { description, required, default }`
  - [✅] Validate: optional variables must have defaults
  - [✅] Validate: variable names follow `^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$` pattern (with dot-notation)
  - [✅] **Conflict detection:** Cannot have both `X` and `X.Y` defined
  - [✅] Collect all parsing errors (don't fail on first)
  - [✅] Return structured Result with errors
- [✅] Implement `VariableExtractor.cs`
  - [✅] Method: `ExtractVariables(string content)` returns list of found variables
  - [✅] Method: `ValidateVariableFormat(string content)` returns `ProcessingResult<Unit>`
  - [✅] Regex: `\{\{([A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*)\}\}`
  - [✅] Find all `{{VARIABLE_NAME}}` patterns
  - [✅] Support nested paths: `{{USER.NAME}}`, `{{API.KEY}}`
  - [✅] Extract unique variable names (deduplicate)
  - [✅] Validate variable format (no malformed syntax)
  - [✅] Handle edge cases: `{{`, `}}` without pairs
- [✅] Implement `SchemaGenerator.cs`
  - [✅] Method: `GenerateSchema(Dictionary<string, VariableDefinition>)` returns JSON string
  - [✅] Convert variables to JSON schema object
  - [✅] **lowerCamelCase conversion:** `USER_NAME` → `userName`
  - [✅] Use description as placeholder value for required vars
  - [✅] Include default values for optional vars
  - [✅] Support nested object structure via dot notation
  - [✅] Example: `USER.NAME` → `{"user": {"name": "..."}}`
  - [✅] Pretty-print JSON output
- [✅] Implement `VariableSubstitutor.cs`
  - [✅] Method: `Substitute(content, variables, args)` returns `ProcessingResult<string>`
  - [✅] **Case-insensitive JSON matching:** `userName`, `user_name`, `UserName` all match `USER_NAME`
  - [✅] Parse nested objects via dot notation
  - [✅] Replace `{{VAR}}` with actual values
  - [✅] Handle optional variables (use defaults if not provided)
  - [✅] Detect missing required variables (collect all)
  - [✅] Validate all variables before substitution
  - [✅] Return Result with errors or substituted content
- [✅] **Tests:** 84 tests passing (100% pass rate)
- [✅] **QA Status:** PASS

### Utilities (Utilities/)
- [ ] Implement `JsonOutput.cs`
  - [ ] Method: `Success(object result)` returns JSON string
  - [ ] Method: `Failure(List<Error> errors)` returns JSON string
  - [ ] Format: `{"success": true/false, "result": "..." / "errors": [...]}`
  - [ ] Include `provided` and `missing` arrays for validation errors
  - [ ] Pretty-print JSON
  - [ ] Handle serialization errors gracefully
- [ ] Implement `FileHelper.cs`
  - [ ] Method: `ReadFile(path)` with validation
  - [ ] Method: `WriteFile(path, content, force)` with overwrite protection
  - [ ] Method: `ValidatePath(path)` - prevent directory traversal
  - [ ] Method: `CheckFileSize(path)` - enforce 10MB limit
  - [ ] Method: `ResolvePathFromCwd(path)` - resolve relative paths

### Commands (Commands/)
- [ ] Implement `GetSchemaCommand.cs`
  - [ ] Setup command with System.CommandLine
  - [ ] Required argument: `<file>` (markdown file path)
  - [ ] Optional option: `--output <path>` (output file)
  - [ ] Handler: Parse markdown file
  - [ ] Handler: Generate JSON schema from variables
  - [ ] Handler: Output to stdout or file
  - [ ] Handler: Use JsonOutput for errors
  - [ ] Handler: Return exit code (0=success, 1=error)
- [ ] Implement `ValidateCommand.cs`
  - [ ] Setup command with System.CommandLine
  - [ ] Required argument: `<file>` (markdown file path)
  - [ ] Required option: `--args <path>` (JSON args file)
  - [ ] Handler: Parse markdown file
  - [ ] Handler: Load JSON arguments
  - [ ] Handler: Validate all required variables present
  - [ ] Handler: Collect all validation errors
  - [ ] Handler: Output validation result as JSON
  - [ ] Handler: Return exit code
- [ ] Implement `ProcessCommand.cs`
  - [ ] Setup command with System.CommandLine
  - [ ] Required argument: `<file>` (markdown file path)
  - [ ] Required option: `--args <path>` (JSON args file)
  - [ ] Optional option: `--output <path>` (output file)
  - [ ] Optional option: `--force` (overwrite without prompt)
  - [ ] Handler: Validate first (reuse validation logic)
  - [ ] Handler: Perform variable substitution
  - [ ] Handler: Check file overwrite (if --output specified)
  - [ ] Handler: Output processed markdown
  - [ ] Handler: Use JsonOutput for errors
  - [ ] Handler: Return exit code
- [ ] Implement `GenerateHeaderCommand.cs`
  - [ ] Setup command with System.CommandLine
  - [ ] Required argument: `<file>` (markdown file path)
  - [ ] Optional option: `--output <path>` (output file)
  - [ ] Handler: Extract all `{{VARIABLES}}` from document
  - [ ] Handler: Generate YAML frontmatter structure
  - [ ] Handler: Use placeholder descriptions: `"Description for VARIABLE_NAME"`
  - [ ] Handler: Format as valid YAML
  - [ ] Handler: Output to stdout or file
  - [ ] Handler: Return exit code

### Program Entry Point
- [ ] Implement `Program.cs`
  - [ ] Setup System.CommandLine root command
  - [ ] Register all four commands
  - [ ] Configure command hierarchy
  - [ ] Setup global error handling
  - [ ] Return appropriate exit codes
  - [ ] Add version information

### Phase 1 Testing
- [ ] Parser unit tests
  - [ ] Valid YAML frontmatter parsing
  - [ ] Invalid YAML error handling
  - [ ] Missing frontmatter handling
  - [ ] Simple string variable format
  - [ ] Object variable format
  - [ ] Optional variables with defaults
  - [ ] Variable name validation
- [ ] Variable extraction tests
  - [ ] Simple variables: `{{NAME}}`
  - [ ] Nested variables: `{{USER.NAME}}`
  - [ ] Multiple nesting levels (up to 5)
  - [ ] Malformed variables
  - [ ] Edge cases: unclosed `{{`, empty `{{}}`
  - [ ] Line number tracking
- [ ] Substitution tests
  - [ ] Simple replacement
  - [ ] Nested object navigation
  - [ ] Case-insensitive JSON matching
  - [ ] Optional variables with defaults
  - [ ] Missing required variables
  - [ ] All variables missing (comprehensive error)
- [ ] Schema generation tests
  - [ ] Simple variables to JSON
  - [ ] Nested structure generation
  - [ ] Required vs optional variables
  - [ ] Default value inclusion
  - [ ] JSON format validation
- [ ] Command integration tests
  - [ ] `get-schema` end-to-end
  - [ ] `validate` with valid args
  - [ ] `validate` with missing args
  - [ ] `process` successful substitution
  - [ ] `process` with --output and --force
  - [ ] `generate-header` from document
  - [ ] File not found errors
  - [ ] Invalid JSON args errors
- [ ] Error handling tests
  - [ ] All error types generated correctly
  - [ ] Error collection (multiple errors)
  - [ ] JSON error format validation
  - [ ] Exit codes correct
- [ ] Edge case tests
  - [ ] Empty markdown files
  - [ ] Files without variables
  - [ ] Very large files (up to 10MB)
  - [ ] Unicode in variable names (should fail)
  - [ ] Unicode in content (should work)
  - [ ] Circular logic prevention

### Phase 1 Documentation
- [ ] Create README.md
  - [ ] Project overview
  - [ ] Installation instructions (`dotnet tool install -g MDTool`)
  - [ ] Quick start guide
  - [ ] All four commands documented with examples
  - [ ] Variable naming conventions
  - [ ] YAML frontmatter format
  - [ ] JSON args format
  - [ ] Error handling examples
- [ ] Create example files
  - [ ] `examples/simple-template.md`
  - [ ] `examples/nested-variables.md`
  - [ ] `examples/optional-vars.md`
  - [ ] `examples/args.json`
  - [ ] `examples/schema-output.json`
- [ ] Create CONTRIBUTING.md
  - [ ] Development setup
  - [ ] Running tests
  - [ ] Code style guidelines
  - [ ] PR process
- [ ] Inline code documentation
  - [ ] XML comments on public methods
  - [ ] README comments in complex algorithms
  - [ ] Usage examples in command classes

### Phase 1 Packaging
- [ ] Configure NuGet package
  - [ ] Set package metadata
  - [ ] Add README to package
  - [ ] Configure license
  - [ ] Add icon (optional)
- [ ] Test local installation
  - [ ] `dotnet pack -c Release`
  - [ ] `dotnet tool install -g --add-source ./nupkg MDTool`
  - [ ] Test all commands work globally
  - [ ] `dotnet tool uninstall -g MDTool`
- [ ] Prepare for distribution
  - [ ] Create GitHub repository
  - [ ] Add .gitignore
  - [ ] Initial commit
  - [ ] Tag v1.0.0

### Phase 1 Completion Criteria
- [ ] ✅ All four commands work correctly
- [ ] ✅ YAML parsing handles both formats (string and object)
- [ ] ✅ Variable substitution supports nested objects (dot notation)
- [ ] ✅ Error handling returns structured JSON
- [ ] ✅ Unit tests pass with >80% coverage
- [ ] ✅ Package installs as global tool successfully
- [ ] ✅ README documents all features with examples
- [ ] ✅ All examples run without errors

---

## Phase 2: Macro System

### Built-in Macros
- [ ] Implement date/time macros
  - [ ] `{{DATE}}` → ISO 8601 date (YYYY-MM-DD)
  - [ ] `{{TIME}}` → ISO 8601 time (HH:MM:SS)
  - [ ] `{{DATETIME}}` → ISO 8601 datetime (YYYY-MM-DDTHH:MM:SSZ)
  - [ ] Use UTC timezone
  - [ ] Expand before variable substitution
  - [ ] Document in README
- [ ] Add macro expansion to `VariableSubstitutor.cs`
  - [ ] Detect system macros vs user variables
  - [ ] Process macros first, then variables
  - [ ] Reserved names validation

### File Expansion - JSON Prefix
- [ ] Implement `file:` prefix in JSON args
  - [ ] Detect `file:` prefix during arg loading
  - [ ] Method: `ExpandFileReferences(jsonObject)` in `VariableSubstitutor.cs`
  - [ ] Resolve paths relative to CWD
  - [ ] Read file contents
  - [ ] Replace JSON value with file contents
  - [ ] Validate file paths (no directory traversal)
  - [ ] Enforce 10MB file size limit
  - [ ] Handle file not found errors
- [ ] Implement recursive processing
  - [ ] Expanded files can contain variables
  - [ ] Process variables in expanded content
  - [ ] Track recursion depth (max 10 levels)
  - [ ] Detect circular references
  - [ ] Error on circular dependency

### File Expansion - Markdown Function
- [ ] Implement `{{file:PATH}}` in markdown
  - [ ] Update regex to detect `{{file:...}}`
  - [ ] Method: `ExpandFileFunction(match)` in `VariableSubstitutor.cs`
  - [ ] Resolve path from CWD
  - [ ] Read and insert file contents
  - [ ] Process variables in expanded content
  - [ ] Maintain recursion tracking
  - [ ] Handle errors gracefully

### Environment Variable Expansion
- [ ] Implement `{{env:VAR_NAME}}` expansion with precedence fallback
  - [ ] Update regex to detect `{{env:...}}`
  - [ ] Method: `ResolveEnvironmentVariable(match)` in `VariableSubstitutor.cs`
  - [ ] **Precedence order (standard env var conventions):**
    - [ ] 1. Check environment variable (case-sensitive): `Environment.GetEnvironmentVariable("VAR_NAME")`
    - [ ] 2. If not found, fall back to JSON args (case-insensitive): check for `var_name` in JSON
    - [ ] 3. If not found, fall back to YAML defaults (if defined)
    - [ ] 4. If not found anywhere, error (missing required variable)
  - [ ] Preserve OS behavior: env vars are case-sensitive
  - [ ] Allow env vars to override JSON args (runtime config takes precedence)
- [ ] Support in both markdown and JSON
  - [ ] Expand during arg loading: `{"db": "{{env:DATABASE_URL}}"}`
  - [ ] Expand during substitution: `{{env:API_KEY}}`
  - [ ] Process env var resolution before standard variable substitution
- [ ] Document precedence behavior
  - [ ] Explain env var → JSON args → YAML defaults → error
  - [ ] Show use cases: CI/CD overrides, local development
  - [ ] Example: `DATABASE_URL=prod mdtool process` overrides JSON

### Phase 2 Testing
- [ ] Macro tests
  - [ ] DATE macro formats correctly
  - [ ] TIME macro formats correctly
  - [ ] DATETIME macro formats correctly
  - [ ] UTC timezone verification
- [ ] File expansion tests (JSON prefix)
  - [ ] Simple file expansion works
  - [ ] Nested variables in expanded files
  - [ ] Recursion depth limit enforced
  - [ ] Circular reference detection
  - [ ] File not found errors
  - [ ] Path traversal prevention
  - [ ] File size limit enforcement
- [ ] File expansion tests (markdown function)
  - [ ] `{{file:path}}` expansion works
  - [ ] Recursive variable processing
  - [ ] Same security validations
- [ ] Environment variable tests
  - [ ] `{{env:VAR}}` expands from environment when set
  - [ ] Falls back to JSON args when env var not set
  - [ ] Falls back to YAML defaults when not in env or JSON
  - [ ] Errors only when not found in any source
  - [ ] Environment variable overrides JSON args (precedence)
  - [ ] Case-sensitive matching for env vars
  - [ ] Case-insensitive matching for JSON fallback
  - [ ] Works in both markdown: `{{env:VAR}}`
  - [ ] Works in JSON args: `{"db": "{{env:DATABASE_URL}}"}`
  - [ ] Precedence order: env → JSON → YAML → error
- [ ] Integration tests
  - [ ] Combine macros + file expansion + variables
  - [ ] Complex nested scenarios
  - [ ] Performance with large expanded files

### Phase 2 Documentation
- [ ] Update README
  - [ ] Document all built-in macros
  - [ ] File expansion examples (both forms)
  - [ ] Environment variable expansion with `{{env:VAR}}`
  - [ ] **Precedence behavior:** env → JSON → YAML → error
  - [ ] Use cases: CI/CD overrides, local vs production config
  - [ ] Example: `DATABASE_URL=prod mdtool process template.md --args dev.json`
  - [ ] Recursion limits explained
  - [ ] Security considerations
- [ ] Create advanced examples
  - [ ] `examples/file-expansion.md`
  - [ ] `examples/env-vars.md`
  - [ ] `examples/macros.md`
  - [ ] `examples/recursive-template.md`
- [ ] Update changelog
  - [ ] Version 1.1.0 features

### Phase 2 Completion Criteria
- [ ] ✅ DATE/TIME/DATETIME macros work correctly
- [ ] ✅ File expansion via `file:` prefix works
- [ ] ✅ File expansion via `{{file:PATH}}` works
- [ ] ✅ Environment variable expansion with `{{env:VAR}}` works
- [ ] ✅ Precedence fallback works: env → JSON → YAML → error
- [ ] ✅ Env vars correctly override JSON args
- [ ] ✅ Recursive processing works correctly (depth limit enforced)
- [ ] ✅ Circular reference detection prevents infinite loops
- [ ] ✅ All security validations in place
- [ ] ✅ Tests pass with >80% coverage
- [ ] ✅ Documentation updated with precedence examples

---

## Phase 3: Advanced Features

### Loop Support
- [ ] Design loop syntax
  - [ ] Decide: `{{FOREACH}}` vs `{{#each}}` (Handlebars-style)
  - [ ] Define end delimiter: `{{END}}` vs `{{/each}}`
  - [ ] Specify iteration variable naming
- [ ] Implement `LoopProcessor.cs`
  - [ ] Parse loop blocks from markdown
  - [ ] Method: `ProcessLoops(content, variables)` returns processed content
  - [ ] Extract loop variable (e.g., `FILES`)
  - [ ] Extract loop body template
  - [ ] Iterate over JSON array
  - [ ] Substitute loop variables in each iteration
  - [ ] Support nested object properties: `{{FILE.NAME}}`
  - [ ] Join results (preserve formatting)
- [ ] Support nested loops
  - [ ] Track loop depth
  - [ ] Prevent deep nesting (max 5 levels?)
  - [ ] Handle variable scoping
  - [ ] Test nested loop scenarios

### Conditional Logic
- [ ] Design conditional syntax
  - [ ] Format: `{{IF VARIABLE}}...{{END}}`
  - [ ] Support `{{ELSE}}` block
  - [ ] Define truthiness rules (empty string, null, false)
- [ ] Implement `ConditionalProcessor.cs`
  - [ ] Parse conditional blocks
  - [ ] Method: `ProcessConditionals(content, variables)` returns processed content
  - [ ] Evaluate condition (variable existence/truthiness)
  - [ ] Include/exclude blocks based on condition
  - [ ] Support nested conditionals
- [ ] Add comparison operators (optional)
  - [ ] `{{IF VAR == "value"}}`
  - [ ] `{{IF VAR > 10}}`
  - [ ] Define operator precedence

### Built-in Functions
- [ ] Implement `{{UPPER:variable}}`
  - [ ] Transform variable value to uppercase
  - [ ] Handle nested variables: `{{UPPER:USER.NAME}}`
- [ ] Implement `{{LOWER:variable}}`
  - [ ] Transform variable value to lowercase
- [ ] Implement `{{LEN:collection}}`
  - [ ] Return length of string or array
  - [ ] Handle edge cases (null, undefined)
- [ ] Implement `{{JOIN:collection:delimiter}}`
  - [ ] Join array elements with delimiter
  - [ ] Default delimiter: `, `
  - [ ] Handle empty arrays
- [ ] Create `FunctionProcessor.cs`
  - [ ] Registry of built-in functions
  - [ ] Method: `ProcessFunctions(content, variables)` returns processed content
  - [ ] Parse function syntax
  - [ ] Execute function with arguments
  - [ ] Support chaining: `{{UPPER:LOWER:NAME}}`

### Phase 3 Testing
- [ ] Loop tests
  - [ ] Simple loop over array
  - [ ] Loop with nested object properties
  - [ ] Nested loops (2 levels)
  - [ ] Empty array handling
  - [ ] Malformed loop syntax errors
- [ ] Conditional tests
  - [ ] IF block with true condition
  - [ ] IF block with false condition
  - [ ] IF/ELSE blocks
  - [ ] Nested conditionals
  - [ ] Truthiness evaluation
- [ ] Function tests
  - [ ] UPPER transformation
  - [ ] LOWER transformation
  - [ ] LEN for strings and arrays
  - [ ] JOIN with various delimiters
  - [ ] Chained functions
  - [ ] Invalid function names
- [ ] Complex integration tests
  - [ ] Loops + conditionals + functions
  - [ ] Loops with file expansion
  - [ ] All features combined

### Phase 3 Documentation
- [ ] Update README
  - [ ] Loop syntax and examples
  - [ ] Conditional syntax and examples
  - [ ] All built-in functions documented
  - [ ] Complex template examples
- [ ] Create advanced examples
  - [ ] `examples/loops.md`
  - [ ] `examples/conditionals.md`
  - [ ] `examples/functions.md`
  - [ ] `examples/complex-template.md`
- [ ] Update changelog
  - [ ] Version 1.2.0 features

### Phase 3 Completion Criteria
- [ ] ✅ FOREACH loops work correctly
- [ ] ✅ Nested loops supported (up to 5 levels)
- [ ] ✅ IF/ELSE conditionals work
- [ ] ✅ All built-in functions implemented (UPPER, LOWER, LEN, JOIN)
- [ ] ✅ Complex templates combining all features work
- [ ] ✅ Tests pass with >80% coverage
- [ ] ✅ Documentation complete with examples

---

## Cross-Phase Tasks

### Security & Validation
- [ ] Path traversal prevention
  - [ ] Validate all file paths
  - [ ] Disallow `..` in paths
  - [ ] Restrict to CWD and subdirectories
- [ ] File size limits enforced
  - [ ] Check before reading files
  - [ ] Error gracefully for oversized files
- [ ] Recursion depth limits
  - [ ] Track depth across all recursive operations
  - [ ] Error when limit exceeded
- [ ] Timeout implementation
  - [ ] Add timeout to all file operations
  - [ ] 30-second limit per operation
  - [ ] Graceful cancellation
- [ ] Input sanitization
  - [ ] Sanitize JSON output
  - [ ] Prevent injection attacks
  - [ ] Validate all user inputs

### Performance Optimization
- [ ] Lazy loading for large files
  - [ ] Stream large files instead of loading entirely
  - [ ] Process in chunks where possible
- [ ] Caching for repeated operations
  - [ ] Cache parsed YAML headers
  - [ ] Cache file expansion results
- [ ] Benchmarking
  - [ ] Measure performance on various file sizes
  - [ ] Optimize bottlenecks
  - [ ] Document performance characteristics

### Developer Experience
- [ ] Add `--verbose` flag
  - [ ] Detailed logging during processing
  - [ ] Show expansion steps
  - [ ] Help debugging complex templates
- [ ] Add `--dry-run` flag (future)
  - [ ] Show what would be processed without doing it
  - [ ] Preview substitutions
- [ ] Config file support (future)
  - [ ] `.mdtoolrc` for default settings
  - [ ] Per-project configuration
- [ ] Better error messages
  - [ ] Include context in errors
  - [ ] Suggest fixes for common mistakes
  - [ ] Friendly formatting

### Continuous Integration
- [ ] Setup CI pipeline
  - [ ] GitHub Actions workflow
  - [ ] Run tests on all commits
  - [ ] Build NuGet package
  - [ ] Test installation
- [ ] Code coverage reporting
  - [ ] Integrate coverage tool
  - [ ] Report to dashboard
  - [ ] Enforce minimum coverage
- [ ] Automated releases
  - [ ] Semantic versioning
  - [ ] Automated changelog
  - [ ] Publish to NuGet on tags

---

## Open Discussion Points

### Decision Required: Loop and Conditional Syntax
**Question:** Which syntax style should we use?

**Option A: Custom syntax**
```markdown
{{FOREACH ITEMS}}
  - {{ITEM.NAME}}
{{END}}

{{IF DEBUG}}
  Debug mode
{{END}}
```

**Option B: Handlebars-style**
```markdown
{{#each items}}
  - {{name}}
{{/each}}

{{#if debug}}
  Debug mode
{{/if}}
```

**Considerations:**
- Custom syntax: More explicit, matches our `{{VARIABLE}}` style
- Handlebars: Familiar to many developers, widely used
- Decision needed before Phase 3 implementation

### Decision Required: Variable Scoping in Loops
**Question:** How should variables be accessed inside loops?

**Option A: Automatic context**
```markdown
{{FOREACH USERS}}
  Name: {{NAME}}  <!-- automatically scoped to current user -->
{{END}}
```

**Option B: Explicit iteration variable**
```markdown
{{FOREACH USER IN USERS}}
  Name: {{USER.NAME}}
{{END}}
```

**Recommendation:** Option B (explicit) - clearer and prevents naming conflicts

### Decision Required: Function Chaining Order
**Question:** How should chained functions execute?

```markdown
{{UPPER:LOWER:NAME}}
```

**Option A:** Left-to-right: `UPPER(LOWER(NAME))`
**Option B:** Right-to-left: `LOWER(UPPER(NAME))`

**Recommendation:** Option A (left-to-right) - more intuitive, matches reading order

---

## Package Distribution

### NuGet Publishing
- [ ] Register NuGet account
- [ ] Configure package metadata
  - [ ] Package ID: `MDTool`
  - [ ] Title: "MDTool - Markdown Processing with Variables"
  - [ ] Tags: markdown, template, cli, dotnet-tool
- [ ] Add license file
  - [ ] Choose license (MIT recommended)
  - [ ] Include LICENSE file
- [ ] Add package icon (optional)
- [ ] Test package locally
- [ ] Publish to NuGet.org
  - [ ] `dotnet nuget push *.nupkg --source https://api.nuget.org/v3/index.json --api-key <key>`

### GitHub Repository
- [ ] Create public repository
- [ ] Add comprehensive README
- [ ] Setup issue templates
- [ ] Setup PR template
- [ ] Add CONTRIBUTING guide
- [ ] Add CODE_OF_CONDUCT
- [ ] Setup GitHub Actions
- [ ] Add badges (build status, coverage, NuGet version)

---

## Version Roadmap

### v1.0.0 - MVP (Phase 1)
**Target:** Full Phase 1 implementation
- [x] Core parsing and substitution
- [x] All four commands
- [x] Comprehensive testing
- [x] Documentation

### v1.1.0 - Macros (Phase 2)
**Target:** Macro system complete
- [ ] Date/time macros
- [ ] File expansion (both forms)
- [ ] Environment variable expansion
- [ ] Recursive processing

### v1.2.0 - Advanced (Phase 3)
**Target:** Loop and conditional support
- [ ] FOREACH loops
- [ ] IF/ELSE conditionals
- [ ] Built-in functions
- [ ] Full feature set

### v1.3.0 - Polish (Future)
**Target:** Developer experience improvements
- [ ] `--verbose` flag
- [ ] `--dry-run` flag
- [ ] Config file support
- [ ] Performance optimizations

---

## Notes

### Implementation Order
1. **Start with Phase 1** - Get the MVP working first
2. **Test thoroughly** - Each component before moving on
3. **Document as you go** - Don't leave docs for the end
4. **Get feedback** - Use Phase 1 before starting Phase 2

### Best Practices
- Write tests first (TDD approach recommended)
- Use Result pattern consistently (no exceptions for business logic)
- Keep commands thin (logic in Core classes)
- Validate early, fail fast with good errors
- Think about AI/automation use cases

### Getting Help
- System.CommandLine docs: https://learn.microsoft.com/en-us/dotnet/standard/commandline/
- YamlDotNet docs: https://github.com/aaubry/YamlDotNet/wiki
- JSON Schema: https://json-schema.org/

---

**Last Updated:** 2025-10-25
**Next Review:** After Phase 1 completion
