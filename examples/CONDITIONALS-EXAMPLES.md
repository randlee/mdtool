# Conditional Sections Examples

This directory contains examples demonstrating MDTool's conditional sections feature (Phase 2).

## Overview

Conditional sections allow you to create dynamic templates that adapt based on input variables. Use the `--enable-conditions` flag to activate this feature.

## Available Examples

### 1. QA Agent Role Switching (`conditionals.md`)
**Purpose:** Demonstrates role-based content switching for QA automation.

**Features:**
- Role-based sections (TEST vs REPORT)
- Environment-specific configuration (DEV vs PROD)
- Debug mode toggling
- Nested conditionals
- The `exists()` function

**Usage:**
```bash
# Test execution mode
mdtool process examples/conditionals.md examples/conditionals-test.json --enable-conditions -o output-test.md

# Report generation mode
mdtool process examples/conditionals.md examples/conditionals-report.json --enable-conditions -o output-report.md
```

---

### 2. Deployment Configuration (`deployment-config.md`)
**Purpose:** Environment-based deployment documentation with feature flags.

**Features:**
- Multi-environment support (development, staging, production)
- Database configuration per environment
- Feature flags (authentication, analytics)
- Monitoring and observability settings
- Security considerations per environment
- Environment-specific deployment steps

**Usage:**
```bash
# Development environment
mdtool process examples/deployment-config.md examples/deployment-config-dev.json --enable-conditions -o deploy-dev.md

# Staging environment
mdtool process examples/deployment-config.md examples/deployment-config-staging.json --enable-conditions -o deploy-staging.md

# Production environment
mdtool process examples/deployment-config.md examples/deployment-config-prod.json --enable-conditions -o deploy-prod.md
```

**Key Conditionals Demonstrated:**
- `{{#if ENVIRONMENT == 'development'}}` - Environment detection
- `{{#if FEATURE_AUTH}}` - Feature flag toggles
- `{{#if ENABLE_MONITORING}}` - Optional sections
- Nested `{{#if}}...{{else if}}...{{else}}` chains

---

### 3. Platform-Specific Installation (`platform-install.md`)
**Purpose:** Platform-specific development environment setup guide.

**Features:**
- Multi-platform support (Windows, macOS, Linux)
- Package manager selection (brew, apt, yum, choco, winget)
- Optional component installation (dev tools, Docker)
- Shell-specific instructions (bash, zsh, powershell, fish)
- Platform-specific troubleshooting

**Usage:**
```bash
# Windows setup
mdtool process examples/platform-install.md examples/platform-install-windows.json --enable-conditions -o install-windows.md

# macOS setup
mdtool process examples/platform-install.md examples/platform-install-macos.json --enable-conditions -o install-macos.md

# Linux setup
mdtool process examples/platform-install.md examples/platform-install-linux.json --enable-conditions -o install-linux.md
```

**Key Conditionals Demonstrated:**
- `{{#if PLATFORM == 'windows'}}` - Platform detection
- `{{#if exists(PACKAGE_MANAGER)}}` - Check if variable is defined
- `{{#if PACKAGE_MANAGER == 'brew'}}` - Package manager selection
- `{{#if INSTALL_DEV_TOOLS}}` - Boolean flags for optional sections
- `{{#if USER_SHELL == 'zsh'}}` - Shell-specific commands

---

## Conditional Syntax Reference

### Basic Conditionals
```markdown
{{#if condition}}
  Content when true
{{/if}}
```

### If-Else
```markdown
{{#if condition}}
  Content when true
{{else}}
  Content when false
{{/if}}
```

### Else-If Chains
```markdown
{{#if condition1}}
  Content for condition1
{{else if condition2}}
  Content for condition2
{{else}}
  Default content
{{/if}}
```

### Operators
- `==` - Equality
- `!=` - Inequality
- `&&` - Logical AND
- `||` - Logical OR
- `!` - Logical NOT
- `()` - Grouping

### Functions
- `exists(variable)` - Check if variable is defined
- `contains(string, substring)` - String contains check
- `startsWith(string, prefix)` - String starts with check
- `endsWith(string, suffix)` - String ends with check
- `in(value, array)` - Array membership check

### Examples
```markdown
{{#if ROLE == 'ADMIN' || ROLE == 'MODERATOR'}}
  Admin/Moderator content
{{/if}}

{{#if exists(OPTIONAL_FEATURE) && OPTIONAL_FEATURE}}
  Optional feature is enabled
{{/if}}

{{#if ENVIRONMENT != 'production'}}
  Development/staging only content
{{/if}}
```

---

## Creating Custom Examples

When creating your own conditional templates:

1. **Define variables** in the frontmatter:
   ```yaml
   ---
   variables:
     YOUR_VAR:
       description: "Description of the variable"
       required: true/false
       default: "optional default value"
   ---
   ```

2. **Use conditionals** to control content:
   - Start with simple if/else for binary choices
   - Use else-if chains for multiple options
   - Nest conditionals when needed (but keep it readable)

3. **Create JSON args files** for different scenarios:
   - Use lowercase keys matching your variable names
   - Provide meaningful test cases

4. **Test thoroughly**:
   ```bash
   mdtool process your-template.md your-args.json --enable-conditions -o output.md
   ```

---

## Tips and Best Practices

1. **Keep conditionals simple** - Complex logic is hard to maintain
2. **Use descriptive variable names** - Makes templates self-documenting
3. **Provide defaults** - Reduces required variables
4. **Test all branches** - Create JSON files for each scenario
5. **Comment complex logic** - Use markdown comments to explain
6. **Avoid deep nesting** - Consider restructuring if > 3 levels deep

---

## Need Help?

- Check the main README for syntax details
- Review existing examples for patterns
- Test your conditionals with different JSON args files
