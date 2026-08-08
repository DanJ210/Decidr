# Getting Started

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/) and npm
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

When running in Development without Entra settings, the app retains the seeded
selected-user profile picker and in-memory/SQL Server demo behavior. Do not use
that fallback as an authentication mechanism in a deployed environment.

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
