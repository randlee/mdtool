# Conditionals Sprint Plan

**Project:** MDTool - Conditional Sections Implementation
**Branch:** feature/conditionals
**Sprint Goal:** Add conditional section support with zero regression
**Target Release:** 1.1.0
**Quality Gate:** 100% test pass rate (352+ existing tests)

---

## Sprint Overview

This sprint implements conditional section support for MDTool, allowing templates to include/exclude content based on boolean expressions over provided variables. This enables role-based content switching (e.g., QA-TEST vs QA-REPORT agents).

**Processing Order:** args+defaults → conditionals → macros/env/file (future) → substitution

**Key Design Decisions:**
- Opt-in via `--enable-conditions` flag (disabled by default in 1.1.0)
- Code fence protection (skip tags inside ``` by default)
- Content-scoped validation (only require vars in kept branches)
- Expression engine: operators (==, !=, &&, ||, !, ()), functions (contains, startsWith, endsWith, in, exists)
- MaxNesting=5 default (configurable via `--conditions-max-depth`)

**References:**
- Design Specification: `docs/design/Conditionals.md`
- Master Checklist: `docs/master-checklist.md`
- Core Design: `docs/design/Core.md`
- Commands Design: `docs/design/Commands.md`

---

## Implementation Strategy: 3 Waves

### Wave 1: Core Conditionals Logic
**Goal:** Implement ConditionalEvaluator with expression parser and tag matching
**Files:** 5-7 new Core classes + unit tests
**Tests:** ~35 new unit tests
**Dev Agent → QA Agent validation → Zero regression confirmation**

### Wave 2: Command Integration
**Goal:** Add CLI options to ProcessCommand and ValidateCommand
**Files:** Modify 2 commands + integration tests
**Tests:** ~20 new integration tests
**Dev Agent → QA Agent validation → 100% pass rate**

### Wave 3: Documentation & Examples
**Goal:** Complete documentation and create working examples
**Files:** Update design docs, README, create examples
**Tests:** ~5 new example tests
**Dev Agent → QA Agent validation → Final approval**

**Total Estimated Tests:** 412+ (352 existing + ~60 new)

---

## Wave 1: Core Conditionals Logic

### Checklist

#### Core Abstractions
- [ ] Create `Core/IArgsAccessor.cs` interface
  - [ ] TryGet(string path, out object? value) method
  - [ ] Case-insensitive, dot-path support
- [ ] Create `Core/ArgsJsonAccessor.cs` implementation
  - [ ] JsonDocument backing
  - [ ] Case-insensitive property matching
  - [ ] Dot-path navigation (USER.NAME)
  - [ ] Type conversion (JsonElement → C# types)
- [ ] Create `Core/ConditionalOptions.cs` record
  - [ ] bool Strict (default: false)
  - [ ] bool CaseSensitiveStrings (default: false)
  - [ ] int MaxNesting (default: 5)
- [ ] Create `Core/ConditionalTrace.cs` classes
  - [ ] ConditionalTrace with Blocks list
  - [ ] ConditionalBlockTrace (StartLine, EndLine, Branches)
  - [ ] ConditionalBranchTrace (Kind, Expr, Taken)

#### Expression Parser
- [ ] Create `Core/ConditionalEvaluator.cs`
- [ ] Implement tokenizer
  - [ ] Operators: ==, !=, &&, ||, !, (, )
  - [ ] Literals: strings ('...' or "..."), numbers, booleans
  - [ ] Variables: ROLE, USER.NAME, etc.
  - [ ] Functions: contains, startsWith, endsWith, in, exists
- [ ] Implement parser (Pratt or shunting-yard)
  - [ ] Operator precedence: ! > && > ||
  - [ ] Parentheses grouping
  - [ ] Function calls with varargs
- [ ] Implement evaluator
  - [ ] Type-aware comparisons (string, number, bool)
  - [ ] Case-insensitive var lookup via IArgsAccessor
  - [ ] String comparison case mode (default: insensitive, strict: sensitive)
  - [ ] Unknown variables: false (default) or error (strict)
  - [ ] Function implementations

#### Tag Matching & Block Processing
- [ ] Implement tag scanner
  - [ ] Find {{#if EXPR}}, {{else if EXPR}}, {{else}}, {{/if}}
  - [ ] Code fence detection (skip tags inside ``` or ~~~)
  - [ ] Line number tracking for errors
- [ ] Implement block parser
  - [ ] Build block stack
  - [ ] Enforce MaxNesting limit
  - [ ] Validate balanced tags
  - [ ] Detect orphaned else/else-if
- [ ] Implement block evaluator
  - [ ] Evaluate branches left-to-right
  - [ ] Keep first true branch content
  - [ ] Drop other branches
  - [ ] Generate trace for debugging

#### Public API
- [ ] Evaluate(content, args, options) → ProcessingResult&lt;string&gt;
- [ ] EvaluateDetailed(content, args, options) → ProcessingResult&lt;(string, ConditionalTrace)&gt;

#### Error Handling
- [ ] Mismatched tags → ValidationError.InvalidVariableFormat with line
- [ ] Nesting > MaxNesting → ValidationError.RecursionDepthExceeded
- [ ] Expression parse failure → ValidationError.ProcessingError with context
- [ ] Unknown var in strict mode → ValidationError.ProcessingError with var name

#### Unit Tests (~35 tests)
- [ ] Expression parsing tests
  - [ ] Simple comparisons: ROLE == 'TEST'
  - [ ] Logical operators: A && B, A || B, !A
  - [ ] Precedence: A || B && C = A || (B && C)
  - [ ] Parentheses: (A || B) && C
  - [ ] String functions: ROLE.Contains('TEST')
  - [ ] Varargs: in(ROLE, 'TEST', 'REPORT')
  - [ ] Type handling: string vs number vs bool
- [ ] Tag matching tests
  - [ ] Simple if/endif
  - [ ] If/else/endif
  - [ ] If/else-if/else/endif
  - [ ] Nested blocks (depth 1-5)
  - [ ] Mismatched tags (error)
  - [ ] Depth limit (error at 6)
- [ ] Code fence tests
  - [ ] Skip tags inside ``` fences
  - [ ] Skip tags inside ~~~ fences
  - [ ] Don't skip outside fences
- [ ] Evaluation tests
  - [ ] Unknown var: false (default)
  - [ ] Unknown var: error (strict)
  - [ ] Case-insensitive var lookup
  - [ ] Case-insensitive string comparison (default)
  - [ ] Case-sensitive string comparison (strict)
  - [ ] Null handling with exists()
- [ ] Integration tests
  - [ ] Multiple blocks in one document
  - [ ] Empty branches
  - [ ] Whitespace preservation

### Success Criteria (Wave 1)
- [ ] All 352 existing tests pass (zero regression)
- [ ] All ~35 new ConditionalEvaluator tests pass
- [ ] Total: ~387 tests passing (100%)
- [ ] Build succeeds with zero errors/warnings
- [ ] Code coverage >80% on ConditionalEvaluator

---

## Wave 2: Command Integration

### Checklist

#### ProcessCommand Options
- [ ] Add `--enable-conditions` flag (default: false)
- [ ] Add `--strict-conditions` flag (default: false)
- [ ] Add `--conditions-trace-out <path>` option
- [ ] Add `--conditions-trace-stderr` flag
- [ ] Add `--conditions-max-depth <n>` option (default: 5)
- [ ] Add `--parse-fences` flag (default: false)

#### ProcessCommand Flow Updates
- [ ] Merge args with YAML defaults
- [ ] Call ConditionalEvaluator if --enable-conditions
- [ ] Write trace to file if --conditions-trace-out specified
- [ ] Write trace to stderr if --conditions-trace-stderr
- [ ] Extract variables from effective content (not original)
- [ ] Validate on effective content
- [ ] Substitute on effective content

#### ValidateCommand Options
- [ ] Add same conditional options as ProcessCommand
- [ ] Add `--require-all-yaml` flag (default: false)

#### ValidateCommand Flow Updates
- [ ] Evaluate conditionals to get effective content (if --enable-conditions)
- [ ] Extract variables from effective content
- [ ] Content-scoped validation (default):
  - [ ] Only require vars referenced in effective content
- [ ] All-YAML validation (--require-all-yaml):
  - [ ] Require all YAML-declared required variables

#### Integration Tests (~20 tests)
- [ ] Basic conditionals
  - [ ] Single if/endif
  - [ ] If/else/endif
  - [ ] If/else-if/else/endif
  - [ ] Nested conditionals
- [ ] Role-based content
  - [ ] ROLE='TEST' keeps test content
  - [ ] ROLE='REPORT' keeps report content
  - [ ] ROLE='OTHER' keeps else content
- [ ] Content-scoped validation
  - [ ] Var in excluded branch not required (default)
  - [ ] Var in excluded branch required (--require-all-yaml)
- [ ] Code fence protection
  - [ ] Tags inside ``` fence remain literal (default)
  - [ ] Tags inside ~~~ fence remain literal (default)
  - [ ] Tags inside fence evaluated (--parse-fences)
- [ ] Trace output
  - [ ] --conditions-trace-out writes JSON to file
  - [ ] --conditions-trace-stderr writes to stderr
  - [ ] Trace contains block/branch information
- [ ] Error cases
  - [ ] Mismatched tags return error
  - [ ] Unknown var in strict mode returns error
  - [ ] Nesting > max depth returns error
- [ ] Disabled by default
  - [ ] Without --enable-conditions, tags remain literal

### Manual Testing Checklist
- [ ] Test 1: Basic conditional with --enable-conditions
- [ ] Test 2: Disabled by default (no flag)
- [ ] Test 3: Trace output to file
- [ ] Test 4: Strict mode error handling
- [ ] Test 5: Code fence protection

### Success Criteria (Wave 2)
- [ ] All 352 existing tests pass
- [ ] All ~35 Wave 1 tests pass
- [ ] All ~20 Wave 2 integration tests pass
- [ ] Total: ~407 tests passing (100%)
- [ ] All command options functional
- [ ] Manual testing checklist passes

---

## Wave 3: Documentation & Examples

### Checklist

#### Design Document Updates
- [ ] Update `docs/design/Core.md`
  - [ ] Add ConditionalEvaluator section
  - [ ] Document processing order
  - [ ] Document syntax and expressions
  - [ ] Document IArgsAccessor abstraction
- [ ] Update `docs/design/Commands.md`
  - [ ] Document ProcessCommand conditional options
  - [ ] Document ValidateCommand conditional options
  - [ ] Document validation modes (content-scoped vs all-YAML)

#### README Updates
- [ ] Add "Conditional Sections (v1.1.0+)" section
- [ ] Document syntax with examples
- [ ] Document all operators and functions
- [ ] Document validation modes
- [ ] Document debugging with trace output
- [ ] Document code fence protection

#### Examples
- [ ] Create `examples/conditionals.md`
  - [ ] YAML with ROLE, AGENT, DEBUG, ENVIRONMENT variables
  - [ ] Conditional blocks for TEST role
  - [ ] Conditional blocks for REPORT role
  - [ ] Shared setup blocks
  - [ ] Debug information block
  - [ ] Environment-specific blocks
- [ ] Create `examples/conditionals-test.json`
  - [ ] Args for TEST role scenario
- [ ] Create `examples/conditionals-report.json`
  - [ ] Args for REPORT role scenario

#### Example Tests (~5 tests)
- [ ] TEST role generates test content
- [ ] REPORT role generates report content
- [ ] Debug mode shows debug info
- [ ] Trace output validates correctly
- [ ] All examples execute without errors

#### Master Checklist Update
- [ ] Update `docs/master-checklist.md`
  - [ ] Mark Phase 2 Conditional Sections as complete
  - [ ] List all completed tasks
  - [ ] Document total test count (412+)

### Success Criteria (Wave 3)
- [ ] All 352 existing tests pass
- [ ] All ~35 Wave 1 tests pass
- [ ] All ~20 Wave 2 tests pass
- [ ] All ~5 Wave 3 example tests pass
- [ ] Total: ~412 tests passing (100%)
- [ ] All documentation accurate and complete
- [ ] All examples execute successfully
- [ ] No broken links or outdated information

---

## Quality Gates

### Per-Wave Gates
Each wave must meet these criteria before proceeding:
- ✅ 100% test pass rate (no failing tests)
- ✅ Zero regression on existing tests
- ✅ Build with zero errors/warnings
- ✅ QA Agent approval

### Final Sprint Gate
Before merging to main:
- ✅ All 412+ tests passing (100%)
- ✅ Zero regressions detected
- ✅ All documentation complete and accurate
- ✅ All examples working correctly
- ✅ Code coverage >80% on new code
- ✅ QA Agent final approval

---

## Risk Mitigation

### Known Risks

**1. Expression Parser Complexity**
- **Risk:** Precedence bugs in operator parsing
- **Mitigation:** Comprehensive unit tests for all combinations
- **Detection:** Wave 1 QA catches early

**2. Code Fence Edge Cases**
- **Risk:** Unusual fence syntax breaks detection
- **Mitigation:** Test multiple fence styles
- **Detection:** Integration tests in Wave 2

**3. Performance Impact**
- **Risk:** Large documents slow down
- **Mitigation:** Benchmark with 10K+ line docs
- **Fallback:** Users can disable with no flag

**4. Opt-in UX Confusion**
- **Risk:** Users don't know about --enable-conditions
- **Mitigation:** Clear documentation + warning if tags detected
- **Detection:** User feedback post-release

**5. Type System Surprises**
- **Risk:** Users expect "123" == 123 to work
- **Mitigation:** Clear docs about type-aware comparisons
- **Future:** Add type coercion functions if needed

### Rollback Plan
- Opt-in design minimizes risk (disabled by default)
- If issues found: document workarounds in README
- Critical bugs: hotfix patch or disable feature

---

## Agent Coordination Pattern

### Dev-QA Loop (Per Wave)

```
1. PM-MD assigns tasks to Dev Agent (background)
2. Dev Agent implements features + tests
3. Dev Agent signals completion
4. PM-MD launches QA Agent (background)
5. QA Agent runs full test suite
6. QA Agent validates success criteria
7. QA Agent reports to PM-MD
8. PM-MD reviews, approves/rejects
9. If approved: proceed to next wave
10. If rejected: Dev Agent fixes issues, repeat from step 3
```

### Communication Protocol
- **Dev Agent Output:** Implementation summary + test count
- **QA Agent Output:** Test results + pass/fail + issues found
- **PM-MD Decision:** PASS / CONDITIONAL PASS / FAIL with rationale

---

## Timeline Estimate

- **Wave 1:** Core Logic - 2-3 hours (dev) + 30 min (qa) = ~3.5 hours
- **Wave 2:** Command Integration - 1-2 hours (dev) + 30 min (qa) = ~2.5 hours
- **Wave 3:** Documentation - 1 hour (dev) + 30 min (qa) = ~1.5 hours
- **Total:** ~7-8 hours for complete sprint

---

## Success Metrics

### Code Quality
- Test coverage: >80% on new ConditionalEvaluator
- Test pass rate: 100% (412+ tests)
- Build status: Zero errors/warnings
- Regression count: 0

### Functionality
- All conditional expressions work correctly
- All command options functional
- Code fence protection works
- Trace output writes correctly
- Validation modes work (content-scoped vs all-YAML)

### Documentation
- Design docs updated and accurate
- README has clear usage guide
- Examples demonstrate all features
- No broken links or outdated info

---

## References

### Design Documents
- **Conditionals Spec:** `docs/design/Conditionals.md`
- **Core Design:** `docs/design/Core.md`
- **Commands Design:** `docs/design/Commands.md`
- **Models Design:** `docs/design/Models.md`
- **Utilities Design:** `docs/design/Utilities.md`

### Project Documents
- **Master Checklist:** `docs/master-checklist.md`
- **Implementation Plan:** `docs/mdtool-implementation-plan.md`

### Sprint Branch
- **Branch Name:** `feature/conditionals`
- **Base Branch:** `main`
- **Target Merge:** `main` (after all waves complete)

---

## Completion Criteria

Sprint is complete when:
- [ ] All 3 waves completed and QA-approved
- [ ] All 412+ tests passing (100%)
- [ ] All documentation updated
- [ ] All examples working
- [ ] Master checklist updated
- [ ] This sprint plan marked complete
- [ ] Ready for PR creation and review

**Once complete, create PR #2 to merge feature/conditionals → main**

---

**Sprint Status:** 🟡 READY TO START
**Current Wave:** Wave 1 - Core Conditionals Logic
**Last Updated:** 2025-10-26
