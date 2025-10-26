---
variables:
  ROLE:
    description: "User role"
    required: true
---

# Mismatched Tags Test

{{#if ROLE == 'TEST'}}
This has an unclosed if tag.

End of document.
