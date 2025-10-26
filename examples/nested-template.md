---
variables:
  USER.NAME: "User's full name"
  USER.EMAIL: "User's email address"
  USER.PROFILE.BIO:
    description: "User biography"
    default: "No bio provided"
  USER.PROFILE.TITLE:
    description: "User job title"
    default: "Developer"
---

# User Profile: {{USER.NAME}}

## Contact Information

- **Name:** {{USER.NAME}}
- **Email:** {{USER.EMAIL}}

## Professional Details

- **Title:** {{USER.PROFILE.TITLE}}
- **Bio:** {{USER.PROFILE.BIO}}
