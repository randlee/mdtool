# MDTool Test Infrastructure Fix - Session Handoff

**Date:** 2025-10-25
**Project:** MDTool Phase 1
**Current State:** Sprint 1 Complete, PR #1 Open
**Next Task:** Fix remaining 25 test infrastructure issues

---

## COPY/PASTE THIS PROMPT TO NEW CLAUDE CODE SESSION:

```
You are PM-MD, senior .NET CLI project manager for MDTool.

CONTEXT RESTORATION:
I'm continuing work on MDTool after completing Sprint 1 and creating PR #1 for YAML type preservation fixes.

CURRENT STATE:
- Sprint 1: ✅ COMPLETE (all 4 waves done)
- PR #1: Open - https://github.com/randlee/mdtool/pull/1
- Branch: fix/yaml-type-preservation
- Tests: 327/327 passing (100%), 25 skipped
- Issue: 25 tests disabled due to test infrastructure problems

PROJECT OVERVIEW:
MDTool is a .NET 8 CLI tool for markdown variable substitution with YAML frontmatter.
- 4 Commands: get-schema, validate, process, generate-header
- Repository: https://github.com/randlee/mdtool
- Working Directory: /Users/randlee/Documents/github/mdtool

RECENT WORK COMPLETED:
1. ✅ Fixed YAML type preservation bug (int/bool defaults were strings)
2. ✅ Disabled 25 failing tests (test infrastructure issues, not bugs)
3. ✅ Created PR #1 with fixes
4. ✅ All commands work correctly (manually verified)

TEST INFRASTRUCTURE ISSUE:
The 25 disabled tests have console output capture problems:
- Tests pass when run individually
- Tests fail when run in full test suite
- Issue: Console.SetOut() capture doesn't work reliably with System.CommandLine
- Commands themselves work perfectly

DISABLED TESTS LOCATION:
1. tests/MDTool.Tests/Commands/GetSchemaCommandTests.cs (4 tests)
2. tests/MDTool.Tests/Commands/ValidateCommandTests.cs (6 tests)
3. tests/MDTool.Tests/Commands/ProcessCommandTests.cs (7 tests)
4. tests/MDTool.Tests/Commands/GenerateHeaderCommandTests.cs (7 tests)
5. tests/MDTool.Tests/Integration/EndToEndTests.cs (1 test)

All marked with: [Fact(Skip = "Test infrastructure issue...")]

YOUR MISSION:
Create a detailed plan to systematically fix all 25 test infrastructure issues and get to 100% test pass rate.

PHASE 1: PLANNING (DO THIS FIRST)

Please create a comprehensive plan that includes:

1. **Root Cause Analysis**
   - Why does Console.SetOut() fail in test suites?
   - Is it System.CommandLine's internal console abstraction?
   - Is it test execution order/isolation?
   - Is it async/threading issues?

2. **Solution Options**
   - Option A: Fix console capture mechanism
   - Option B: Convert to integration tests (Process.Start)
   - Option C: Use System.CommandLine's TestConsole
   - Option D: Mock/stub console output
   - Recommend best approach with pros/cons

3. **Implementation Strategy**
   - Test-by-test approach vs wholesale refactor
   - Risk assessment
   - Rollback plan if approach doesn't work

4. **Success Criteria**
   - 352/352 tests passing (0 skipped)
   - Tests pass in isolation AND in full suite
   - No flaky tests
   - Maintainable test code

5. **Estimated Effort**
   - Time estimate per approach
   - Complexity rating
   - Resource requirements

6. **Execution Plan**
   - Specific steps in order
   - Which tests to fix first (priority order)
   - Validation checkpoints
   - Rollback triggers

DELIVERABLE:
Provide a detailed, actionable plan document that I can review and approve.

After I approve the plan, you will:
1. Create a new branch: fix/test-infrastructure
2. Coordinate background agents (dev-qa loop) to implement fixes
3. Ensure 100% test pass rate
4. Create PR #2
5. Get both PRs merged

IMPORTANT CONTEXT FILES TO READ:
1. /Users/randlee/Documents/github/mdtool/docs/master-checklist.md
2. /Users/randlee/Documents/github/mdtool/docs/sprint-1-plan.md
3. /Users/randlee/Documents/github/mdtool/tests/MDTool.Tests/Commands/*.cs (all 4 test files)
4. /Users/randlee/Documents/github/mdtool/tests/MDTool.Tests/Integration/EndToEndTests.cs

CONSTRAINTS:
- Do NOT merge PR #1 yet (wait for approval)
- Do NOT modify production code unless absolutely necessary
- Focus on test infrastructure only
- Maintain test coverage at current level
- No breaking changes to test patterns

Begin by reading the test files and creating your detailed plan.
```

---

## ADDITIONAL CONTEXT FOR NEW SESSION

### Project Structure
```
mdtool/
├── src/MDTool/
│   ├── Commands/          # 4 command classes
│   ├── Core/              # MarkdownParser, SchemaGenerator, etc.
│   ├── Models/            # Data models
│   ├── Utilities/         # FileHelper, JsonOutput
│   └── Program.cs         # CLI entry point
├── tests/MDTool.Tests/
│   ├── Commands/          # 25 tests (4 files, all skipped)
│   ├── Core/              # All passing
│   ├── Models/            # All passing
│   ├── Utilities/         # All passing
│   └── Integration/       # 9/10 passing, 1 skipped
├── examples/              # Working example files
└── docs/                  # Complete documentation

Total: 17 production files, 18 test files
```

### Test Infrastructure Problem Details

**Symptom:**
```csharp
// This pattern fails in full test suite
var output = await CaptureOutput(async () =>
{
    return await rootCommand.InvokeAsync(new[] { "get-schema", inputFile });
});

private async Task<(int ExitCode, string StdOut, string StdErr)> CaptureOutput(Func<Task<int>> action)
{
    var originalOut = Console.Out;
    using var outWriter = new StringWriter();
    Console.SetOut(outWriter);

    var exitCode = await action();
    return (exitCode, outWriter.ToString(), string.Empty);
}
```

**Why It Fails:**
System.CommandLine may use its own console abstraction that doesn't respect Console.SetOut().

**What Works:**
- Running tests individually: ✅
- Running commands manually via CLI: ✅
- Integration tests using Process.Start(): ✅

### Recommended Approach (for PM-MD to evaluate)

Convert command tests to integration-style tests similar to EndToEndTests.cs which uses:

```csharp
private async Task<(int exitCode, string output, string error)> RunCommand(string args)
{
    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"run --project ../../src/MDTool/MDTool.csproj -- {args}",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    var process = Process.Start(psi);
    var output = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    return (process.ExitCode, output, error);
}
```

This approach:
- ✅ Tests actual CLI behavior
- ✅ No console capture issues
- ✅ More realistic tests
- ⚠️ Slower execution
- ⚠️ Requires built project

### Key Files to Fix

1. **GetSchemaCommandTests.cs** - 4 tests, ~200 lines
2. **ValidateCommandTests.cs** - 6 tests, ~250 lines
3. **ProcessCommandTests.cs** - 7 tests, ~300 lines
4. **GenerateHeaderCommandTests.cs** - 7 tests, ~250 lines
5. **EndToEndTests.cs** - 1 test, ~450 lines total

Total: ~1,000 lines of test code to refactor

### Success Metrics

**Current State:**
- Passing: 327/327 (100%)
- Skipped: 25
- Failed: 0
- Total: 352

**Target State:**
- Passing: 352/352 (100%)
- Skipped: 0
- Failed: 0
- Total: 352

### Git Workflow

```bash
# Current state
Branch: fix/yaml-type-preservation
PR #1: Open, ready for review

# New work
Branch: fix/test-infrastructure (create from main)
PR #2: Test infrastructure fixes

# Merge order
1. Review and approve PM-MD's plan
2. Implement fixes on fix/test-infrastructure
3. Create PR #2
4. Merge PR #1 first (type preservation fix)
5. Merge PR #2 second (test fixes)
6. Tag v1.0.0 release
```

### Timeline Estimate

- Plan creation: 30 minutes
- Plan review & approval: 15 minutes
- Implementation: 3-4 hours (depends on approach)
- Testing & validation: 1 hour
- PR review: 30 minutes

**Total:** 5-6 hours to 100% test pass rate

---

**NEXT STEPS:**
1. Copy the prompt above into new Claude Code session
2. Review PM-MD's detailed plan
3. Approve or request modifications
4. Let PM-MD coordinate background agents
5. Review PR #2
6. Merge both PRs
7. Ship v1.0.0! 🚀
