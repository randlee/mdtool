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

# Multiple Blocks Test

{{#if ROLE == 'TEST'}}
First block: TEST role
{{/if}}

Middle content.

{{#if DEBUG}}
Second block: Debug enabled
{{/if}}

End of document.
