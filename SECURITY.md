# Security Policy

## Supported Versions

Only the latest commit on `main` is actively supported with security updates.

## Reporting a Vulnerability

If you discover a security vulnerability, please email kevin@premialab.com with a clear description and reproduction steps. Do not open a public issue for security-sensitive bugs.

We will acknowledge receipt within 48 hours and aim to provide a fix or mitigation within 14 days.

## Security Practices

- Secrets and credentials are loaded from environment variables, never committed to source.
- Dependencies are kept up to date via manual review and periodic upgrades.

> **Note:** Authentication, TLS termination, and container hardening are not yet implemented in this repository. Deploy behind a reverse proxy that provides TLS and access control.
