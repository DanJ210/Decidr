# Decidr Copilot Instructions

## High-Level Overview

**Decidr** is a community debate platform where users submit two-sided arguments and vote on winners. The app awards reward badges for participation and voting. It's a full-stack single-page application (SPA) with an ASP.NET Core 8 backend API and a Vue 3 + TypeScript frontend.

### Repository Size & Structure
- **Size**: Small to medium (~500 KB codebase)
- **Type**: Full-stack web application
- **Backend**: ASP.NET Core 8 (C#, .NET 8 SDK required)
- **Frontend**: Vue 3, TypeScript, Vite build tool
- **Database**: PostgreSQL 16 (optional; in-memory fallback available)
- **State Management**: Pinia
- **HTTP Client**: Axios
- **Main Root Files**: `docker-compose.yml` (PostgreSQL), `.github/` (agents)

---

## Build & Validation Instructions

### Prerequisites
Always verify these are installed before running any commands:
- **.NET 8 SDK**: Required for backend compilation and running
- **Node.js 18+**: Required for frontend build and npm package management
- **PostgreSQL 16** (optional): Only needed if using persistent database; app falls back to in-memory storage

### Bootstrap & Setup

#### Backend Bootstrap
```bash
cd backend
dotnet restore
```
- **Precondition**: .NET 8 SDK must be installed
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
- **Output**: Compiled files in `frontend/dist/`
- **Time**: ~30 seconds
- **Precondition**: `npm install` must be completed first
- **Postcondition**: Vue TypeScript compilation via `vue-tsc`, followed by Vite bundling
- **Notes**: Always run before integrating frontend with backend for production deployment

#### Typecheck Frontend
```bash
cd frontend
vue-tsc -b
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
- **URL**: `https://localhost:5001`
- **Swagger UI**: Available at `https://localhost:5001/swagger` (development only)
- **Time to Start**: ~5 seconds
- **Precondition**: .NET 8 SDK installed, `dotnet restore` run
- **Notes**: On first run, EF Core migrations are applied automatically. If `ConnectionStrings:DefaultConnection` is empty, in-memory storage is used with seeded test data.

#### Run Frontend (Development)
```bash
cd frontend
npm run dev
```
- **URL**: Typically `http://localhost:5173`
- **Features**: Hot module replacement (HMR), automatic browser refresh on changes
- **Time to Start**: ~5 seconds
- **Proxy**: `/api/*` requests are proxied to backend at `https://localhost:5001`
- **Precondition**: `npm install` completed, backend running
- **Notes**: Always verify the terminal output for the actual URL

#### Run Full Stack (Production)
```bash
cd frontend
npm run build
# Move dist contents to backend/wwwroot (or let build process handle it)
cd ../backend
dotnet run
```
- **Single URL**: Backend serves entire app at `https://localhost:5001`
- **Frontend served**: As static files from `wwwroot/`
- **Client-side routing**: Backend fallback to `index.html` enables Vue Router navigation

### Test Commands

**Currently**: No test suite configured. If tests are added, they will use:
- **Backend**: xUnit or MSTest (typical for .NET Core)
- **Frontend**: Vitest or Jest (typical for Vue 3)

Document test commands here when they are added.

### Database & Persistence

#### With Docker Compose (PostgreSQL)
```bash
docker-compose up -d
```
- **Service**: PostgreSQL 16 on port 5433 (mapped from container's 5432)
- **Credentials**: User `decidr`, DB `decidr_dev` (configure `POSTGRES_PASSWORD` env var first)
- **Connection String**: `Host=localhost;Port=5433;Database=decidr_dev;Username=decidr;Password=...`
- **Precondition**: Docker and Docker Compose installed

#### Database Migrations (EF Core)
```bash
cd backend
dotnet ef database update
```
- **Applies**: Pending EF Core migrations to PostgreSQL
- **Precondition**: `ConnectionStrings:DefaultConnection` is configured and database is running
- **Notes**: Migrations run automatically on app startup if not already applied

#### Seed Data
- **In-memory mode**: Seeded automatically at startup with 5 users and 2 test cases
- **PostgreSQL mode**: Seeded only if database is empty (first migration)
- **Seeded Users**: Alex, Jordan, Casey, Morgan (Members), Sam (Moderator)

### Common Workarounds & Issues

#### SSL Certificate Errors
If running locally, you may see SSL certificate warnings. To trust the local dev certificate:
```bash
dotnet dev-certs https --trust
```
- **Windows**: Adds certificate to local trust store
- **macOS/Linux**: Follow on-screen instructions

#### Port Already in Use
If port 5001 is already bound:
- Change port in `backend/Properties/launchSettings.json` (look for `"https"` section)
- Restart backend with new port

#### Vite Proxy Issues
If frontend cannot reach backend API:
- Ensure backend is running on the port specified in `vite.config.ts`
- Check that `https://localhost:5001/api/*` is accessible directly in a browser
- Clear Vite cache: `rm -rf frontend/node_modules/.vite`

#### Database Connection Fallback
If PostgreSQL is unavailable or not configured:
- Backend automatically switches to `InMemoryCommunityCourtService`
- Data is reset on app restart; this is by design for development
- No errors are shown; check `appsettings.Development.json` to confirm `ConnectionStrings:DefaultConnection` is empty

---

## Project Layout & Architecture

### Root Directory Structure
```
.
├── backend/                    # ASP.NET Core 8 application
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
├── docker-compose.yml          # PostgreSQL service for development
└── .gitignore                  # Excludes node_modules, appsettings.*.local.json, etc.
```

### Key Configuration Files

| File | Purpose |
|------|---------|
| `backend/backend.csproj` | NuGet dependencies: EF Core, Npgsql, Swashbuckle |
| `backend/Program.cs` | Middleware registration, CORS, compression, static file serving |
| `frontend/package.json` | npm dependencies: Vue, Pinia, Axios, Vite |
| `frontend/vite.config.ts` | Proxy rules, dev server config (`https://localhost:5001/api`) |
| `docker-compose.yml` | PostgreSQL 16 configuration (port 5433) |

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
vue-tsc --noEmit -b

# Optional: Run a request to backend health endpoint
curl -X GET https://localhost:5001/api/cases --insecure
```

---

## Architecture Highlights

### Backend (ASP.NET Core 8)

**Controllers** expose REST endpoints:
- `CasesController` → `GET /api/cases`, `POST /api/cases`, etc.
- `UsersController` → `GET /api/users/{id}`, etc.
- `FriendsController` → `POST /api/friends/requests`, etc.

**Services** contain business logic:
- `ICommunityCourtService` interface defines operations
- `EfCoreCourtService` implements with EF Core + PostgreSQL
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
- Development: Proxied by Vite to `https://localhost:5001`
- Production: Backend serves static files and handles API directly

---

## Dependencies & Versions

### Backend
- **Framework**: .NET 8.0
- **Microsoft.EntityFrameworkCore**: 8.0.27
- **Npgsql.EntityFrameworkCore.PostgreSQL**: 8.0.11 (PostgreSQL support)
- **Swashbuckle.AspNetCore**: 6.6.2 (Swagger/OpenAPI)

### Frontend
- **Vue**: 3.5.34
- **TypeScript**: ~6.0.2
- **Vite**: 8.0.12
- **Pinia**: 3.0.4
- **Vue Router**: 4.6.4
- **Axios**: 1.16.1

### External Services
- **PostgreSQL**: 16 (optional; in-memory fallback works)
- **Docker**: (optional; only for running PostgreSQL in container)

---

## Critical Design Notes

1. **No Authentication Layer**: The frontend stores `selectedUserId` in `localStorage`. All backend requests include `userId` in the body. The backend validates user existence.

2. **Verdict Computation**: Vote counts are recalculated on each case read (not stored). This ensures consistency.

3. **Vote Change Rule**: Users can vote once, then change once. A second change is rejected.

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

