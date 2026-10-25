---
name: talent-security
description: Security model - BFF login with OpenID Connect, pluggable role source and data scopes, permission matrix, field classification and masking, conflict of interest, and append-only audit (ADR-07). Use when adding any endpoint, query, report, export or screen that touches employee or talent data, when changing login, roles or permissions.
---

# Security

Source: `docs/talent-solution-architecture.md` ADR-07, ADR-12, §5; diagram 7.
**Talent data is very sensitive. Deny by default.**

## Authentication: BFF with OpenID Connect

- **BFF** (backend-for-frontend): `Talent.Host` runs the login. Tokens stay on the server.
- The browser gets only a **secure, HttpOnly, SameSite cookie**. Tokens never reach JavaScript.
- Login uses the ASP.NET Core **OpenID Connect handler** (authorization code flow + PKCE) to the client's identity provider.
- The login system is a **plug point**: Entra ID, AD FS or Keycloak. Change only `Authentication:Oidc:Authority` and `ClientId`.
- The React app calls the API on the **same origin**. State-changing calls need an **anti-forgery** token.
- We never store passwords.

## From login to current user

1. Map the login subject to an `Employee` through `ExternalIdMap` (system `SSO`).
2. Load roles through **`IRoleSource`** (plug point): `TalentManaged` or `IdpGroups`. The choice is a setting.
3. Build **`ICurrentUser`** once per request: user ID, employee ID, roles.
4. A user with no mapped employee gets **403**.

## Authorisation model

| Part | Values |
|---|---|
| Actions | `View`, `Edit`, `Approve`, `Calibrate`, `Export` |
| Permission | role × action × object, in a **versioned** matrix (data, not code) |
| Data scope | `Self`, `DirectReports`, `DeptTree`, `CycleAssigned`, `All` (plug point `IDataScopeResolver`, keyed) |
| Field class | `Public`, `Internal`, `TalentConfidential` |

`TalentConfidential` = nine-box, potential, successor status, risk of loss. It is masked unless the grant is explicit.
**Employees never see their own `TalentConfidential` fields.**

## Check order

1. Valid session? If not, return **401**.
2. Role allows the action? If not, return **403**.
3. Record inside the user's data scope? If not, return **403**.
4. Is it the user's own record (conflict of interest)? If yes, return **403**.
5. Run the logic. Write the audit entry. Mask fields on the server.

## Rules

- Enforce in the **service layer** and in **SQL filters**. The UI check is extra, never the only one.
- Apply the scope filter **in the query**, not after loading rows. `IDataScopeResolver` returns an EF Core predicate.
- **Mask on the server.** Never send hidden data to the browser.
- `Export` is its own permission and is always audited.
- **PostgreSQL row-level security** protects the reporting schema.
- Never log confidential fields (see `csharp-coding-standards`).

## Audit

- **Append-only**: no updates, no deletes. Before/after values for confidential writes and every export.
- Keep **7 years**, or the client's policy.

## Targets

TLS 1.2+, encryption at rest for confidential fields, OWASP Top 10, OWASP ZAP in CI, pen test before go-live.

## Tests for every endpoint (write them first)

- [ ] 401 without a session
- [ ] 403 without permission
- [ ] 403 outside the data scope
- [ ] 403 on the user's own record (for write, approve and calibrate)
- [ ] 403 or 400 on a state-changing call without an anti-forgery token
- [ ] Confidential fields masked without a grant
- [ ] Audit entry written
