# Architecture

## Overview

Decidr is a full-stack single-page application (SPA). The ASP.NET Core backend exposes a REST API and also serves the compiled Vue frontend as static files.

```
┌──────────────────────────────────────────────┐
│                  Browser                     │
│                                              │
│  Vue 3 SPA (Vite build / dev server)         │
│  ┌─────────┐ ┌────────┐ ┌────────────────┐  │
│  │  Views  │ │Stores  │ │ Router         │  │
│  │         │ │(Pinia) │ │ /              │  │
│  │ Home    │ │ auth   │ │ /cases/:id     │  │
│  │ Case    │ │ court  │ │ /cases/new     │  │
│  │ Create  │ │ rewards│ │ /rewards       │  │
│  │ Rewards │ └───┬────┘ └────────────────┘  │
│  └────┬────┘     │                          │
│       └──────────┘                          │
│            │  Axios (/api/*)                │
└────────────┼───────────────────────────────┘
             │ HTTP
┌────────────▼───────────────────────────────┐
│         ASP.NET Core 8 Backend             │
│                                            │
│  ┌─────────────────────────────────┐       │
│  │  Controllers                    │       │
│  │  CasesController   /api/cases   │       │
│  │  UsersController   /api/users   │       │
│  └──────────────┬──────────────────┘       │
│                 │ ICommunityCourtService    │
│  ┌──────────────▼──────────────────┐       │
│  │  InMemoryCommunityCourtService  │       │
│  │  (singleton, thread-safe)       │       │
│  │                                 │       │
│  │  _users   List<AppUser>         │       │
│  │  _cases   List<ArgumentCase>    │       │
│  │  _votes   List<CaseVote>        │       │
│  │  _rewards List<UserReward>      │       │
│  └─────────────────────────────────┘       │
│                                            │
│  Static files → wwwroot (Vue build)        │
└────────────────────────────────────────────┘
```

## Key Design Decisions

### In-Memory Store
All data is held in `InMemoryCommunityCourtService`, a singleton registered in DI. A `lock (_syncRoot)` guards every read and write to make operations thread-safe. **Data does not persist between restarts.**

### Verdict Refresh
Vote counts are not stored on the `ArgumentCase` record directly. Instead, `RefreshVerdict()` recomputes the tally from `_votes` each time a case is read. This keeps the case record immutable while ensuring the verdict is always current.

### Reward System
Badges are awarded automatically at key lifecycle events:
- **POST_PARTICIPATION** — both side posters when a case is created.
- **VOTE_PARTICIPATION** — every user who casts a vote.
- **VOTE_WINNER_MATCH** — voters whose vote matched the winning side, awarded on case close.
- **CASE_VICTOR** — the winning side's poster, awarded on case close.

Duplicate awards are prevented: a `(userId, badgeCode, sourceType, sourceId)` combination is only awarded once.

### User Identity (Simplified Auth)
There is no authentication system. The frontend stores a `selectedUserId` in `localStorage`. All actions (voting, closing cases, creating cases) pass a `userId` in the request body. The backend validates that the user exists and has the appropriate role or participation.

### Frontend–Backend Integration
In production, `dotnet run` serves both the API and the compiled Vue SPA. The backend registers `UseDefaultFiles()`, `UseStaticFiles()`, and `MapFallbackToFile("index.html")` so Vue Router can handle client-side navigation. In development, the Vite dev server handles the frontend and proxies API calls to the .NET backend.
