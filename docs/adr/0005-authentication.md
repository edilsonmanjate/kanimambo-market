# 0005 – JWT bearer authentication, with `dotnet user-jwts` locally

- **Status:** accepted
- **Date:** 2026-10-17

## Context

Back-office operations must be restricted, and the audit log must show who performed each one. The
project must run locally without any external identity provider or credentials.

## Decision

- The API uses the standard **JWT bearer** handler. Authorization uses the policy
  `AuthorizationPolicies.Backoffice`, which requires the role `backoffice`.
- **Locally:**
  1. Run `dotnet user-jwts create --project src/Kanimambo.Api --name <user> --role backoffice`. It stores
     a signing key in the developer's user secrets and adds the issuer and audiences to
     `appsettings.Development.json`.
  2. Restart the API, then send the printed token as `Authorization: Bearer …`.
- **Integration tests** override the validation parameters with a test signing key and mint tokens
  (`KanimamboWebApplicationFactory.CreateClientAs`).
- `ICurrentActor` (SharedKernel) gives application code the user name without depending on ASP.NET.
- In production, the same handler points to a real issuer (for example Entra ID or Keycloak) through
  configuration only.

## Consequences

- No custom authentication code, and nothing secret is committed.
- Anonymous requests to protected endpoints get `401`, and authenticated requests without the role get
  `403`, both as ProblemDetails (status code pages).
- Customer and seller identities (Ordering, Phase 3) will reuse the same scheme with other roles.
