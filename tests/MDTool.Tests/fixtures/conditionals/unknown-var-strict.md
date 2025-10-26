---
variables:
  ROLE:
    description: "User role"
    required: true
---

# Unknown Variable Test

{{#if UNKNOWN_VAR == 'TEST'}}
This references an unknown variable.
{{/if}}

End of document.
