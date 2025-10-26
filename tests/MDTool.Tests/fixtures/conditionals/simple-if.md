---
variables:
  ROLE:
    description: "User role"
    required: true
---

# Simple Conditional Test

{{#if ROLE == 'TEST'}}
This content should appear when ROLE is TEST.
{{/if}}

End of document.
