---
variables:
  ROLE:
    description: "User role"
    required: true
  DEBUG:
    description: "Debug mode"
    required: false
    default: false
---

# Nested Conditionals Test

{{#if ROLE == 'TEST'}}
Outer: TEST role
{{#if DEBUG}}
Inner: Debug mode enabled
{{else}}
Inner: Debug mode disabled
{{/if}}
{{else}}
Outer: Other role
{{/if}}

End of document.
