---
variables:
  ROLE:
    description: "User role"
    required: true
---

# If-ElseIf-Else Test

{{#if ROLE == 'TEST'}}
Content for TEST role.
{{else if ROLE == 'REPORT'}}
Content for REPORT role.
{{else}}
Content for other roles.
{{/if}}

End of document.
