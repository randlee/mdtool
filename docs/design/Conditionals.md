# Conditionals Namespace Design Document

Project: MDTool — Conditional Sections in Markdown
Version: 1.0.0
Last Updated: 2025-10-26

---

## Overview

This document specifies conditional sections in markdown templates so a single template can target multiple roles or scenarios (e.g., QA agent roles like TEST vs REPORT).

Goals:
- Allow including/excluding sections based on boolean expressions over provided variables (e.g., ROLE, AGENT)
- Keep evaluation deterministic, safe, and independent of external side effects
- Integrate with existing processing pipeline: args → conditions → substitution

Non-goals (Phase 1): loops, arbitrary scripting. These are addressed elsewhere.

---

## Syntax

Block form with else-if and else:

```
{{#if EXPR}}
  ... content when EXPR true ...
{{else if EXPR2}}
  ... content when EXPR2 true ...
{{else}}
  ... content when none of the above true ...
{{/if}}
```

- Tags must be balanced and properly nested (nesting limit: 10).
- Whitespace inside tags is ignored: `{{#if   EXPR}}` is valid.
- Tags may span multiple lines; content preserves original formatting.

---

## Expressions

Type semantics and YAML defaults:
- MarkdownParser preserves default value types (int, double, bool, string)
- Args JSON is parsed with native types
- Expression evaluation is type-aware; no implicit string<->number coercion
- Mismatched type comparisons evaluate false (or error in Strict)

Supported value types: string, number, boolean.

Case behavior:
- Variable lookup: case-insensitive keys (ROLE, role, Role equivalent)
- String comparisons: case-insensitive by default; `--strict-conditions` forces case-sensitive

Operators:
- Unary: `!`
- Binary logical: `&&`, `||`
- Equality: `==`, `!=` (type-aware; strings, numbers, booleans)
- Grouping: `(` `)`

Functions (aliases are case-insensitive):
- `contains(haystack, needle)` or `haystack.Contains(needle)`
- `startsWith(text, prefix)` or `text.StartsWith(prefix)`
- `endsWith(text, suffix)` or `text.EndsWith(suffix)`
- `in(value, [a, b, c])` — matches any element (string comparisons per current case mode)
- `exists(VAR)` — true if VAR is present (after merging defaults), false otherwise

Literals:
- Strings: single or double quotes: 'TEST', "QA"
- Numbers: 123, 45.67
- Booleans: true, false

Unknown variables in expressions:
- Default: evaluate as false (no error)
- With `--strict-conditions`: error (ProcessingError with context)

Return values of functions:
- contains/startsWith/endsWith → bool (case per mode)
- in(value, array) → bool; array elements can be strings/numbers/bools; compared type-safely
- exists(VAR) → bool

Examples:
- `ROLE == 'TEST' || ROLE == 'REPORT'`
- `ROLE.Contains('TEST') || AGENT.StartsWith('QA')`
- `in(ROLE, ['TEST','REPORT']) && !DEBUG`

---

## Processing Order (ProcessCommand and ValidateCommand)

1) Load args (case-insensitive) and apply YAML defaults
2) Evaluate conditionals on the document to produce “effective content” (remove excluded branches)
3) Variable extraction/substitution operates only on effective content
4) Write output (Process) or report validation (Validate)

Macros/file expansion (when enabled in future phases) occur only inside kept branches.

---

## Validation Semantics

Content-scoped by default:
- Only variables referenced in effective content are required to be present
- Optional variables use YAML defaults as before

Override flags:
- `--require-all-yaml`: require all YAML-declared required variables regardless of usage
- `--no-conditions`: skip conditional evaluation (treat tags as literals)
- `--strict-conditions`: unknown vars in expressions cause errors; also enables case-sensitive string comparison

Error conditions:
- Mismatched or improperly nested tags → InvalidVariableFormat (with line)
- Expression parse/eval failure → ProcessingError (with description and line)
- Nesting beyond limit (10) → RecursionDepthExceeded

---

## Core API

New class in Core:

```csharp
namespace MDTool.Core;

public class ConditionalEvaluator
{
    /// <summary>
    /// Evaluates {{#if}} blocks against the provided args and returns pruned content.
    /// </summary>
    /// <param name="content">Original markdown content</param>
    /// <param name="args">Case-insensitive args view (JSON-backed)</param>
    /// <param name="options">Evaluation options</param>
    /// <returns>ProcessingResult with pruned content or errors</returns>
    public ProcessingResult<string> Evaluate(
        string content,
        IArgsAccessor args,
        ConditionalOptions options);
}

public record ConditionalOptions(
    bool Strict = false,
    bool CaseSensitiveStrings = false,
    int MaxNesting = 10
);
```

IArgsAccessor is an abstraction for case-insensitive, dot-path lookups over args (e.g., `Get("ROLE")`, `Get("AGENT.NAME")`).

Algorithm (high level):
- Tokenize tags `{{#if ...}}`, `{{else if ...}}`, `{{else}}`, `{{/if}}`
- Build a block stack; enforce MaxNesting; verify structure
- For each block: parse and evaluate expressions left-to-right until a true branch is found; keep that branch, drop others
- Return concatenation of kept content segments

Expression engine:
- Shunting-yard or Pratt parser for precedence and parentheses
- Operators and functions per spec; values resolve via IArgsAccessor

---

## Commands Integration

New option:
- `--conditions-trace-out <path>`: write a JSON trace of conditional decisions for testing/debugging (stdout remains unchanged)

ProcessCommand (add options):
- `--no-conditions` (bool): Disable conditional evaluation
- `--strict-conditions` (bool): Unknown variables in expressions are errors; strings case-sensitive

Flow:
- Parse markdown (YAML/content)
- Load args JSON
- If not `--no-conditions`, call ConditionalEvaluator to get effective content; else use original content
- Substitute variables on effective content
- Output to stdout or file (existing behavior)

ValidateCommand (add options):
- Same options as ProcessCommand
- Evaluate conditionals to get effective content unless `--no-conditions`
- Extract variables from effective content
- Validate presence according to:
  - Default: content-scoped requiredness
  - If `--require-all-yaml`: all YAML-required variables must be provided

Help text updates to reflect options and semantics.

Trace output schema (written when --conditions-trace-out is specified):
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

---

## Examples

YAML frontmatter (ROLE and AGENT):

```yaml
---
variables:
  ROLE:
    description: "Agent role (e.g., TEST, REPORT)"
    required: true
  AGENT:
    description: "Agent identifier"
    required: false
    default: "qa-1"
---
```

Template content:

```
# QA Execution

{{#if ROLE == 'TEST' || ROLE == 'REPORT'}}
## Shared Setup
- Initialize environment
{{/if}}

{{#if ROLE == 'TEST'}}
## Unit Tests
- Run unit test suite
{{else if ROLE.Contains('REPORT') || AGENT.StartsWith('QA')}}
## Quality Report
- Gather metrics and write report
{{else}}
## No-op
{{/if}}
```

Validate (content-scoped by default):
- If ROLE="TEST": only variables inside TEST or shared sections are required
- If ROLE="OTHER": only variables inside ELSE branch are required

---

## Errors and Diagnostics

- On tag mismatch: `InvalidVariableFormat` with the line of offending tag
- On bad expression: `ProcessingError` with parser message and location
- On unknown variable in `--strict-conditions`: `ProcessingError` naming the variable

JsonOutput examples follow existing conventions.

---

## Testing Strategy

Architecture/testability:
- ConditionalEvaluator is a pure Core component (no System.CommandLine dependency)
- Provide an ArgsJsonAccessor that adapts JsonDocument to IArgsAccessor (case-insensitive, dot-path)
- Provide EvaluateDetailed(content, args, options) → (content, trace) for unit tests
- CLI writes only content to stdout and trace (if any) to the file path; logs/errors go to stderr

Unit:
- Expression parsing precedence, functions, case modes
- Tag nesting/mismatch detection
- Strict vs non-strict unknown variable handling

Integration:
- End-to-end `validate` and `process` by invoking the built DLL via Process.Start()
- Assertions rely on stdout (content/JSON) only; trace validated via --conditions-trace-out file
- No reliance on Console.SetOut capture; compatible with current CI workflows

Edge cases:
- Nested ifs up to depth=10, then fail
- Empty branches, whitespace-only blocks
- Literal `{{#if` inside code fences should still be parsed (no language-sensitive parsing)

Unit:
- Expression parsing precedence, functions, case modes
- Tag nesting/mismatch detection
- Strict vs non-strict unknown variable handling

Integration:
- End-to-end `validate` and `process` with ROLE switching
- Interaction with defaults and optional variables
- Interaction with future macros/file expansions (kept branches only)

Edge cases:
- Nested ifs up to depth=10, then fail
- Empty branches, whitespace-only blocks
- Literal `{{#if` inside code fences should still be parsed (no language-sensitive parsing)

---

## Acceptance Criteria

- ProcessCommand and ValidateCommand support `--no-conditions`, `--strict-conditions`, and `--require-all-yaml` (Validate only)
- ConditionalEvaluator correctly prunes branches and returns errors per spec
- Validation defaults to content-scoped requiredness; override works
- Documentation updated (Core, Commands, Implementation Plan) and examples provided