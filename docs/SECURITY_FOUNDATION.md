# Security foundation — what changed and how to operate it

This release hardens authentication, access control, audit logging and secret storage. Read **Before you restart**
first: a few behaviours change the first time the new build runs.

## Before you restart

1. **Restart the API.** An instance started from an older build keeps running the old code. The database is shared,
   and the new build migrates it and encrypts stored credentials, which the old build cannot read.
2. **Database migrations are applied automatically in Development** (`ASPNETCORE_ENVIRONMENT=Development`). A
   database that was created by the old `EnsureCreated` behaviour (no migration history) is *baselined once*, then
   migrated — your data is kept. In other environments set `Database:MigrateOnStartup=true`, or run
   `dotnet ef database update` as a deployment step.
3. **The seeded admin must change its password at first sign-in.** `admin` / `Vulscan@2025` is public (README), so
   Vulscan now refuses to serve anything but the change-password page until it is replaced.
4. **Stored credentials are now encrypted** (Azure DevOps/Git usernames + passwords/PATs, and the SMTP password).
   Existing plaintext values are encrypted on first start. **Back up the key ring** (below) — without it those
   credentials cannot be decrypted.
5. **`Jwt:SecretKey` is now validated at startup.** The API refuses to start if it is the unexpanded
   `${JWT_SECRET_KEY}` placeholder (which `appsettings.json` ships) or shorter than 32 bytes. Previously it silently
   signed tokens with that publicly known string.

## The key ring (encryption of stored secrets)

Secrets are encrypted with ASP.NET Core Data Protection (AES-256 + HMAC-SHA256). The key ring is stored **outside
the database**, so a database backup alone cannot reveal credentials.

| Setting | Default | Notes |
|---|---|---|
| `DataProtection:KeysPath` | `~/.vulscan/keys` | Directory holding the key ring (owner-only permissions are applied on macOS/Linux). In containers, mount a persistent, backed-up volume here. |
| `DataProtection:CertificatePath` | — | PKCS#12 certificate used to encrypt the key ring *at rest*. **Recommended outside development.** |
| `DataProtection:CertificatePassword` | — | Supply from a secret store, never from a committed file. |

* **Back the key ring up separately from the database**, and restore both together. If the key ring is lost,
  every stored credential must be re-entered (scans fail with a clear "could not be decrypted" message until then).
* Keys rotate automatically every 90 days; old keys are retained so existing secrets keep decrypting.
* Run every API replica against the **same** key ring.

## Sign-in, sessions and lockout

| Behaviour | Value | Setting |
|---|---|---|
| Access token lifetime | 30 min | `Jwt:ExpirationMinutes` |
| Session (refresh) lifetime, absolute | 12 h | `Jwt:RefreshTokenHours` |
| Refresh tokens | single-use, rotated; replaying a used token ends the whole session | — |
| Lockout | 5 failed attempts → locked 15 min | `Auth:Lockout:MaxFailedAttempts` / `DurationMinutes` |
| Credential-endpoint rate limit | 10 requests/min per client IP | `Security:AuthRateLimitPerMinute` |
| Password policy | ≥ 12 chars; upper, lower, digit, symbol; no username/email parts; no published/common passwords | (SECURITY.md) |
| Dormant accounts | deactivated after 90 days without sign-in (never the last admin) | `Auth:DormantDays` (0 = off) |
| Access review overdue after | 90 days | `Auth:AccessReviewIntervalDays` |
| Local sign-in | enabled | `Auth:Local:Enabled` |
| Microsoft Entra sign-in | disabled until configured | `Auth:Entra:*` — see [ENTRA_SETUP.md](ENTRA_SETUP.md) |

Failure messages are deliberately uniform ("Invalid username or password.") so the endpoint cannot be used to find
out which accounts exist; the precise reason is in the audit log.

## Audit trail

Admin-only: **Audit Log** page, `GET /api/v1/audit-logs`, CSV export. It records:

* every sign-in, failed sign-in, lockout, refresh failure/reuse, sign-out, password change;
* user creation, role changes, deactivation/reactivation, unlock, password reset, access reviews, automatic
  deactivation of dormant accounts;
* every state-changing API call (POST/PUT/PATCH/DELETE) and every export/download, with outcome
  (`Success` / `Failure` / `Denied`);
* authorization denials (a signed-in user attempting something their role does not allow).

Request bodies are **never** recorded (they can carry credentials). Audit writes use their own database connection,
so a failing request cannot suppress its own record, and an audit failure cannot break a request.

> There is no retention/purge job yet; the table grows indefinitely. Decide a retention period with your GRC team
> before production, and archive to your SIEM if required.

## Deployment notes

* **Production client → API.** A production build calls the relative path `/api/v1` (previously the production
  build wrongly contained `https://localhost:5071`). Serve the SPA and reverse-proxy `/api/` to the API from the
  same origin.
* **Behind a reverse proxy**, set `Network:TrustForwardedHeaders=true` and list the proxy in `Network:KnownProxies`,
  otherwise audit records and the rate limiter see the proxy's address for every client.
* **Multiple API instances.** `BackgroundJobs:Enabled=false` turns off the scan worker and schedulers on an
  instance (so only designated instances process the queue).
* Responses carry `X-Content-Type-Options`, `X-Frame-Options: DENY`, a restrictive CSP and `Cache-Control: no-store`;
  HSTS is sent outside Development. The `Server` header is removed.
* All timestamps are UTC and now serialised with a trailing `Z`. (Before, they had no offset, so browsers showed
  every time shifted by the viewer's UTC offset — 4 hours in the UAE.)

## Tests

`dotnet test server/tests/Vulscan.Tests` runs the suite. SQL Server integration tests (migrations, baselining)
are skipped unless `VULSCAN_TEST_SQL` holds a connection string to a server where the tests may create and drop
scratch databases.
