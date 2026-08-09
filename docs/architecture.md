# Architecture

## Overview

Decidr is a full-stack single-page application (SPA). The ASP.NET Core backend exposes a REST API and also serves the compiled Vue frontend as static files.

```
┌──────────────────────────────────────────────┐
│                  Browser                     │
│                                              │
│  Vue 3 SPA (Vite build / dev server)         │
│  ┌─────────┐ ┌──────────┐ ┌──────────────┐  │
│  │  Views  │ │ Stores   │ │ Router       │  │
│  │         │ │ (Pinia)  │ │ /            │  │
│  │ Home    │ │ auth     │ │ /cases/:id   │  │
│  │ Case    │ │ court    │ │ /cases/new   │  │
│  │ Create  │ │ friends  │ │ /rewards     │  │
│  │ Friends │ │ rewards  │ │ /friends     │  │
│  │ Rewards │ └────┬─────┘ └──────────────┘  │
│  └────┬────┘      │                         │
│       └───────────┘                         │
│            │  Axios (/api/*)                │
└────────────┼───────────────────────────────┘
             │ HTTP
┌────────────▼───────────────────────────────┐
│         ASP.NET Core 10 Backend            │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │  Controllers                         │  │
│  │  CasesController   /api/cases        │  │
│  │  UsersController   /api/users        │  │
│  │  FriendsController /api/friends      │  │
│  └──────────────┬───────────────────────┘  │
│                 │ ICommunityCourtService    │
│  ┌──────────────▼───────────────────────┐  │
│  │  EfCoreCourtService                  │  │
│  │  (scoped, EF Core-backed)            │  │
│  │                                      │  │
│  │  DbSet<UserEntity>                   │  │
│  │  DbSet<CaseEntity>                   │  │
│  │  DbSet<CaseVoteEntity>               │  │
│  │  DbSet<UserRewardEntity>             │  │
│  │  DbSet<FriendRequestEntity>          │  │
│  └──────────────────────────────────────┘  │
│                                            │
│  Azure SQL via EF Core SQL Server provider │
│                                            │
│  Static files → wwwroot (Vue build)        │
└────────────────────────────────────────────┘
```

## Key Design Decisions

### Persistence Strategy
When a connection string is configured, the backend uses `EfCoreCourtService` with `DecidirDbContext` and Azure SQL or SQL Server. In development, migrations are applied and seed data is inserted when the database is empty.

If no `ConnectionStrings:DefaultConnection` is configured, the app falls back to `InMemoryCommunityCourtService` for local/demo execution.

### Verdict Refresh
Vote counts are not stored directly on `ArgumentCase`. `RefreshVerdict()` recomputes the tally from vote records each time a case is read, keeping the read model consistent with persisted votes.

### Vote Change Rule
Users can cast one vote on an `Open` case if they are not one of the case-side participants. Vote changes are not supported.

### Side Evidence Attachments
Each side on an open case can attach supporting evidence as links or uploaded files. Evidence is modeled as side-scoped items and exposed through dedicated evidence endpoints:
- `GET /api/cases/{id}/evidence`
- `GET /api/cases/{id}/evidence/{evidenceId}/content`
- `POST /api/cases/{id}/evidence/link`
- `POST /api/cases/{id}/evidence/upload`

Uploaded files are stored in the private Azure Blob Storage `case-evidence`
container. `DefaultAzureCredential` uses the App Service managed identity in
Azure, and storage keys remain server-side. The API returns an application content
URL and streams files through an authenticated endpoint; the SPA retrieves the
bytes with its bearer-authenticated Axios client and creates temporary browser
object URLs for previews. Development without Blob configuration uses a private
`App_Data/case-evidence` provider behind the same storage interface.

### Case Invitation Flow
Cases are created in a `Pending` state. Only the creator's Side A claim is stored initially. The invited user (`InvitedUserId`) must navigate to the case and either:
- **Accept** — provide their Side B claim, which moves the case to `Open` and makes it visible on the public feed.
- **Decline** — which moves the case to `Closed` with no winner.

`GET /api/cases` deliberately excludes `Pending` cases, keeping the public feed clean. `Pending` cases are surfaced to the invited user via `GET /api/users/{id}/invitations` and shown in the "My Invitations" section on the home page.

### Friend System
Users can send, accept, decline, and remove friend connections. Accepted friendships are derived from `_friendRequests` where `Status == Accepted`. When creating a new case, invitations are restricted to accepted friends only.

### Reward System
Badges are awarded automatically at key lifecycle events:
- **POST_PARTICIPATION** — awarded to Side A on case creation; awarded to Side B when they accept an invitation.
- **VOTE_PARTICIPATION** — every user who casts a vote.
- **VOTE_WINNER_MATCH** — voters whose vote matched the winning side, awarded on case close.
- **CASE_VICTOR** — the winning side's poster, awarded on case close.

Duplicate awards are prevented: a `(userId, badgeCode, sourceType, sourceId)` combination is only awarded once.

### User Identity and Authentication
In configured environments, the Vue SPA authenticates with Microsoft Entra External
ID through MSAL and sends bearer tokens with API requests. ASP.NET Core JWT bearer
validation checks the token authority and audience. `IAuthenticatedUserService`
maps the token's stable `tid` and `oid` claims to the local `UserEntity`; a first
sign-in creates a local Member profile. Authenticated API operations require the
delegated `access_as_user` scope. Entra configuration also requires persistent
SQL storage so external identities cannot be provisioned into transient memory.

Controller endpoints are secure by default when Entra is configured: the
`AccessAsUser` policy is attached to the controller endpoint convention, so new
actions require a valid scoped token unless they explicitly opt into anonymous
access. The public case feed, detail, comments, evidence metadata, and result
actions are the only anonymous API surfaces. This convention is conditional so
Development without Entra can retain its selected-user header workflow.

Write endpoints resolve the acting user from the authenticated claims. Actor IDs
are not accepted from request bodies. Request IDs remain only when they identify a
target user, case, friend request, or other resource. User-scoped private reads
also require the authenticated local user to match the route ID. Vote status and
per-viewer case state derive the viewer from the authenticated actor rather than
accepting a user ID from the caller.

Object authorization also applies to anonymous case reads. `Open` and `Closed`
cases are public, while a `Pending` case and its comments, evidence metadata, and
result are visible only to Side A, the invited/Side B user, or a moderator.

Development without Entra configuration retains the seeded selected-user fallback
for local demos. This fallback is intentionally unavailable as an authentication
mode outside Development.

API endpoints are rate limited per authenticated Entra object ID, falling back
to the remote IP address for anonymous traffic. Responses include anti-sniffing,
anti-framing, and strict referrer-policy headers, and non-Development deployments
enable HTTP Strict Transport Security (HSTS).

Uploaded evidence uses private Azure Blob Storage outside Development and an
application-controlled download path. Uploads are limited to 10 MB, validated by
extension, MIME allowlist, and file signature before storage, and returned with
explicit content types and download filenames. The container has anonymous access
disabled and the App Service managed identity has data-plane access only at
container scope. In Azure, the download path checks the
Defender for Storage `Malware Scanning scan result` blob index tag and streams
content only when the value is exactly `No threats found`. Missing or unknown
results are pending, and malicious, failed, or unscanned results fail closed.
Production operations should additionally enable Defender's malicious-blob soft
delete and use security alerts, Event Grid, or Log Analytics for tamper-resistant
response and audit workflows because blob index tags can be modified by principals
with tag-write permission.

### Frontend–Backend Integration
In production, `dotnet run` serves both the API and the compiled Vue SPA. The backend registers `UseDefaultFiles()`, `UseStaticFiles()`, and `MapFallbackToFile("index.html")` so Vue Router can handle client-side navigation. In development, the Vite dev server handles the frontend and proxies API calls to the .NET backend.

### Response Compression
The backend enables Brotli and Gzip response compression (including HTTPS responses) for API JSON payloads and static assets. This reduces transfer size and improves page/API latency, especially on slower networks.
