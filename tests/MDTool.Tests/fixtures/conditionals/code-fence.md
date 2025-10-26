---
variables:
  ROLE:
    description: "User role"
    required: true
---

# Code Fence Test

{{#if ROLE == 'TEST'}}
This is outside code fence.
{{/if}}

```markdown
{{#if ROLE == 'TEST'}}
This should remain literal inside code fence.
{{/if}}
```

End of document.
