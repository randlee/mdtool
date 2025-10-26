---
variables:
  PROJECT_NAME: "Project name"
  VERSION:
    description: "Version number"
    default: "1.0.0"
  DEBUG_MODE:
    description: "Enable debug mode"
    default: "false"
  BUILD_DATE:
    description: "Build date"
    default: "Not specified"
---

# {{PROJECT_NAME}} v{{VERSION}}

**Build Configuration:**
- Version: {{VERSION}}
- Debug Mode: {{DEBUG_MODE}}
- Build Date: {{BUILD_DATE}}

This project was built with the configuration shown above.
