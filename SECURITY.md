# Security Policy

## Supported Versions

Only the latest commit on `main` is actively supported with security updates.

## Reporting a Vulnerability

If you discover a security vulnerability, please email kevin@premialab.com with a clear description and reproduction steps. Do not open a public issue for security-sensitive bugs.

We will acknowledge receipt within 48 hours and aim to provide a fix or mitigation within 14 days.

## Security Practices

- Secrets and credentials are loaded from environment variables, never committed to source.
- Dependencies are kept up to date via manual review and periodic upgrades.

> **Note:** API-key authentication is implemented and enabled by default (`SQT_AUTH_ENABLED=true`). Set `SQT_API_KEY` to a strong secret before starting the server; the host refuses to start when auth is enabled and no key is configured. TLS termination, container hardening, and dependency scanning are not yet implemented. Deploy behind a reverse proxy that provides TLS, and treat the in-memory audit store as non-durable.
