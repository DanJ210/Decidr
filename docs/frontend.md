# Frontend Guide

The frontend is a Vue 3 SPA built with Vite and TypeScript. Source lives in `frontend/src/`.

## Directory Structure

```
frontend/src/
├── main.ts              # App bootstrap (Vue, Pinia, Router)
├── App.vue              # Root component
├── types.ts             # Shared TypeScript types (mirrors backend models)
├── style.css            # Global styles
├── router/
│   └── index.ts         # Vue Router route definitions
├── services/
│   └── api.ts           # Axios-based API client functions
├── stores/
│   ├── auth.ts          # Active user selection (Pinia store)
│   ├── court.ts         # Case list and case detail state (Pinia store)
│   └── rewards.ts       # User reward badges (Pinia store)
├── views/
│   ├── HomeView.vue      # Case listing page
│   ├── CaseDetailView.vue# Single case view with voting and close actions
│   ├── CreateCaseView.vue# Form to submit a new debate case
│   └── RewardsView.vue   # Badge/reward display for the current user
└── components/
    └── HelloWorld.vue    # (Scaffold placeholder)
```

---

## Router

Defined in `router/index.ts`. Uses `createWebHistory` (HTML5 mode).

| Path | Name | View | Notes |
|------|------|------|-------|
| `/` | `home` | `HomeView` | Lists all cases |
| `/cases/:id` | `case-detail` | `CaseDetailView` | Route param `id` passed as prop |
| `/cases/new` | `case-create` | `CreateCaseView` | New case form |
| `/rewards` | `rewards` | `RewardsView` | Rewards for selected user |

---

## Pinia Stores

### `auth` — `stores/auth.ts`
Manages user identity. Persists `selectedUserId` to `localStorage` under the key `decidr-selected-user-id`.

| State | Type | Description |
|-------|------|-------------|
| `users` | `AppUser[]` | All users loaded from the API |
| `selectedUserId` | `string \| null` | Currently acting user |
| `loading` | `boolean` | API fetch in progress |
| `error` | `string \| null` | Last error message |

| Getter | Returns | Description |
|--------|---------|-------------|
| `selectedUser` | `AppUser \| null` | Full user object for `selectedUserId` |

| Action | Description |
|--------|-------------|
| `loadUsers()` | Fetches all users; restores cached selection or defaults to first user |
| `setSelectedUser(userId)` | Updates selection and persists to `localStorage` |

---

### `court` — `stores/court.ts`
Manages the case list and the currently viewed case.

| State | Type | Description |
|-------|------|-------------|
| `cases` | `ArgumentCase[]` | All cases |
| `selectedCase` | `ArgumentCase \| null` | Case being viewed |
| `loading` | `boolean` | Read operation in progress |
| `mutating` | `boolean` | Write operation in progress |
| `error` | `string \| null` | Last error message |

| Action | Description |
|--------|-------------|
| `loadCases()` | Fetches all cases |
| `loadCase(id)` | Fetches a single case by ID |
| `createCase(request)` | Creates a new case; prepends it to `cases` |
| `vote(caseId, userId, side)` | Casts a vote; updates `cases` and `selectedCase` |
| `closeCase(caseId, actorUserId)` | Closes a case; updates `cases` and `selectedCase` |

---

### `rewards` — `stores/rewards.ts`
Manages the reward badges for the selected user.

| State | Type | Description |
|-------|------|-------------|
| `rewards` | `UserRewardView[]` | Badges for the current user |
| `loading` | `boolean` | Fetch in progress |
| `error` | `string \| null` | Last error message |

| Action | Description |
|--------|-------------|
| `loadRewards(userId)` | Fetches badges for the given user |
| `clearRewards()` | Clears the rewards list (used on user switch) |

---

## API Service Layer

Defined in `services/api.ts`. All functions use a shared Axios instance with `baseURL: '/api'` and a 10-second timeout.

| Function | Method | Endpoint | Description |
|----------|--------|----------|-------------|
| `fetchCases()` | `GET` | `/cases` | Get all cases |
| `fetchCaseById(id)` | `GET` | `/cases/{id}` | Get one case |
| `createCase(request)` | `POST` | `/cases` | Create a new case |
| `castVote(caseId, request)` | `POST` | `/cases/{id}/vote` | Cast a vote |
| `closeCase(caseId, request)` | `POST` | `/cases/{id}/close` | Close a case |
| `fetchUsers()` | `GET` | `/users` | Get all users |
| `fetchUserRewards(userId)` | `GET` | `/users/{id}/rewards` | Get user rewards |

---

## Types

`types.ts` mirrors the backend C# models as TypeScript interfaces. See [Data Models](./data-models.md) for full field descriptions.

Key types: `AppUser`, `ArgumentCase`, `ArgumentPost`, `CommunityVerdict`, `UserRewardView`, `CreateCaseRequest`, `CastVoteRequest`, `CloseCaseRequest`.
