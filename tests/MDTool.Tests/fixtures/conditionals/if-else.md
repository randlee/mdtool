---
variables:
  ROLE:
    description: "User role"
    required: true
---

# If-Else Test

{{#if ROLE == 'TEST'}}
Content for TEST role.
{{else}}
Content for other roles.
{{/if}}

End of document.
