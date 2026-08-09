# Getting Started

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22+](https://nodejs.org/) and npm
- SQL Server 2022, Azure SQL, or the in-memory fallback

## Backend Configuration

The backend reads connection settings from:

1. `backend/appsettings.json`
2. `backend/appsettings.Development.json`
3. `backend/appsettings.Development.local.json` (optional, ignored by git)

If `ConnectionStrings:DefaultConnection` is set, the app uses SQL Server or Azure SQL with EF Core.
If it is empty, the app falls back to in-memory storage.

For local SQL Server development, create a repository-root `.env` file:

```dotenv
MSSQL_SA_PASSWORD=<strong-local-password>
```

Start SQL Server with `docker compose up -d`, then create the ignored
`backend/appsettings.Development.local.json` file:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=decidr_dev;User Id=sa;Password=<strong-local-password>;Encrypt=True;TrustServerCertificate=True"
  }
}
```

For Azure SQL, provide `ConnectionStrings__DefaultConnection` through the
deployment environment or secret store instead of committing credentials.

## Entra External ID Configuration

Production and non-Development environments require Microsoft Entra External ID.
Configure the backend with:

```dotenv
Entra__Authority=https://<tenant>.ciamlogin.com/<tenant-id>/v2.0
Entra__Audience=<backend-api-application-client-id>
```

Configure the SPA with a local `frontend/.env.local` file (also ignored by git):

```dotenv
VITE_ENTRA_CLIENT_ID=<spa-application-client-id>
VITE_ENTRA_AUTHORITY=https://<tenant-id>.ciamlogin.com/<tenant-id>
VITE_ENTRA_API_SCOPE=api://<backend-api-application-id>/<scope-name>
```

Use the canonical tenant-ID authority for MSAL. The friendly tenant-subdomain
authority can return discovery metadata whose issuer uses the tenant-ID host;
MSAL 5 rejects that alias mismatch with `endpoints_resolution_error`.

The SPA signs users in with MSAL and silently attaches an access token when one
is available. Profile initialization starts an interactive redirect only when
MSAL reports that interaction is required; public reads remain available when
silent token acquisition fails. The backend validates a v2 token, requires the
delegated `access_as_user` scope, and maps stable `tid` plus `oid` claims to a
local Decidr profile. A first authenticated sign-in creates a local Member
profile; subsequent requests reuse that profile.

Entra authentication requires a non-empty `DefaultConnection`. Startup fails
when Entra is configured without persistent SQL Server or Azure SQL storage.

### Disable Entra for Local Development

Entra configuration is independent in the backend and SPA, so disable both
halves before using the selected-user development workflow.

In the ignored `backend/appsettings.Development.local.json`, set both backend
Entra values to empty strings while preserving any existing connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<existing local value, or empty for in-memory storage>"
  },
  "Entra": {
    "Authority": "",
    "Audience": ""
  }
}
```

In the ignored `frontend/.env.local`, remove the three Entra entries or leave
them empty:

```dotenv
VITE_ENTRA_CLIENT_ID=
VITE_ENTRA_AUTHORITY=
VITE_ENTRA_API_SCOPE=
```

Restart both `dotnet run` and `npm run dev` after changing these settings. Vite
reads its environment variables only at startup. In this mode, the SPA restores
the seeded/local user selector and sends `X-Dev-User-Id`; the backend accepts that
header only in Development when Entra is not configured. The conditional
controller authorization convention is also omitted so this development identity
flow can reach protected actions.

This fallback is not a deployment authentication mechanism. Non-Development
startup requires Entra authority and audience settings, and the development user
header is rejected outside Development or whenever Entra is configured. To
re-enable Entra locally, restore all five settings and restart both processes.

### Azure Deployment

The selected hosting design is a single Azure App Service serving both the Vue
SPA and ASP.NET Core API, backed by Azure SQL Database, private Blob Storage, and
Microsoft Entra External ID. Live tenant IDs, application IDs, resource names,
hostnames, deployment IDs, and migration state are intentionally excluded from
this public repository. Keep that operational inventory in a private runbook or
retrieve it from the protected GitHub `production` environment and Azure portal.

The production environment must provide:

- an External ID tenant with separate SPA and API registrations;
- the delegated API scope `access_as_user` granted to the SPA;
- `https://<app-service-host>/auth/callback` as a production SPA redirect URI;
- an App Service system-assigned managed identity;
- an Azure SQL contained user for the App Service identity;
- a private evidence Blob container; and
- Defender for Storage on-upload malware scanning with Blob index result tags.

The `/auth/callback` frontend route completes the MSAL redirect flow and returns
the user to the route where authentication started. It is distinct from the
existing backend `/api/auth/me` endpoint, which maps an authenticated token to a
Decidr profile. Local External ID sign-in, callback handling, token exchange, and
local profile mapping have been verified end to end.

The API security boundary has also been verified locally: authenticated profile
requests succeed, anonymous and spoofed-development-header mutations are rejected,
controller endpoints require `access_as_user` by default in Entra environments,
and pending case reads are restricted to participants, invitees, and moderators.
API throttling plus baseline response security headers are enabled. Evidence
uploads now use the private Blob container and an application-controlled download
endpoint. The endpoint fails closed while Defender scanning is pending and for
malicious, failed, unscanned, or unknown results. The Development-only local file
provider treats files that pass structural validation as clean so local work does
not depend on Azure Defender.

Configure these App Service production settings with environment-specific values:

```text
ASPNETCORE_ENVIRONMENT=Production
Entra__Authority=https://<tenant-subdomain>.ciamlogin.com/<tenant-id>/v2.0
Entra__Audience=<api-application-client-id>
EvidenceStorage__BlobServiceUri=https://<storage-account>.blob.core.windows.net/
EvidenceStorage__ContainerName=<private-evidence-container>
ConnectionStrings__DefaultConnection=<managed-identity Azure SQL connection string>
```

These values are identifiers, not credentials. In Azure, `DefaultAzureCredential`
uses the Web App system-assigned managed identity. Outside Development the app
fails startup if either setting is missing. In Development, omitting them selects
the ignored `backend/App_Data/case-evidence` provider; setting them allows local
Azure testing with a developer identity obtained through standard Azure tooling.

### Evidence malware scanning

Enable Defender for Storage with on-upload malware scanning, an appropriate
monthly cap, Blob index scan-result tags, and a remediation policy for malicious
files. The API deliberately refuses access when the result tag is absent or is
anything other than `No threats found`.

Also configure the following operational controls:

1. Confirm the `Microsoft.EventGrid` resource provider is registered. Defender
  creates an Event Grid system topic that triggers scans; do not delete it.
2. Set a monthly scan cap appropriate for this test deployment. The service
  defaults to 10,000 GB and can exceed a configured cap by up to 20 GB.
3. Enable Defender's built-in soft deletion of malicious blobs and set a retention
  period suitable for investigation and false-positive recovery.
4. Keep Defender security alerts enabled. Add Log Analytics or an Event Grid
  result destination before production if a tamper-resistant audit trail or
  automated quarantine workflow is required. Blob index tags are an application
  access gate, but users with tag-write permission can alter them.
5. Do not add scan exclusions for the `case-evidence/` container. Excluded,
  oversized, timed-out, or otherwise unscanned files remain unavailable by design.

Validate the scan contract with a normal allowed file. It should initially return
HTTP `423` and become downloadable only after Defender applies the exact
`No threats found` tag. Use EICAR only from a controlled test source; do not
weaken endpoint protection or use real malware. A detected file must remain
unavailable with HTTP `410` and follow the configured remediation policy.

Disable Shared Key authorization and public Blob access, require HTTPS with TLS
1.2 or later, and scope `Storage Blob Data Contributor` to the private evidence
container. Add a custom read-only role containing only the Blob `tags/read` data
action so the application can evaluate Defender results without altering them.

### CI/CD and controlled migrations

The [CI/CD workflow](../.github/workflows/ci-cd.yml) validates pull requests and
deploys `main`. Pull requests restore locked npm and NuGet dependencies, build the
Vue SPA and ASP.NET Core backend, run backend tests, and audit transitive NuGet
packages for known vulnerabilities.

Pushes to `main` produce one immutable release artifact containing:

- the App Service ZIP package;
- a pinned EF Core Linux migration bundle;
- an idempotent SQL migration script for approval review; and
- SHA-256 checksums verified before production changes begin.

The `production` GitHub environment accepts only `main`, requires approval by
`DanJ210`, and does not allow administrator bypass. Review `migrations.sql` in the
workflow artifact before approving the deployment. After approval, GitHub
authenticates to Azure through workload identity federation; no Azure client
secret or publish profile is stored. The workflow opens a runner-specific Azure
SQL firewall rule, applies the migration bundle, removes the rule even on failure,
deploys the exact package built earlier, and checks the public root, case list,
and anonymous authentication boundary. Production jobs are serialized so
migrations and deployments cannot overlap.

The deployment identity uses a federated credential restricted to this
repository's `production` environment. Azure management access is limited to
`Website Contributor` on the target Web App and a custom role containing only
SQL server/firewall-rule read, write, and delete actions on the target SQL server.
Inside the production database, its contained user has `db_ddladmin`,
`db_datareader`, and `db_datawriter`; it is not `db_owner`.

Create every schema change as a reviewed EF Core migration and merge it with the
application code that consumes it. Production startup never applies migrations.
If migration fails, deployment does not run. If migration succeeds but deployment
fails, rerun the same workflow: the EF bundle is idempotent and skips migrations
already recorded in `__EFMigrationsHistory`.

Application client IDs, authority URLs, and resource names are public identifiers
and may be version-controlled. Keep credentials and connection strings in managed
identity, workload identity federation, ignored local settings, or Azure App
Service configuration; never commit secrets.

## Running the Backend

```bash
cd backend
dotnet run
```

The API starts on `https://localhost:5001` (or the port shown in the terminal).  
Swagger UI is available at `https://localhost:5001/swagger` in development.

## Running the Frontend (Development)

```bash
cd frontend
npm install
npm run dev
```

Vite starts a dev server (typically `http://localhost:5173`) and proxies `/api` requests to the backend.

## Running the Full Stack (Production Build)

The backend serves the compiled frontend as static files from `wwwroot`. To build and serve everything together:

```bash
cd frontend
npm run build
# Copy dist output into backend/wwwroot, then:
cd ../backend
dotnet run
```

The app is then fully accessible at the backend URL.

## Run Tests

Run the backend authentication-boundary regression tests from the repository root:

```bash
dotnet test backend.Tests/backend.Tests.csproj
```

## Seed Data

On first run against an empty database, EF Core migrations are applied and the app seeds two debate cases and five users (four Members, one Moderator).

In in-memory fallback mode, the same seed-style development dataset is loaded at startup and resets on each restart.

### Seeded Users

| Display Name | Username | Role | ID |
|---|---|---|---|
| Alex | alex_t | Member | `89f651a2-d6ad-43b6-a2d8-209da7599387` |
| Jordan | jordan_r | Member | `03a431ca-7354-43b8-b8f3-cf95f65f83b4` |
| Casey | casey_l | Member | `c421252a-2976-4f97-9fbf-e9f848f066f8` |
| Morgan | morgan_p | Member | `8af01b3a-d4b4-4954-9805-6dc58a2f0e0c` |
| Sam | sam_k | Moderator | `e1d2e6fb-c79f-4d18-8dd9-c9507487e2c4` |
