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
│   ├── friends.ts       # Friends, friend requests, and invitations (Pinia store)
│   └── rewards.ts       # User reward badges (Pinia store)
├── views/
│   ├── HomeView.vue      # Case listing page + My Invitations section
│   ├── CaseDetailView.vue# Single case view — handles Pending/Open/Closed states
│   ├── CreateCaseView.vue# Form to start a new case and invite a Side B opponent
│   ├── FriendsView.vue   # Friends list, incoming requests, add friend
│   └── RewardsView.vue   # Badge/reward display for the current user
└── components/
    └── HelloWorld.vue    # (Scaffold placeholder)
```

---

## Router

Defined in `router/index.ts`. Uses `createWebHistory` (HTML5 mode).

| Path | Name | View | Notes |
|------|------|------|-------|
| `/` | `home` | `HomeView` | Lists active cases + pending invitations for current user |
| `/cases/:id` | `case-detail` | `CaseDetailView` | Route param `id` passed as prop |
| `/cases/new` | `case-create` | `CreateCaseView` | New case form |
| `/rewards` | `rewards` | `RewardsView` | Rewards for selected user |
| `/friends` | `friends` | `FriendsView` | Friend management |

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
| `cases` | `ArgumentCase[]` | All public cases (Open/Closed only) |
| `selectedCase` | `ArgumentCase \| null` | Case being viewed (any status) |
| `loading` | `boolean` | Read operation in progress |
| `mutating` | `boolean` | Write operation in progress |
| `error` | `string \| null` | Last error message |

| Action | Description |
|--------|-------------|
| `loadCases()` | Fetches all public cases |
| `loadCase(id)` | Fetches a single case by ID (any status) |
| `createCase(request)` | Creates a new `Pending` case; prepends it to `cases` |
| `vote(caseId, userId, side)` | Casts a vote; updates `cases` and `selectedCase` |
| `closeCase(caseId, actorUserId)` | Closes a case; updates `cases` and `selectedCase` |
| `acceptInvitation(caseId, userId, claim)` | Accepts a case invitation; case moves to `Open` |
| `declineInvitation(caseId, userId)` | Declines an invitation; removes case from local state |

---

### `friends` — `stores/friends.ts`
Manages the social graph: friends list, incoming friend requests, and pending case invitations.

| State | Type | Description |
|-------|------|-------------|
| `friends` | `AppUser[]` | Accepted friends of the current user |
| `incomingRequests` | `FriendRequest[]` | Pending friend requests addressed to the current user |
| `invitations` | `ArgumentCase[]` | Pending case invitations where the user is invited to Side B |
| `loading` | `boolean` | Any fetch in progress |
| `error` | `string \| null` | Last error message |

| Action | Description |
|--------|-------------|
| `loadFriends(userId)` | Fetches accepted friends |
| `loadFriendRequests(userId)` | Fetches incoming pending friend requests |
| `loadInvitations(userId)` | Fetches pending case invitations for the user |
| `sendRequest(fromUserId, toUserId)` | Sends a friend request |
| `respondToRequest(requestId, actorUserId, accept)` | Accepts or declines a friend request; removes it from `incomingRequests` |
| `clearAll()` | Clears all state (used on user switch) |

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
| `fetchCases()` | `GET` | `/cases` | Get all public cases |
| `fetchCaseById(id)` | `GET` | `/cases/{id}` | Get one case (any status) |
| `createCase(request)` | `POST` | `/cases` | Create a new `Pending` case |
| `castVote(caseId, request)` | `POST` | `/cases/{id}/vote` | Cast a vote |
| `closeCase(caseId, request)` | `POST` | `/cases/{id}/close` | Close a case |
| `acceptCaseInvitation(caseId, request)` | `POST` | `/cases/{id}/accept` | Accept invitation and provide Side B claim |
| `declineCaseInvitation(caseId, userId)` | `POST` | `/cases/{id}/decline` | Decline an invitation |
| `fetchUsers()` | `GET` | `/users` | Get all users |
| `fetchUserRewards(userId)` | `GET` | `/users/{id}/rewards` | Get user rewards |
| `fetchFriends(userId)` | `GET` | `/users/{id}/friends` | Get user's friends |
| `fetchFriendRequests(userId)` | `GET` | `/users/{id}/friend-requests` | Get incoming friend requests |
| `fetchInvitations(userId)` | `GET` | `/users/{id}/invitations` | Get pending case invitations |
| `sendFriendRequest(dto)` | `POST` | `/friends/request` | Send a friend request |
| `respondToFriendRequest(id, dto, accept)` | `POST` | `/friends/{id}/accept` or `.../decline` | Accept or decline a friend request |

---

## Types

`types.ts` mirrors the backend C# models as TypeScript interfaces. See [Data Models](./data-models.md) for full field descriptions.

Key types: `AppUser`, `ArgumentCase`, `ArgumentPost`, `CommunityVerdict`, `FriendRequest`, `FriendRequestStatus`, `UserRewardView`, `CreateCaseRequest`, `AcceptInvitationRequest`, `DeclineInvitationRequest`, `SendFriendRequestDto`, `RespondFriendRequestDto`, `CastVoteRequest`, `CloseCaseRequest`.

