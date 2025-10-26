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
    required: false
    default: false
  ENVIRONMENT:
    description: "Environment (DEV or PROD)"
    required: false
    default: "DEV"
---

# QA Execution Agent: {{AGENT}}

**Environment:** {{ENVIRONMENT}}

---

{{#if ROLE == 'TEST' || ROLE == 'REPORT'}}
## Shared Setup

This section appears for both TEST and REPORT roles.

- **Initialize environment:** {{ENVIRONMENT}}
- **Agent identifier:** {{AGENT}}
- **Timestamp:** 2025-10-26

{{#if DEBUG}}
**Debug Information:**
- Debug mode is enabled
- Verbose logging active
- Detailed error messages available
{{/if}}

{{/if}}

---

{{#if ROLE == 'TEST'}}
## Unit Test Execution

This section appears only when ROLE is TEST.

### Test Suite Configuration

- **Test Framework:** xUnit
- **Coverage Tool:** Coverlet
- **Minimum Coverage:** 80%

### Execution Steps

1. Restore dependencies
2. Build solution
3. Run test suite
4. Collect coverage metrics
5. Report test failures

### Test Output

```
Running tests for {{AGENT}}...
Environment: {{ENVIRONMENT}}
```

{{#if DEBUG}}
### Debug Output

Additional debug information for test execution:
- Test discovery: Enabled
- Parallel execution: Enabled (max degree: 4)
- Test timeout: 30 seconds per test
{{/if}}

{{#if ENVIRONMENT == 'DEV'}}
### Development Environment Notes

- Running in development mode
- Using local test database
- Test data will be reset after execution
{{else if ENVIRONMENT == 'PROD'}}
### Production Environment Notes

- Running in production mode
- Using production test database
- Results will be archived
{{/if}}

### Expected Results

- All unit tests should pass
- Code coverage should meet or exceed 80%
- No critical warnings

---

{{else if ROLE == 'REPORT'}}
## Quality Report Generation

This section appears only when ROLE is REPORT.

### Report Configuration

- **Report Type:** Quality Metrics
- **Agent:** {{AGENT}}
- **Format:** Markdown + JSON

### Metrics Collection

1. Gather test results
2. Analyze code coverage
3. Review static analysis results
4. Compile dependency audit
5. Generate trend analysis

### Report Sections

#### Test Results Summary
- Total tests executed
- Pass/Fail breakdown
- Failure analysis

#### Code Coverage Analysis
- Overall coverage percentage
- Coverage by module
- Uncovered code hotspots

#### Quality Metrics
- Cyclomatic complexity
- Code duplication
- Technical debt

{{#if ENVIRONMENT == 'PROD'}}
### Production Report Publishing

- **Destination:** Production quality dashboard
- **URL:** https://quality.example.com/reports/{{AGENT}}
- **Notification:** Email sent to QA team
- **Retention:** 90 days
{{else}}
### Development Report Generation

- **Destination:** Local filesystem
- **Path:** ./reports/{{AGENT}}-report.md
- **Notification:** Console output only
- **Retention:** Until next report
{{/if}}

{{#if DEBUG}}
### Debug Information

Additional debug information for report generation:
- Report template version: 1.1.0
- Data sources validated: Yes
- Timestamp: 2025-10-26T00:00:00Z
{{/if}}

---

{{else}}
## No-op Mode

This section appears when ROLE is neither TEST nor REPORT.

**Warning:** Unknown role specified: `{{ROLE}}`

Supported roles:
- `TEST` - Execute unit test suite
- `REPORT` - Generate quality report

Please update your configuration and try again.

{{/if}}

---

## Execution Complete

{{#if exists(AGENT)}}
Agent **{{AGENT}}** finished execution.
{{else}}
Anonymous agent finished execution.
{{/if}}

{{#if ROLE == 'TEST' || ROLE == 'REPORT'}}
Thank you for using the QA Execution System!
{{/if}}
