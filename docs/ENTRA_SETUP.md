# Microsoft Entra ID sign-in — setup guide

Vulscan supports **Microsoft Entra ID** (Azure AD) sign-in alongside local username/password sign-in. Only the
SDD security team signs in, so access is controlled in Entra by *app role assignment*: a person who holds none of
Vulscan's app roles is refused, even though they are a valid user in your tenant.

How it works (so you know what you are registering):

1. The browser redirects to Microsoft (authorization-code flow with **PKCE**, no client secret).
2. Microsoft returns an **ID token** to the SPA's `/login` page.
3. The SPA sends the ID token to `POST /api/v1/auth/entra`. The API validates it (RS256 signature against your
   tenant's published keys, issuer, audience = your client id, tenant id, expiry), maps the `roles` claim to a
   Vulscan role, provisions the user on first sign-in, and issues Vulscan's own short-lived session.

Nothing else depends on Microsoft tokens after that, and **no secrets are stored in Vulscan** for Entra users.

## 1. Register the application

Entra admin centre → **App registrations → New registration**

| Setting | Value |
|---|---|
| Name | `Vulscan` |
| Supported account types | **Accounts in this organizational directory only (single tenant)** |
| Redirect URI | leave blank for now |

From the **Overview** page note the **Application (client) ID** and **Directory (tenant) ID**.

## 2. Add the redirect URIs (Single-page application)

**Authentication → Add a platform → Single-page application**, then add one URI per environment. The path is
always `/login`, and it must match **exactly** (scheme, host, port):

| Environment | Redirect URI |
|---|---|
| Local development | `https://localhost:4271/login` |
| Production | `https://<your-vulscan-host>/login` |

Do **not** tick *Access tokens* or *ID tokens* under implicit grant — PKCE does not need them.

## 3. Create the app roles

**App roles → Create app role** (three times), or edit the manifest (**Manifest** blade) and replace `appRoles`
with the block below. The *Value* strings are what Vulscan matches; they are configurable (see §6).

```json
"appRoles": [
  {
    "allowedMemberTypes": ["User"],
    "description": "Full administration of Vulscan, including users, settings and the audit log.",
    "displayName": "Vulscan Administrator",
    "id": "be1b9996-7741-42f3-b1ac-1e13f1c066a6",
    "isEnabled": true,
    "value": "Vulscan.Admin"
  },
  {
    "allowedMemberTypes": ["User"],
    "description": "Run scans, triage vulnerabilities and configure projects.",
    "displayName": "Vulscan Security Analyst",
    "id": "311ca00b-64dd-4ad9-8cea-89579a666111",
    "isEnabled": true,
    "value": "Vulscan.SecurityAnalyst"
  },
  {
    "allowedMemberTypes": ["User"],
    "description": "Read-only access to Vulscan.",
    "displayName": "Vulscan Viewer",
    "id": "2fa5b7a3-11d6-4e8f-83f2-8d942424c688",
    "isEnabled": true,
    "value": "Vulscan.Viewer"
  }
]
```

## 4. Restrict who can sign in, and assign people

Entra admin centre → **Enterprise applications → Vulscan → Properties**

* **Assignment required? → Yes.** Users who are not assigned cannot even reach the consent step.

Then **Users and groups → Add user/group** and assign each person (or the SDD security team group) to the
role they should have.

> Assigning a *group* to an app role needs Entra ID **P1** or higher. On the free tier, assign users individually.

A person holding several roles gets the **highest** one (Admin > SecurityAnalyst > Viewer).

## 5. Make MFA mandatory (recommended, and what auditors will ask for)

Vulscan does not implement MFA itself — Entra does, which is stronger and already governed by your policy.
Create a **Conditional Access** policy: *Users* = the Vulscan assignees, *Target resources* = the Vulscan
application, *Grant* = **Require multifactor authentication** (and, if you use them, *Require compliant device*).

## 6. Configure Vulscan

Put these in `appsettings.Development.json` (git-ignored) or, in other environments, environment variables
(`__` is the separator). They contain no secrets — tenant and client IDs are public identifiers.

```json
{
  "Auth": {
    "Entra": {
      "Enabled": true,
      "TenantId": "<Directory (tenant) ID>",
      "ClientId": "<Application (client) ID>"
    }
  }
}
```

```bash
Auth__Entra__Enabled=true
Auth__Entra__TenantId=<Directory (tenant) ID>
Auth__Entra__ClientId=<Application (client) ID>
```

Only if you chose different role *Value* strings, override the mapping (Entra value → Vulscan role):

```json
"Auth": { "Entra": { "RoleMapping": { "SDD.Sec.Admin": "Admin", "SDD.Sec.Analyst": "SecurityAnalyst" } } }
```

Restart the API. The sign-in page now shows **Sign in with Microsoft**. The button only appears when both IDs are
valid GUIDs and `Enabled` is true.

## 7. Verify

1. Sign in with Microsoft as an assigned user. You land on the dashboard; **Users** shows the account with source
   *Microsoft Entra* and the role from Entra.
2. Sign in as a tenant user who is **not** assigned (if *Assignment required* is off for testing): Vulscan refuses
   with *"Your account has not been granted access"* and the **Audit log** records `Auth.EntraDenied`.
3. In **Audit log**, confirm `User.Provisioned` and `Auth.Login` (provider *Entra*).

## Behaviour to know about

* **Role changes in Entra take effect at the user's next Microsoft sign-in** (and are recorded as
  `User.RoleChanged`). A session lasts at most 12 hours (rotating refresh tokens).
* **Removing someone's assignment** stops them signing in again, but does not end an existing session. To cut
  access *immediately*, deactivate them on the **Users** page — that revokes their sessions at once.
* **Entra users have no Vulscan password.** The Users page hides *Change role* / *Reset password* for them; their
  role is managed only in Entra.
* Vulscan never silently links a Microsoft identity to an existing local account that merely has the same
  username or email (that would allow takeover via a changed Entra attribute). Such a sign-in is refused and
  audited; an administrator must resolve it.
* Accounts unused for 90 days are deactivated automatically (`Auth:DormantDays`, 0 disables).

## Keeping local sign-in

Local sign-in stays enabled by default so you are never locked out if Entra or the network path to Microsoft is
unavailable. Once Entra is proven, you can set `Auth:Local:Enabled=false` — but keep **one break-glass local
admin** reachable only from the management network, with a long unique password stored in your vault, and
document that procedure. (A disabled local sign-in cannot be used to recover from an Entra outage.)

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `AADSTS50011` — reply URL does not match | The redirect URI in Entra is not an exact match for `<origin>/login`, or it was added under *Web* instead of *Single-page application*. |
| `AADSTS700016` — application not found | Wrong `TenantId`/`ClientId`, or the app is single-tenant and you signed in from another directory. |
| `AADSTS50105` — user not assigned | *Assignment required* is on and the user has no role. Assign them. |
| "Your account has not been granted access" | The sign-in worked but the token has no recognised app role. Check the role *Value* strings against `Auth:Entra:RoleMapping`. |
| "Microsoft sign-in failed" | Token validation failed (expired, wrong audience/tenant). The exact reason is in the audit log (`Auth.EntraLoginFailed`). |
| "Could not reach Microsoft to verify the sign-in" | The API server cannot fetch `https://login.microsoftonline.com/<tenant>/v2.0/.well-known/openid-configuration` — check outbound access/proxy from the API host. |
