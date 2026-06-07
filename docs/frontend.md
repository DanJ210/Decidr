# Frontend Guide

The frontend is a Vue 3 SPA built with Vite and TypeScript. Source lives in `frontend/src/`.

## Directory Structure

```
frontend/src/
├── main.ts              # App bootstrap (Vue, Pinia, Router)
├── App.vue              # Root component (includes temporary Active User picker)
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
├── composables/
│   ├── useCaseDetail.ts  # Reactive logic for CaseDetailView (computed permissions, actions)
│   ├── useCreateCase.ts  # Form state + submit logic for CreateCaseView
│   ├── useFriends.ts     # User search, friend filtering, and friend-management actions
│   ├── useGroupedRewards.ts # Rewards loading + tier-grouped computed for RewardsView
│   └── useHottestCases.ts   # Sorted case list + invitations loading for HomeView
├── views/
│   ├── HomeView.vue      # Case listing page + My Invitations section
│   ├── CaseDetailView.vue# Single case view — handles Pending/Open/Closed states
│   ├── CreateCaseView.vue# Form to start a new case and invite a connected Side B friend
│   ├── FriendsView.vue   # Friend search, incoming requests, add/remove friend
│   └── RewardsView.vue   # Badge/reward display for the current user
└── components/
    └── HelloWorld.vue    # (Scaffold placeholder)
```

---

## Mobile-first UX plan (social feed)

Goal: make Decidr feel like a social-first mobile app (Instagram-style) with a case feed as the home screen, primary actions within thumb reach, and fast scanning of cases.

### Information architecture

- **Primary tabs (bottom nav):** Home (case feed), Create (center action), Friends, Rewards, Profile (or Settings).
- **Secondary actions:** Filters, search, and notifications live in the top app bar on mobile.
- **Entry points:** The case feed is the default route; detail pages open from feed cards.

### Layout & navigation changes

- **App shell:** Shift to a full-width, mobile-first container (no fixed max width on small screens).
- **Top app bar:** Brand + optional notifications/search; collapse secondary links into icons.
- **Bottom navigation:** Persistent bottom nav with 4–5 items; make “Create Case” the center primary action (FAB).
- **Desktop view:** Add a wider breakpoint where bottom nav becomes a left rail or top bar, while keeping feed cards centered.

### Case feed (home page) experience

- **Feed card hierarchy:** Category pill → title → summary → side participants → vote count → status.
- **Action row:** “Vote” (if open) and “View Case” as primary/secondary CTAs.
- **Infinite scroll / pagination:** Replace “top 6 cases” with a paged feed and load-on-scroll.
- **Optional media:** Reserve space for a thumbnail (even if placeholder) to create a social feed feel.

### Case detail and social layers

- **Sticky action bar:** Keep vote/close/share actions fixed on mobile.
- **Threaded updates:** Add a compact activity timeline (votes, closure, invitations).
- **Friends surface:** Promote friend invitations and pending requests as “notifications” items.

### Implementation checklist for this repo

- **App layout:** Update `App.vue` to include a bottom nav component and a slimmer top bar for mobile.
- **Components:** Add `components/BottomNav.vue` and (optional) `components/AppHeader.vue`.
- **Styles:** Extend `style.css` with mobile-first defaults, then add `@media (min-width: 900px)` for desktop.
- **Routes:** Ensure bottom-nav destinations map to existing routes; optionally add `/profile`.
- **Feed data:** Update `useHottestCases` (or add `useCaseFeed`) to support pagination and sorting.
- **Home view:** Refactor `HomeView.vue` into a vertically stacked feed with action rows per card.
- **A11y:** Keep tap targets ≥ 44px, use contrast-safe colors, and ensure keyboard focus states remain visible.

## Composables

Composables in `composables/` encapsulate reactive logic that would otherwise live inline in `<script setup>`. Each composable calls stores, sets up watchers, and returns only the values and functions the view needs.

### `useCaseDetail` — `composables/useCaseDetail.ts`
Used by `CaseDetailView`. Loads the case on mount from the URL param, then derives permission flags and exposes all case actions.

| Returned | Type | Description |
|----------|------|-------------|
| `courtStore` | `CourtStore` | Direct store reference (loading/mutating/error state) |
| `sideBClaim` | `Ref<string>` | Two-way bound text for the Side B invitation response |
| `commentMessage` | `Ref<string>` | Two-way bound text for posting a case-level comment |
| `comments` | `Ref<CaseComment[]>` | Shared case comment pool (not side-specific) |
| `canComment` | `ComputedRef<boolean>` | `true` when an active user is selected and the case is loaded |
| `caseItem` | `ComputedRef` | The currently loaded case (`selectedCase`) |
| `totalVotes` | `ComputedRef<number>` | Sum of votes for both sides |
| `isInvited` | `ComputedRef<boolean>` | `true` when the active user is the invited Side B participant |
| `inviterName` | `ComputedRef<string>` | Username of the Side A participant |
| `isParticipant` | `ComputedRef<boolean>` | `true` when the active user is Side A or Side B |
| `canVote` | `ComputedRef<boolean>` | `true` when the case is Open and the user is not a participant |
| `canCloseCase` | `ComputedRef<boolean>` | `true` when the user is a participant or moderator |
| `closePermissionMessage` | `ComputedRef<string>` | Contextual hint shown below the Close button |
| `vote(side)` | `function` | Casts a vote and refreshes the case |
| `closeCase()` | `function` | Closes the case and refreshes it |
| `acceptInvitation()` | `function` | Submits the Side B claim and refreshes the case |
| `declineInvitation()` | `function` | Declines the invitation and navigates back to `/` |
| `submitComment()` | `function` | Posts a comment into the case's shared comment pool |

---

### `useCreateCase` — `composables/useCreateCase.ts`
Used by `CreateCaseView`. Owns the reactive form, loads prerequisite data, keeps `invitedUserId` in sync with the friends list, and handles form submission.

| Returned | Type | Description |
|----------|------|-------------|
| `authStore` | `AuthStore` | Used to display the active user's name in the template |
| `courtStore` | `CourtStore` | Exposes `mutating` and `error` for the submit button and error message |
| `form` | `Reactive` | Form fields: `title`, `category`, `summary`, `sideAClaim`, `invitedUserId` |
| `inviteCandidates` | `ComputedRef<AppUser[]>` | Friends eligible to be invited (Side B) |
| `submit()` | `function` | Creates the case via the store and navigates to the new case page |

---

### `useFriends` — `composables/useFriends.ts`
Used by `FriendsView`. Loads all friend data on mount and on user switch, and encapsulates the search, filtering, and mutating actions.

| Returned | Type | Description |
|----------|------|-------------|
| `friendsStore` | `FriendsStore` | Loading/error state and all raw lists |
| `userSearchTerm` | `Ref<string>` | Two-way bound search input for finding new friends |
| `friendSearchTerm` | `Ref<string>` | Two-way bound filter input for the friends list |
| `normalizedUserSearch` | `ComputedRef<string>` | Trimmed + lowercased `userSearchTerm` |
| `userSearchResults` | `ComputedRef<UserWithStatus[]>` | Filtered user list annotated with friendship status |
| `filteredFriends` | `ComputedRef<AppUser[]>` | Friends list filtered by `friendSearchTerm` |
| `fromUserName(id)` | `function` | Resolves a user ID to a display name (incoming requests) |
| `toUserName(id)` | `function` | Resolves a user ID to a display name (outgoing requests) |
| `sendRequest(toUserId)` | `function` | Sends a friend request from the active user |
| `respondToRequest(id, accept)` | `function` | Accepts or declines an incoming request and refreshes friends |
| `removeFriend(friendUserId)` | `function` | Removes an accepted friend connection |

Exported types: `UserStatus`, `UserWithStatus`.

---

### `useGroupedRewards` — `composables/useGroupedRewards.ts`
Used by `RewardsView`. Reacts to `selectedUserId` changes via `watchEffect`, loading or clearing rewards automatically, and groups them by tier.

| Returned | Type | Description |
|----------|------|-------------|
| `authStore` | `AuthStore` | Used for the page heading (selected user's display name) |
| `rewardsStore` | `RewardsStore` | Loading/error state and raw rewards list |
| `groupedRewards` | `ComputedRef<Record<string, UserRewardView[]>>` | Rewards keyed by tier name |

---

### `useHottestCases` — `composables/useHottestCases.ts`
Used by `HomeView`. Loads cases and invitations on mount, refreshes invitations when the active user changes, and exposes the full case feed sorted by vote count.

| Returned | Type | Description |
|----------|------|-------------|
| `courtStore` | `CourtStore` | Loading/error state and raw cases list |
| `friendsStore` | `FriendsStore` | Exposes `invitations` for the My Invitations section |
| `caseFeed` | `ComputedRef<ArgumentCase[]>` | All cases sorted by total vote count (descending) |

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
| `outgoingRequests` | `FriendRequest[]` | Pending friend requests sent by the current user |
| `invitations` | `ArgumentCase[]` | Pending case invitations where the user is invited to Side B |
| `loading` | `boolean` | Any fetch in progress |
| `error` | `string \| null` | Last error message |
| `outgoingError` | `string \| null` | Last error message from loading sent friend requests |

| Action | Description |
|--------|-------------|
| `loadFriends(userId)` | Fetches accepted friends |
| `loadFriendRequests(userId)` | Fetches incoming pending friend requests |
| `loadOutgoingRequests(userId)` | Fetches sent pending friend requests |
| `loadInvitations(userId)` | Fetches pending case invitations for the user |
| `sendRequest(fromUserId, toUserId)` | Sends a friend request |
| `respondToRequest(requestId, actorUserId, accept)` | Accepts or declines a friend request; removes it from `incomingRequests` |
| `removeFriend(actorUserId, friendUserId)` | Removes an existing friend connection |
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
| `createCase(request)` | `POST` | `/cases` | Create a new `Pending` case (friend connection required for invite) |
| `castVote(caseId, request)` | `POST` | `/cases/{id}/vote` | Cast a vote |
| `closeCase(caseId, request)` | `POST` | `/cases/{id}/close` | Close a case |
| `fetchCaseComments(caseId)` | `GET` | `/cases/{id}/comments` | Get the shared case comment pool |
| `postCaseComment(caseId, request)` | `POST` | `/cases/{id}/comments` | Add a case-level comment |
| `acceptCaseInvitation(caseId, request)` | `POST` | `/cases/{id}/accept` | Accept invitation and provide Side B claim |
| `declineCaseInvitation(caseId, userId)` | `POST` | `/cases/{id}/decline` | Decline an invitation |
| `fetchUsers()` | `GET` | `/users` | Get all users |
| `fetchUserRewards(userId)` | `GET` | `/users/{id}/rewards` | Get user rewards |
| `fetchFriends(userId)` | `GET` | `/users/{id}/friends` | Get user's friends |
| `fetchFriendRequests(userId)` | `GET` | `/users/{id}/friend-requests` | Get incoming friend requests |
| `fetchOutgoingFriendRequests(userId)` | `GET` | `/users/{id}/sent-requests` | Get sent pending friend requests |
| `fetchInvitations(userId)` | `GET` | `/users/{id}/invitations` | Get pending case invitations |
| `sendFriendRequest(dto)` | `POST` | `/friends/request` | Send a friend request |
| `respondToFriendRequest(id, dto, accept)` | `POST` | `/friends/{id}/accept` or `.../decline` | Accept or decline a friend request |
| `removeFriend(dto)` | `POST` | `/friends/remove` | Remove an accepted friend connection |

---

## Types

`types.ts` mirrors the backend C# models as TypeScript interfaces. See [Data Models](./data-models.md) for full field descriptions.

Key types: `AppUser`, `ArgumentCase`, `ArgumentPost`, `CommunityVerdict`, `CaseComment`, `FriendRequest`, `FriendRequestStatus`, `UserRewardView`, `CreateCaseRequest`, `AcceptInvitationRequest`, `DeclineInvitationRequest`, `SendFriendRequestDto`, `RespondFriendRequestDto`, `CastVoteRequest`, `CloseCaseRequest`, `CreateCaseCommentRequest`.
