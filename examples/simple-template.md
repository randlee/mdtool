---
variables:
  APP_NAME: "Application name"
  ENVIRONMENT:
    description: "Deployment environment"
    default: "staging"
---

# Deployment Plan: {{APP_NAME}}

Deploying to **{{ENVIRONMENT}}** environment.

## Configuration

Application: {{APP_NAME}}
Environment: {{ENVIRONMENT}}

This deployment will be executed according to standard procedures.
