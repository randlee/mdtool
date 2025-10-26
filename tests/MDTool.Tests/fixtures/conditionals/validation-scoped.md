---
variables:
  ROLE:
    description: "User role"
    required: true
  VAR_A:
    description: "Variable A"
    required: true
  VAR_B:
    description: "Variable B"
    required: true
---

# Content-Scoped Validation Test

{{#if ROLE == 'TEST'}}
This uses {{VAR_A}}.
{{else}}
This uses {{VAR_B}}.
{{/if}}

End of document.
