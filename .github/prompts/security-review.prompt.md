---
agent: agent
description: Perform a focused security review on API and frontend changes.
---

Use the following references for standards:
- .github/instructions/copilot.instructions.md
- .github/instructions/code-review.instructions.md

Scope
- Backend: auth/authorization, input validation, EF queries, secrets, logging, exception handling, headers (HTTPS/CORS)
- Frontend: sensitive data exposure, injection vectors, permission directives, client-side validation

Checklist
1) Authentication & Authorization
- Are endpoints protected with [Authorize] or Permission filters where needed?
- Are permission names consistent and granular?

2) Input Validation
- DTO/DataAnnotation validation present?
- Server-side validation for critical paths?

3) Data Access
- LINQ with parameters used? No raw SQL?
- AsNoTracking on read-only queries?

4) Secrets & Config
- No secrets committed; uses appsettings with secure providers?

5) Error Handling & Logging
- Exceptions handled centrally; logs avoid PII?

6) CORS/HTTPS
- HTTPS enforced; CORS minimal and explicit?

7) Frontend Security
- No direct DOM injection; sanitize dynamic HTML?
- Avoid exposing sensitive info in source or network calls.

Output format
- Findings by file (severity: ✅/⚠️/❌)
- Concrete remediation items
- Overall risk rating and quick win list
