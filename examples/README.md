# MDTool Examples

This directory contains example markdown templates and JSON argument files demonstrating MDTool's features.

## Examples Overview

### 1. Simple Template (`simple-template.md` + `simple-args.json`)

**Demonstrates:**
- Basic variable substitution
- Required vs optional variables
- Default values

**Try it:**
```bash
# Extract schema
mdtool get-schema examples/simple-template.md

# Validate arguments
mdtool validate examples/simple-template.md --args examples/simple-args.json

# Process template
mdtool process examples/simple-template.md --args examples/simple-args.json
```

**Expected Output:**
```markdown
# Deployment Plan: MyWebApp

Deploying to **production** environment.
...
```

### 2. Nested Variables (`nested-template.md` + `nested-args.json`)

**Demonstrates:**
- Dot-notation nested variables (`USER.NAME`, `USER.PROFILE.BIO`)
- Nested JSON structure
- Case-insensitive matching

**Try it:**
```bash
# Extract nested schema
mdtool get-schema examples/nested-template.md

# Process with nested args
mdtool process examples/nested-template.md --args examples/nested-args.json
```

**Expected Output:**
```markdown
# User Profile: John Doe

## Contact Information

- **Name:** John Doe
- **Email:** john@example.com
...
```

### 3. Optional Variables (`optional-vars-template.md`)

**Demonstrates:**
- Variables with default values
- Minimal JSON arguments (only required vars)
- Default value substitution

**Try it:**
```bash
# Process with minimal args (only PROJECT_NAME required)
echo '{"projectName": "MyProject"}' > /tmp/minimal-args.json
mdtool process examples/optional-vars-template.md --args /tmp/minimal-args.json
```

**Expected Output:**
```markdown
# MyProject v1.0.0

**Build Configuration:**
- Version: 1.0.0
- Debug Mode: false
- Build Date: Not specified
...
```

### 4. No Frontmatter (`no-frontmatter.md`)

**Demonstrates:**
- Generating YAML frontmatter from existing markdown
- Variable discovery
- Template creation workflow

**Try it:**
```bash
# Generate header from document
mdtool generate-header examples/no-frontmatter.md

# Manually create template by combining header + content
mdtool generate-header examples/no-frontmatter.md > /tmp/header.yaml
cat /tmp/header.yaml examples/no-frontmatter.md > /tmp/template.md

# Now create args file and process
echo '{
  "userName": "Alice Smith",
  "accountId": "12345",
  "userEmail": "alice@example.com",
  "supportEmail": "support@example.com"
}' > /tmp/args.json

mdtool process /tmp/template.md --args /tmp/args.json
```

## Common Workflows

### Workflow 1: Create Template from Scratch

1. Write markdown with `{{VARIABLES}}`
2. Generate YAML header: `mdtool generate-header document.md > header.yaml`
3. Edit descriptions in header.yaml
4. Combine: `cat header.yaml document.md > template.md`
5. Create args.json based on schema
6. Process: `mdtool process template.md --args args.json`

### Workflow 2: Use Existing Template

1. Get schema: `mdtool get-schema template.md --output schema.json`
2. Create args.json based on schema
3. Validate: `mdtool validate template.md --args args.json`
4. Process: `mdtool process template.md --args args.json --output result.md`

### Workflow 3: Overwrite Protection

```bash
# First processing succeeds
mdtool process template.md --args args.json --output output.md

# Second processing fails without --force
mdtool process template.md --args args.json --output output.md
# Error: File exists, use --force to overwrite

# Use --force to overwrite
mdtool process template.md --args args.json --output output.md --force
```

## Tips

1. **Case-Insensitive Matching:** JSON keys can be `userName`, `user_name`, or `UserName` - all match `USER_NAME`
2. **Nested Variables:** Use dot notation in markdown (`{{USER.EMAIL}}`) and nested objects in JSON
3. **Optional Variables:** Always provide default values for optional variables in YAML frontmatter
4. **Validation First:** Always run `validate` before `process` to catch errors early
5. **Schema Output:** Use `--output` to save schemas for version control

## Testing Examples

All examples can be tested with:

```bash
cd examples

# Test all simple examples
mdtool process simple-template.md --args simple-args.json

# Test nested structure
mdtool process nested-template.md --args nested-args.json

# Test optional variables
mdtool process optional-vars-template.md --args simple-args.json
```

## File Structure

```
examples/
├── README.md                    # This file
├── simple-template.md           # Basic template
├── simple-args.json             # Arguments for simple template
├── nested-template.md           # Nested variables template
├── nested-args.json             # Nested structure arguments
├── optional-vars-template.md    # Template with defaults
└── no-frontmatter.md            # Document for header generation
```

## Next Steps

- Read the main README.md for installation and full documentation
- Try modifying these examples
- Create your own templates
- Explore advanced features in future releases (macros, conditionals, loops)
