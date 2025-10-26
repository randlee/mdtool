# MDTool Sprint 1: Phase 1 MVP Implementation

**Sprint Goal:** Implement complete Phase 1 MVP with all four commands, comprehensive unit tests, and zero-warning build.

**Timeline:** Execute in parallel waves with dev-qa loops

**Quality Gate:** All code builds with zero warnings, all unit tests pass

**Current Status:** Waves 1 & 2 Complete (✅), Wave 3 Ready (🔄)

---

## Execution Status

| Wave | Component | Dev | QA | Integration | Status |
|------|-----------|-----|-----|-------------|---------|
| 1 | Models | ✅ | ✅ | N/A | ✅ COMPLETE |
| 2A | Utilities | ✅ | ✅ | ✅ | ✅ COMPLETE |
| 2B | Core | ✅ | ✅ | ✅ | ✅ COMPLETE |
| 3 | Commands | ⏸️ | ⏸️ | ⏸️ | 🔄 NEXT |
| 4 | Integration & Docs | ⏸️ | ⏸️ | ⏸️ | ⏸️ BLOCKED |

**Completed:** 317 tests passing, zero warnings, zero errors
**Next:** Wave 3 - Commands implementation

---

## Design Changes Summary (Pre-Sprint)

### Key Updates From Design Review:
1. **Unified Result/Error Handling:**
   - `ProcessingResult<T>` for all operations  
   - `Unit` type for void operations
   - Canonical `ErrorType` enum with 16 error types

2. **File I/O Separation:**
   - Core classes take string content (e.g., `ParseContent`), not file paths
   - FileHelper handles all I/O operations
   - Better testability, fewer side effects

3. **Variable Naming:**
   - Single regex: `^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$`
   - Allows dot-notation: `USER.EMAIL`
   - Conflict rule: Cannot have both `X` and `X.*`

4. **Schema Keys:**
   - lowerCamelCase for all JSON output
   - Example: `USER.EMAIL` → `{"user": {"email": "..."}}`

5. **Path Validation:**
   - No global CWD restriction in Phase 1
   - `ValidatePath(path, strictForMacros=false)`  
   - `strictForMacros=true` only for Phase 2 expansions

6. **File Operations:**
   - Async signatures: `ReadFileAsync`, `WriteFileAsync`
   - `WriteFileAsync` returns `FileExists` error without `--force`
   - Directory creation in FileHelper

7. **Commands:**
   - Get-schema uses `--file` argument
   - Phase 1 generate-header: flat-only (no nested structure)

---

## Sprint Execution Strategy

### Parallel Wave Architecture

```
Wave 1: Foundation (Models)
   ↓
Wave 2: Business Logic (Core + Utilities in parallel)
   ↓
Wave 3: CLI Interface (Commands)
   ↓
Wave 4: Integration Tests & Documentation
```

### Dev-QA Loop Pattern

For each wave:
1. **Dev Agent(s)** implement code + unit tests to completion
2. **QA Agent** verifies:
   - ✅ Zero build warnings
   - ✅ All unit tests pass
   - ✅ Code coverage >80%
   - ✅ Design document compliance
3. **If QA fails:** 
   - Create Fix Agent with specific issues
   - New QA Agent verifies fixes
   - Repeat until quality gate passed

---

## Wave 1: Models Namespace (Foundation)

### Scope
Implement all model classes with comprehensive unit tests.

### Dev Agent: `models-dev`

**Input Files:**
- `docs/design/Models.md`
- `docs/master-checklist.md`

**Output Files:**
- `src/MDTool/Models/MarkdownDocument.cs`
- `src/MDTool/Models/VariableDefinition.cs`
- `src/MDTool/Models/ValidationResult.cs`
- `src/MDTool/Models/ProcessingResult.cs`
- `src/MDTool/Models/ValidationError.cs`
- `src/MDTool/Models/Unit.cs`
- `tests/MDTool.Tests/Models/MarkdownDocumentTests.cs`
- `tests/MDTool.Tests/Models/VariableDefinitionTests.cs`
- `tests/MDTool.Tests/Models/ValidationResultTests.cs`
- `tests/MDTool.Tests/Models/ProcessingResultTests.cs`
- `tests/MDTool.Tests/Models/ValidationErrorTests.cs`

**Key Requirements:**
1. ✅ `Unit` type for void operations
2. ✅ Canonical `ErrorType` enum (16 error types)
3. ✅ Variable name regex with dot-notation support
4. ✅ `ProcessingResult<T>` with `Map` and `Bind` methods
5. ✅ Factory methods for all models
6. ✅ Complete XML documentation
7. ✅ Unit tests with >80% coverage

**Success Criteria:**
- All models compile without warnings
- All tests pass
- Design document compliance verified

### QA Agent: `models-qa`

**Verification Checklist:**
- [ ] `dotnet build` produces zero warnings
- [ ] `dotnet test` all tests pass
- [ ] All 16 ErrorType values present
- [ ] Variable regex allows dot notation: `USER.EMAIL`
- [ ] `ProcessingResult<Unit>` works for void operations
- [ ] Factory methods enforce validation rules
- [ ] Test coverage >80% (use `dotnet test --collect:"XPlat Code Coverage"`)

**Output:** Pass/Fail report with specific issues

---

## Wave 2A: Utilities Namespace (Parallel with Core)

### Dev Agent: `utilities-dev`

**Input Files:**
- `docs/design/Utilities.md`
- Completed Models namespace

**Output Files:**
- `src/MDTool/Utilities/JsonOutput.cs`
- `src/MDTool/Utilities/FileHelper.cs`
- `tests/MDTool.Tests/Utilities/JsonOutputTests.cs`
- `tests/MDTool.Tests/Utilities/FileHelperTests.cs`

**Key Requirements:**
1. ✅ `FileHelper` uses `ProcessingResult<Unit>` and `ProcessingResult<string>`
2. ✅ Async signatures: `ReadFileAsync`, `WriteFileAsync`
3. ✅ `WriteFileAsync` overwrite policy (FileExists error without `--force`)
4. ✅ `ValidatePath(path, strictForMacros=false)` - no global CWD restriction
5. ✅ Directory creation in `WriteFileAsync`
6. ✅ lowerCamelCase JSON property naming
7. ✅ UTF-8 encoding without BOM
8. ✅ 10MB file size limit

**Success Criteria:**
- Zero warnings
- All tests pass
- Security validations work (path traversal, file size)
- Async patterns correct

### QA Agent: `utilities-qa`

**Verification Checklist:**
- [ ] Builds without warnings
- [ ] All async tests pass
- [ ] `WriteFileAsync` without `--force` returns `FileExists` error
- [ ] `ValidatePath` allows normal paths (no CWD restriction)
- [ ] Directory creation works
- [ ] 10MB limit enforced
- [ ] JSON output uses lowerCamelCase

---

## Wave 2B: Core Namespace (Parallel with Utilities)

### Dev Agent: `core-dev`

**Input Files:**
- `docs/design/Core.md`
- Completed Models namespace

**Output Files:**
- `src/MDTool/Core/MarkdownParser.cs`
- `src/MDTool/Core/VariableExtractor.cs`
- `src/MDTool/Core/SchemaGenerator.cs`
- `src/MDTool/Core/VariableSubstitutor.cs`
- `tests/MDTool.Tests/Core/MarkdownParserTests.cs`
- `tests/MDTool.Tests/Core/VariableExtractorTests.cs`
- `tests/MDTool.Tests/Core/SchemaGeneratorTests.cs`
- `tests/MDTool.Tests/Core/VariableSubstitutorTests.cs`

**Key Requirements:**
1. ✅ `MarkdownParser.ParseContent(string content)` - takes string, not filepath
2. ✅ Variable regex: `^[A-Z][A-Z0-9_]*(?:\.[A-Z][A-Z0-9_]*)*$`
3. ✅ Conflict detection: Cannot have both `X` and `X.*`
4. ✅ Schema output uses lowerCamelCase keys
5. ✅ Case-insensitive JSON matching
6. ✅ Support both YAML formats (string and object)
7. ✅ Error collection (not fail-fast)

**Schema Key Conversion Examples:**
```
USER_NAME    → userName
USER.EMAIL   → user: { email: ... }
API.KEY.NAME → api: { key: { name: ... }}
```

**Success Criteria:**
- Zero warnings
- All tests pass including edge cases
- Conflict detection works
- lowerCamelCase conversion correct

### QA Agent: `core-qa`

**Verification Checklist:**
- [ ] Builds without warnings
- [ ] All tests pass
- [ ] `ParseContent` takes string (no file operations)
- [ ] Dot-notation variables work: `USER.EMAIL`
- [ ] Conflict detection: `X` and `X.Y` → error
- [ ] Schema uses lowerCamelCase: `USER_NAME` → `userName`
- [ ] Both YAML formats supported
- [ ] Error collection works (multiple errors returned)

---

## Wave 2 QA Gate

**After both Utilities and Core pass individual QA:**

### Integration QA Agent: `wave2-integration-qa`

**Verification:**
- [ ] Core + Utilities build together without warnings
- [ ] All tests pass together
- [ ] No circular dependencies
- [ ] FileHelper works with MarkdownParser integration

**If fails:** Create fix agent for specific issues, re-run QA

---

## Wave 3: Commands Namespace

### Dev Agent: `commands-dev`

**Input Files:**
- `docs/design/Commands.md`
- Completed Models, Core, Utilities namespaces

**Output Files:**
- `src/MDTool/Commands/GetSchemaCommand.cs`
- `src/MDTool/Commands/ValidateCommand.cs`
- `src/MDTool/Commands/ProcessCommand.cs`
- `src/MDTool/Commands/GenerateHeaderCommand.cs`
- Updated `src/MDTool/Program.cs`
- `tests/MDTool.Tests/Commands/GetSchemaCommandTests.cs`
- `tests/MDTool.Tests/Commands/ValidateCommandTests.cs`
- `tests/MDTool.Tests/Commands/ProcessCommandTests.cs`
- `tests/MDTool.Tests/Commands/GenerateHeaderCommandTests.cs`

**Key Requirements:**
1. ✅ Each command is separate class (not methods)
2. ✅ System.CommandLine integration
3. ✅ `--force` flag for ProcessCommand
4. ✅ Generate-header: flat-only (no nested structure in Phase 1)
5. ✅ Async handlers
6. ✅ JsonOutput for all responses
7. ✅ Exit codes: 0=success, 1=error

**Success Criteria:**
- Zero warnings
- All command tests pass
- Commands work end-to-end
- Proper error handling

### QA Agent: `commands-qa`

**Verification Checklist:**
- [ ] Builds without warnings
- [ ] All tests pass
- [ ] 4 separate command classes
- [ ] `--force` flag works for process command
- [ ] Generate-header is flat-only (no nested output)
- [ ] All commands return proper JSON
- [ ] Exit codes correct
- [ ] Async patterns correct

---

## Wave 4: Integration & Documentation

### Dev Agent: `integration-dev`

**Tasks:**
1. Create end-to-end integration tests
2. Create example markdown files in `examples/`
3. Create example JSON args files
4. Update README.md with usage examples
5. Test global tool installation

**Output Files:**
- `tests/MDTool.Tests/Integration/EndToEndTests.cs`
- `examples/simple-template.md`
- `examples/nested-variables.md`
- `examples/optional-vars.md`
- `examples/args.json`
- Updated `README.md`

**Success Criteria:**
- End-to-end tests pass
- Examples work correctly
- Tool installs globally
- Documentation complete

### QA Agent: `integration-qa`

**Verification Checklist:**
- [ ] Complete solution builds with zero warnings
- [ ] ALL tests pass (unit + integration)
- [ ] Test coverage >80%
- [ ] Global tool installation works: `dotnet tool install -g MDTool`
- [ ] All 4 commands work end-to-end
- [ ] Examples execute successfully
- [ ] README accurate and complete

---

## Final Sprint QA Gate

### QA Agent: `sprint-final-qa`

**Complete Verification:**
- [ ] `dotnet build` - zero warnings
- [ ] `dotnet test` - 100% pass rate
- [ ] `dotnet test --collect:"XPlat Code Coverage"` - >80% coverage
- [ ] `dotnet pack` - successful package creation
- [ ] `dotnet tool install -g --add-source ./nupkg MDTool` - installs
- [ ] `mdtool --help` - shows all commands
- [ ] Run all 4 commands with examples - all succeed
- [ ] Verify JSON output format for success and error cases
- [ ] Check all design document requirements met

**Output:** Comprehensive pass/fail report

---

## Quality Goals

### Build Quality
- ✅ Zero compiler warnings
- ✅ Zero nullable reference warnings
- ✅ All files compile successfully

### Test Quality
- ✅ 100% test pass rate
- ✅ >80% code coverage
- ✅ Edge cases tested
- ✅ Error paths tested

### Code Quality
- ✅ XML documentation on all public APIs
- ✅ Design document compliance
- ✅ Consistent naming conventions
- ✅ Proper async/await patterns

### Functional Quality
- ✅ All 4 commands work end-to-end
- ✅ Error handling comprehensive
- ✅ JSON output format correct
- ✅ Global tool installation works

---

## Agent Prompts Location

All agent prompts stored in `.prompts/` directory:
- `.prompts/models-dev.md` - Models implementation
- `.prompts/utilities-dev.md` - Utilities implementation
- `.prompts/core-dev.md` - Core implementation
- `.prompts/commands-dev.md` - Commands implementation
- `.prompts/integration-dev.md` - Integration tests
- `.prompts/qa-agent.md` - General QA verification
- `.prompts/fix-agent.md` - Fix template for issues

---

## Success Criteria Summary

Sprint 1 is **COMPLETE** when:
1. ✅ All code builds with zero warnings
2. ✅ All tests pass (100% pass rate)
3. ✅ Test coverage >80%
4. ✅ All 4 commands work end-to-end
5. ✅ Global tool installation successful
6. ✅ Examples execute successfully
7. ✅ Documentation complete and accurate
8. ✅ All design document requirements met

---

## Risk Mitigation

### Potential Issues:
1. **Async patterns:** Use proper `async`/`await`, avoid `.Result` or `.Wait()`
2. **File I/O:** Test with temp directories, clean up after tests
3. **Path handling:** Test on different OS (Windows vs Unix paths)
4. **JSON serialization:** Handle edge cases (null, empty, special chars)

### Mitigation Strategy:
- Dev agents create comprehensive tests
- QA agents verify edge cases
- Fix agents address specific issues immediately
- Re-run QA after all fixes

---

## Next Steps After Sprint 1

Once Sprint 1 completes successfully:
1. Tag release v1.0.0
2. Publish to NuGet
3. Update master checklist
4. Plan Sprint 2 (Phase 2: Macro System)

