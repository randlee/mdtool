---
variables:
  ENVIRONMENT:
    description: "Deployment environment (development, staging, production)"
    required: true
  APP_NAME:
    description: "Application name"
    required: true
  FEATURE_AUTH:
    description: "Enable authentication feature"
    required: false
    default: true
  FEATURE_ANALYTICS:
    description: "Enable analytics feature"
    required: false
    default: false
  ENABLE_MONITORING:
    description: "Enable application monitoring"
    required: false
    default: false
---

# {{APP_NAME}} Deployment Configuration

**Target Environment:** {{ENVIRONMENT}}

---

## Overview

This document provides deployment configuration for {{APP_NAME}} based on the target environment.

{{#if ENVIRONMENT == 'development'}}
**NOTE:** This is a development deployment. Security features may be relaxed for easier debugging.
{{else if ENVIRONMENT == 'staging'}}
**NOTE:** This is a staging deployment. This environment mirrors production for final testing.
{{else if ENVIRONMENT == 'production'}}
**WARNING:** This is a production deployment. All security features are enabled and changes require approval.
{{else}}
**ERROR:** Unknown environment specified: {{ENVIRONMENT}}
Valid options: development, staging, production
{{/if}}

---

## Database Configuration

{{#if ENVIRONMENT == 'development'}}
### Development Database

- **Host:** localhost
- **Port:** 5432
- **Database:** {{APP_NAME}}_dev
- **SSL:** Disabled
- **Connection Pool:** 5 connections
- **Auto-migrations:** Enabled

**Connection String:**
```
postgresql://devuser:devpass@localhost:5432/{{APP_NAME}}_dev
```

**Notes:**
- Database can be reset at any time
- Test data is seeded automatically
- Query logging is enabled for debugging

{{else if ENVIRONMENT == 'staging'}}
### Staging Database

- **Host:** staging-db.example.com
- **Port:** 5432
- **Database:** {{APP_NAME}}_staging
- **SSL:** Required (TLS 1.2+)
- **Connection Pool:** 20 connections
- **Auto-migrations:** Enabled with approval

**Connection String:**
```
postgresql://staginguser:${DB_PASSWORD}@staging-db.example.com:5432/{{APP_NAME}}_staging?sslmode=require
```

**Notes:**
- Database is backed up daily
- Mirrors production schema
- Limited test data available

{{else if ENVIRONMENT == 'production'}}
### Production Database

- **Host:** prod-db.example.com (Primary), prod-db-replica.example.com (Replica)
- **Port:** 5432
- **Database:** {{APP_NAME}}_prod
- **SSL:** Required (TLS 1.3)
- **Connection Pool:** 50 connections (primary), 30 connections (replica)
- **Auto-migrations:** Disabled (manual migration required)
- **High Availability:** Active-passive failover enabled
- **Backup:** Continuous backup + daily snapshots

**Connection String:**
```
postgresql://produser:${DB_PASSWORD}@prod-db.example.com:5432/{{APP_NAME}}_prod?sslmode=require&pool_size=50
```

**Notes:**
- All migrations require DBA approval
- Read replicas available for reporting queries
- Point-in-time recovery available for 30 days

{{/if}}

---

## Application Settings

### Base Configuration

{{#if ENVIRONMENT == 'development'}}
```json
{
  "app_name": "{{APP_NAME}}",
  "environment": "{{ENVIRONMENT}}",
  "log_level": "DEBUG",
  "hot_reload": true,
  "cors_enabled": true,
  "cors_origins": ["*"],
  "max_request_size": "10MB",
  "request_timeout": 30
}
```
{{else if ENVIRONMENT == 'staging'}}
```json
{
  "app_name": "{{APP_NAME}}",
  "environment": "{{ENVIRONMENT}}",
  "log_level": "INFO",
  "hot_reload": false,
  "cors_enabled": true,
  "cors_origins": ["https://staging.example.com", "https://staging-api.example.com"],
  "max_request_size": "10MB",
  "request_timeout": 30
}
```
{{else if ENVIRONMENT == 'production'}}
```json
{
  "app_name": "{{APP_NAME}}",
  "environment": "{{ENVIRONMENT}}",
  "log_level": "WARNING",
  "hot_reload": false,
  "cors_enabled": true,
  "cors_origins": ["https://example.com", "https://api.example.com"],
  "max_request_size": "10MB",
  "request_timeout": 30
}
```
{{/if}}

---

## Feature Flags

{{#if FEATURE_AUTH}}
### Authentication Feature: ENABLED

{{#if ENVIRONMENT == 'development'}}
**Authentication Provider:** Local (OAuth2 mock)
- **Token expiry:** 24 hours
- **Refresh tokens:** Enabled
- **Test accounts:** Available in `docs/test-accounts.md`
{{else if ENVIRONMENT == 'staging'}}
**Authentication Provider:** Auth0 (Staging)
- **Token expiry:** 2 hours
- **Refresh tokens:** Enabled
- **Domain:** staging-auth.example.com
{{else if ENVIRONMENT == 'production'}}
**Authentication Provider:** Auth0 (Production)
- **Token expiry:** 1 hour
- **Refresh tokens:** Enabled
- **Domain:** auth.example.com
- **MFA:** Required for admin users
{{/if}}

{{else}}
### Authentication Feature: DISABLED

**WARNING:** Application running without authentication. All endpoints are publicly accessible.

{{/if}}

{{#if FEATURE_ANALYTICS}}
### Analytics Feature: ENABLED

{{#if ENVIRONMENT == 'development'}}
**Analytics Provider:** Console logging
- Events are logged to console only
- No data is transmitted
{{else if ENVIRONMENT == 'staging'}}
**Analytics Provider:** Google Analytics (Staging)
- **Tracking ID:** UA-STAGING-001
- **Sample rate:** 100%
{{else if ENVIRONMENT == 'production'}}
**Analytics Provider:** Google Analytics (Production)
- **Tracking ID:** UA-PROD-001
- **Sample rate:** 10%
- **PII filtering:** Enabled
- **Data retention:** 26 months
{{/if}}

{{else}}
### Analytics Feature: DISABLED

No user analytics will be collected.

{{/if}}

---

## Monitoring and Observability

{{#if ENABLE_MONITORING}}
### Monitoring: ENABLED

{{#if ENVIRONMENT == 'development'}}
**Monitoring Stack:** Local (Docker)
- **Metrics:** Prometheus (http://localhost:9090)
- **Logs:** Loki (http://localhost:3100)
- **Dashboards:** Grafana (http://localhost:3000)
- **Retention:** 7 days

{{else if ENVIRONMENT == 'staging'}}
**Monitoring Stack:** Cloud (Staging)
- **Metrics:** CloudWatch / Prometheus
- **Logs:** CloudWatch Logs
- **Dashboards:** Grafana Cloud
- **Alerts:** Email notifications to dev team
- **Retention:** 30 days

{{else if ENVIRONMENT == 'production'}}
**Monitoring Stack:** Cloud (Production)
- **Metrics:** CloudWatch / Prometheus
- **Logs:** CloudWatch Logs + S3 archive
- **Dashboards:** Grafana Cloud
- **Alerts:** PagerDuty (critical), Slack (warning)
- **Retention:** 90 days (metrics), 1 year (logs)
- **APM:** New Relic enabled
- **Uptime Monitoring:** Pingdom (1-minute intervals)

**Alert Thresholds:**
- CPU > 80% for 5 minutes: Warning
- CPU > 95% for 2 minutes: Critical
- Error rate > 1%: Warning
- Error rate > 5%: Critical
- Response time p95 > 500ms: Warning
- Response time p95 > 1000ms: Critical

{{/if}}

{{else}}
### Monitoring: DISABLED

No monitoring configured. Application health must be checked manually.

{{/if}}

---

## Deployment Steps

{{#if ENVIRONMENT == 'development'}}
### Development Deployment

1. **Pull latest code:**
   ```bash
   git pull origin develop
   ```

2. **Install dependencies:**
   ```bash
   npm install
   ```

3. **Run database migrations:**
   ```bash
   npm run migrate
   ```

4. **Seed test data:**
   ```bash
   npm run seed
   ```

5. **Start development server:**
   ```bash
   npm run dev
   ```

6. **Access application:**
   - Frontend: http://localhost:3000
   - API: http://localhost:8080
   - API Docs: http://localhost:8080/docs

{{else if ENVIRONMENT == 'staging'}}
### Staging Deployment

1. **Create deployment branch:**
   ```bash
   git checkout -b deploy/staging-$(date +%Y%m%d-%H%M%S)
   ```

2. **Run tests:**
   ```bash
   npm run test
   npm run test:integration
   ```

3. **Build application:**
   ```bash
   npm run build
   ```

4. **Deploy to staging:**
   ```bash
   ./scripts/deploy-staging.sh
   ```

5. **Run smoke tests:**
   ```bash
   npm run test:smoke -- --env=staging
   ```

6. **Verify deployment:**
   - Frontend: https://staging.example.com
   - API: https://staging-api.example.com
   - Health check: https://staging-api.example.com/health

{{else if ENVIRONMENT == 'production'}}
### Production Deployment

**IMPORTANT:** Production deployments require approval and must follow the change management process.

1. **Create release tag:**
   ```bash
   git tag -a v1.0.0 -m "Release v1.0.0"
   git push origin v1.0.0
   ```

2. **Pre-deployment checklist:**
   - [ ] All tests passing
   - [ ] Security scan completed
   - [ ] Change request approved
   - [ ] Rollback plan documented
   - [ ] On-call engineer notified

3. **Deploy to production:**
   ```bash
   ./scripts/deploy-production.sh --tag=v1.0.0
   ```

4. **Run smoke tests:**
   ```bash
   npm run test:smoke -- --env=production
   ```

5. **Monitor deployment:**
   - Check error rates in monitoring dashboard
   - Verify health checks are passing
   - Monitor user traffic patterns

6. **Post-deployment:**
   - [ ] Verify critical user flows
   - [ ] Update deployment log
   - [ ] Notify stakeholders

{{/if}}

---

## Security Considerations

{{#if ENVIRONMENT == 'production'}}
### Production Security Requirements

- All secrets stored in AWS Secrets Manager
- TLS 1.3 required for all connections
- Rate limiting: 1000 requests per minute per IP
- DDoS protection: CloudFlare enabled
- WAF: AWS WAF with OWASP Top 10 rules
- Security headers: HSTS, CSP, X-Frame-Options
- Dependency scanning: Daily automated scans
- Penetration testing: Quarterly

{{else if ENVIRONMENT == 'staging'}}
### Staging Security Requirements

- Secrets stored in environment variables
- TLS 1.2+ required
- Rate limiting: 5000 requests per minute per IP
- Security headers: Basic set enabled
- Dependency scanning: Weekly

{{else if ENVIRONMENT == 'development'}}
### Development Security Notes

- Secrets can be stored in `.env` file
- HTTP allowed for local development
- No rate limiting
- Security headers optional
- Use test credentials only (never production credentials)

{{/if}}

---

## Support and Troubleshooting

{{#if ENVIRONMENT == 'production'}}
### Production Support

- **On-call:** PagerDuty rotation
- **Response time:** 15 minutes for critical issues
- **Escalation:** Follow incident response playbook
- **Documentation:** https://wiki.example.com/ops/runbooks

{{else if ENVIRONMENT == 'staging'}}
### Staging Support

- **Contact:** DevOps team on Slack (#devops)
- **Response time:** Best effort during business hours
- **Documentation:** https://wiki.example.com/ops/staging

{{else if ENVIRONMENT == 'development'}}
### Development Support

- **Contact:** Development team on Slack (#development)
- **Self-service:** Check local logs and restart services
- **Documentation:** See `docs/local-development.md`

{{/if}}

---

## Deployment Complete

{{#if ENVIRONMENT == 'production'}}
**{{APP_NAME}}** has been deployed to **PRODUCTION**.

Remember to:
- Monitor the application for the next hour
- Update the deployment log
- Notify the team of successful deployment
{{else if ENVIRONMENT == 'staging'}}
**{{APP_NAME}}** has been deployed to **STAGING**.

Ready for QA testing and final validation before production release.
{{else if ENVIRONMENT == 'development'}}
**{{APP_NAME}}** is running in **DEVELOPMENT** mode.

Happy coding!
{{/if}}
