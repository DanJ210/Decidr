# Decidr Copilot Instructions

## High-Level Overview

**Decidr** is a community debate platform where users submit two-sided arguments and vote on winners. The app awards reward badges for participation and voting. It's a full-stack single-page application (SPA) with an ASP.NET Core 10 backend API and a Vue 3 + TypeScript frontend.

### Repository Size & Structure
- **Size**: Small to medium (~500 KB codebase)
- **Type**: Full-stack web application
- **Backend**: ASP.NET Core 10 (C#, .NET 10 SDK required)
- **Frontend**: Vue 3, TypeScript, Vite build tool
- **Database**: Azure SQL / SQL Server 2022 (optional; in-memory fallback available)
- **State Management**: Pinia
- **HTTP Client**: Axios
- **Main Root Files**: `docker-compose.yml` (SQL Server), `.github/` (agents)

---

## Build & Validation Instructions

### Prerequisites
Always verify these are installed before running any commands:
- **.NET 10 SDK**: Required for backend compilation and running
- **Node.js 18+**: Required for frontend build and npm package management
- **Docker & Docker Compose** (optional but recommended): Simplest way to run SQL Server 2022 locally
- **SQL Server 2022** (optional alternative): Only needed if NOT using Docker; Azure SQL and the in-memory fallback are also supported

### Bootstrap & Setup

#### Database Bootstrap (SQL Server via Docker Compose)
**Recommended approach** — Set up SQL Server without manual installation:

1. Create a `.env` file in the repository root with the required password:
```bash
echo "MSSQL_SA_PASSWORD=your_secure_password_here" > .env
```

2. Start the SQL Server container:
```bash
docker-compose up -d
```
- **Service**: SQL Server 2022 runs on `localhost:1433`
- **Database**: `decidr_dev`
- **User**: `sa`
- **Time to Start**: ~30 seconds
- **Precondition**: Docker and Docker Compose installed, `.env` file with a strong `MSSQL_SA_PASSWORD` set
- **Postcondition**: SQL Server is running; verify with `docker compose ps`

3. Configure backend connection string in `backend/appsettings.Development.local.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<local SQL Server connection string>"
  }
}
```
- **Notes**: Create this file locally (git-ignored); it overrides `appsettings.Development.json`

4. Stop SQL Server when finished:
```bash
docker-compose down
```

**Alternative**: Configure Azure SQL through `ConnectionStrings__DefaultConnection`, or leave the connection string empty to use in-memory storage.

#### Backend Bootstrap
```bash
cd backend
dotnet restore
```
- **Precondition**: .NET 10 SDK must be installed
- **Postcondition**: NuGet packages are restored; `bin` and `obj` directories are populated
- **Notes**: Always run this after changes to `.csproj` file or when dependencies are added

#### Frontend Bootstrap
```bash
cd frontend
npm install
```
- **Precondition**: Node.js 18+ must be installed
- **Postcondition**: `node_modules/` directory is created with all dependencies
- **Notes**: Run this every time `package.json` is modified. If `package-lock.json` conflicts occur, delete `node_modules/` and re-run

### Build Commands

#### Build Frontend (Production)
```bash
cd frontend
npm run build
```
- **Output**: Compiled files written directly to `backend/wwwroot/`
- **Time**: ~30 seconds
- **Precondition**: `npm install` must be completed first
- **Postcondition**: Vue TypeScript compilation via `vue-tsc`, followed by Vite bundling
- **Notes**: Always run before integrating frontend with backend for production deployment

#### Typecheck Frontend
```bash
cd frontend
npx vue-tsc -b
```
- **Time**: ~10 seconds
- **Validates**: TypeScript types across all `.vue` and `.ts` files
- **Notes**: Run this to catch TypeScript errors before full build; included in `npm run build`

### Run Commands

#### Run Backend (Development)
```bash
cd backend
dotnet run
```
- **URL**: `http://localhost:5066` (default `http` profile) or `https://localhost:7277` (HTTPS profile)
- **Swagger UI**: Available at `http://localhost:5066/swagger` (development only)
- **Time to Start**: ~5 seconds
- **Precondition**: .NET 10 SDK installed, `dotnet restore` run
- **Notes**: On first run, EF Core migrations are applied automatically. If `ConnectionStrings:DefaultConnection` is empty or whitespace, in-memory storage is used with seeded test data. If it is non-empty and SQL Server is unavailable, startup will fail.

#### Run Frontend (Development)
```bash
cd frontend
npm run dev
```
- **URL**: Typically `http://localhost:5173`
- **Features**: Hot module replacement (HMR), automatic browser refresh on changes
- **Time to Start**: ~5 seconds
- **Proxy**: `/api/*` requests are proxied to backend at `http://localhost:5066`
- **Precondition**: `npm install` completed, backend running
- **Notes**: Always verify the terminal output for the actual URL

#### Run Full Stack (Production)
```bash
cd frontend
npm run build
cd ../backend
dotnet run
```
- **Single URL**: Backend serves entire app at `http://localhost:5066`
- **Frontend served**: As static files from `wwwroot/`
- **Client-side routing**: Backend fallback to `index.html` enables Vue Router navigation

### Test Commands

Backend regression tests are in `backend.Tests/` and can be run with:

```bash
dotnet test backend.Tests/backend.Tests.csproj
```

The current suite covers authenticated actor precedence and the Development-only
selected-user header boundary.

### Database & Persistence

#### SQL Server Setup with docker-compose.yml

The `docker-compose.yml` file at the repository root provides a SQL Server 2022 development instance. Azure SQL remains the intended hosted database.

**docker-compose.yml Configuration:**
```yaml
services:
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    ports:
      - "1433:1433"
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_PID: Developer
      MSSQL_SA_PASSWORD: (from .env)
    volumes:
      - db_data:/var/opt/mssql
```

**Quick Start:**
```bash
# Step 1: Set password in .env
echo "MSSQL_SA_PASSWORD=your_password" > .env

# Step 2: Start SQL Server
docker-compose up -d

# Step 3: Verify it's running
docker ps

# Step 4: Configure backend connection string (see Bootstrap section above)

# Step 5: Start backend (migrations run automatically)
cd backend
dotnet run

# Step 6: View logs (optional)
docker-compose logs -f db

# Step 7: Stop when done
docker-compose down
```

**Verification:**
- Confirm the container is running: `docker compose ps`
- Connect with SQL Server tooling at `localhost,1433` using the `sa` account
- Health status: `docker-compose ps` (Status should show "Up")

#### Database Migrations (EF Core)
```bash
cd backend
dotnet ef database update
```
- **Applies**: Pending EF Core migrations to Azure SQL or SQL Server
- **Precondition**: `dotnet-ef` tool installed (`dotnet tool install --global dotnet-ef`), `ConnectionStrings:DefaultConnection` is configured, and the database is reachable
- **Automatic**: Migrations run automatically on app startup if not already applied
- **Notes**: Safe to run multiple times (idempotent)

#### Database Fallback (In-Memory)
If Docker is not available or you prefer in-memory storage:
- Create `backend/appsettings.Development.local.json` with an empty `DefaultConnection` to override the tracked config:
  ```json
  { "ConnectionStrings": { "DefaultConnection": "" } }
  ```
- Backend automatically switches to `InMemoryCommunityCourtService`
- Data is reset on app restart; this is by design for development
- Seeded with 5 users and 2 test cases on startup

#### Seed Data
- **SQL Server mode**: Seeded only if database is empty (first migration)
- **In-memory mode**: Seeded automatically at startup with same test data
- **Seeded Users**: Alex, Jordan, Casey, Morgan (Members), Sam (Moderator)
- **Seeded Cases**: Two debate examples for testing

**Reset Database (local SQL Server):**
```bash
docker-compose down -v          # Remove volume to reset database
docker-compose up -d            # Start fresh
cd backend && dotnet run        # Migrations and seed data run automatically
```

### Common Workarounds & Issues

#### SSL Certificate Errors
If running locally, you may see SSL certificate warnings. To trust the local dev certificate:
```bash
dotnet dev-certs https --trust
```
- **Windows**: Adds certificate to local trust store
- **macOS/Linux**: Follow on-screen instructions

#### Port Already in Use
If port 5066 is already bound:
- Change port in `backend/Properties/launchSettings.json` (look for `"http"` profile)
- Restart backend with new port

#### Vite Proxy Issues
If frontend cannot reach backend API:
- Ensure backend is running on the port specified in `vite.config.ts`
- Check that `http://localhost:5066/api/` is accessible directly in a browser
- Clear Vite cache: `rm -rf frontend/node_modules/.vite`

#### Database Connection Fallback
The backend uses `InMemoryCommunityCourtService` **only when `ConnectionStrings:DefaultConnection` is empty or whitespace**. If the connection string is set and SQL Server is unavailable, the app will throw an exception at startup (EF Core runs `db.Database.Migrate()` in Development). To use in-memory storage:
- Override `ConnectionStrings:DefaultConnection` to empty in `backend/appsettings.Development.local.json` (see Bootstrap section)
- Data is reset on app restart; this is by design for development

---

## Project Layout & Architecture

### Root Directory Structure
```
.
├── backend/                    # ASP.NET Core 10 application
│   ├── Controllers/            # REST endpoints
│   ├── Data/                   # EF Core DbContext
│   ├── Models/                 # C# records and enums
│   ├── Services/               # Business logic (ICommunityCourtService)
│   ├── appsettings.json        # Default config
│   ├── appsettings.Development.json
│   ├── appsettings.Development.local.json  # Local overrides (git-ignored)
│   ├── backend.csproj          # .NET project file
│   ├── backend.http            # REST Client requests for testing
│   └── Program.cs              # Service registration and middleware setup
│
├── frontend/                   # Vue 3 + TypeScript application
│   ├── src/
│   │   ├── components/         # Reusable Vue components
│   │   ├── stores/             # Pinia stores (auth, court, friends, rewards)
│   │   ├── views/              # Page-level components (Home, Case, Create, etc.)
│   │   ├── router/             # Vue Router configuration
│   │   ├── App.vue             # Root component
│   │   └── main.ts             # Frontend entry point
│   ├── package.json            # npm dependencies and scripts
│   ├── tsconfig.json           # TypeScript configuration
│   ├── vite.config.ts          # Vite build configuration
│   └── vite.env.d.ts           # Vite env type definitions
│
├── .github/
│   └── agents/                 # Custom Copilot agents (dotnet-vue-scaffolder)
│
├── docs/                       # Project documentation
│   ├── README.md               # Overview and tech stack
│   ├── architecture.md         # Detailed system design
│   ├── getting-started.md      # Local setup guide
│   ├── api-reference.md        # REST endpoints and schemas
│   ├── data-models.md          # C# ↔ TypeScript model mapping
│   └── frontend.md             # Frontend structure and UX
│
├── docker-compose.yml          # SQL Server service for development
└── .gitignore                  # Excludes node_modules, appsettings.*.local.json, etc.
```

### Key Configuration Files

| File | Purpose |
|------|---------|
| `backend/backend.csproj` | NuGet dependencies: EF Core SQL Server, Swashbuckle |
| `backend/Program.cs` | Middleware registration, response compression, static file serving |
| `frontend/package.json` | npm dependencies: Vue, Pinia, Axios, Vite |
| `frontend/vite.config.ts` | Proxy rules, dev server config (`http://localhost:5066/api`) |
| `docker-compose.yml` | SQL Server 2022 configuration (port 1433) |

### Validation & CI Checks

**Currently**: No GitHub Actions workflows are configured. The repository has no automated CI/CD pipeline.

When CI is added, document the following:
1. What linters run (e.g., ESLint, Roslyn analyzers)
2. What builds are triggered (dotnet build, npm run build)
3. What tests run (when test suite is added)
4. Expected time to complete the full validation suite

**Manual Validation Steps** (to replicate CI when added):
```bash
# Backend: Restore and compile
cd backend
dotnet restore
dotnet build

# Frontend: Install and build
cd frontend
npm install
npm run build

# Frontend: TypeScript check
cd frontend
npx vue-tsc -b

# Optional: Verify the backend is running (list cases endpoint)
curl -X GET http://localhost:5066/api/cases
```

---

## Architecture Highlights

### Backend (ASP.NET Core 10)

**Controllers** expose REST endpoints:
- `CasesController` → `GET /api/cases`, `POST /api/cases`, etc.
- `UsersController` → `GET /api/users` (list all), `GET /api/users/{id}/rewards`, `GET /api/users/{id}/friends`, `GET /api/users/{id}/friend-requests`, `GET /api/users/{id}/sent-requests`, `GET /api/users/{id}/invitations`
- `FriendsController` routes:
  - `POST /api/friends/request`
  - `POST /api/friends/{requestId}/accept`
  - `POST /api/friends/{requestId}/decline`
  - `POST /api/friends/remove`

**Services** contain business logic:
- `ICommunityCourtService` interface defines operations
- `EfCoreCourtService` implements with EF Core + Azure SQL / SQL Server
- `InMemoryCommunityCourtService` provides fallback for development

**Database** (EF Core):
- Entities: `UserEntity`, `CaseEntity`, `CaseVoteEntity`, `UserRewardEntity`, `FriendRequestEntity`
- Migrations in `Data/Migrations/` (auto-applied on startup)

**Response Compression**: Brotli and Gzip enabled for JSON and static files.

### Frontend (Vue 3 + TypeScript)

**Stores** (Pinia):
- `auth` — selected user and authentication state
- `court` — cases, voting, case management
- `friends` — friend requests and connections
- `rewards` — earned badges

**Router** handles navigation:
- `/` — Home page (public cases, invitations)
- `/cases/:id` — Individual case view
- `/cases/new` — Create new case
- `/friends` — Friend connections
- `/rewards` — Badge achievements

**HTTP Client** (Axios):
- All API calls prefixed with `/api/`
- Development: Proxied by Vite to `http://localhost:5066`
- Production: Backend serves static files and handles API directly

---

## Dependencies & Versions

### Backend
- **Framework**: .NET 10.0
- **Microsoft.EntityFrameworkCore**: 8.0.27
- **Microsoft.EntityFrameworkCore.SqlServer**: 8.0.27
- **Swashbuckle.AspNetCore**: 6.6.2 (Swagger/OpenAPI)

### Frontend
- **Vue**: 3.5.34
- **TypeScript**: ~6.0.2
- **Vite**: 8.0.12
- **Pinia**: 3.0.4
- **Vue Router**: 4.6.4
- **Axios**: 1.16.1

### External Services
- **Azure SQL**: Hosted relational database
- **SQL Server**: 2022 container for local development (optional; in-memory fallback works)
- **Docker**: Optional; only needed for the local SQL Server container

---

## Critical Design Notes

1. **No Authentication Layer**: The frontend stores `selectedUserId` in `localStorage`. Backend requests include `userId` in the body (POST/PUT) or as a query parameter (GET). The backend validates user existence.

2. **Verdict Computation**: Vote counts are recalculated on each case read (not stored). This ensures consistency.

3. **Voting Rule**: Users can vote once per case. Subsequent vote attempts are rejected with "You have already voted on this case."

4. **Case Invitation Flow**: Cases start in `Pending` state. The invited user must accept or decline to move to `Open` or `Closed`.

5. **Friend System**: Only accepted friendships (not pending requests) can be invited to new cases.

6. **Reward Badges**: Automatically awarded on participation, voting, and case closure. Duplicates prevented via `(userId, badgeCode, sourceType, sourceId)` unique constraint.

---

## Trust These Instructions

When working on Decidr:
- **Trust these instructions first**: They document the exact build steps, dependencies, and workarounds
- **Search the codebase only if**: The information here is incomplete, contradicts what you find, or doesn't address your specific task
- **Always validate changes** by running the full build and startup sequence after making code changes
- **For new patterns** not documented here, search `docs/` and the relevant source files to understand design intent before implementing
