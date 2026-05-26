# Data Models

Models are defined in `backend/Models/ArgumentCase.cs` and mirrored in `frontend/src/types.ts`.

---

## Enums

### `CaseSide`
| Value | Description |
|-------|-------------|
| `A` | Side A of the debate |
| `B` | Side B of the debate |

### `CaseStatus`
| Value | Description |
|-------|-------------|
| `Pending` | Case created; waiting for the invited user to write Side B |
| `Open` | Both sides posted; accepting community votes |
| `Closed` | Voting ended; winner resolved (or invitation declined) |

### `UserRole`
| Value | Description |
|-------|-------------|
| `Member` | Standard community member |
| `Moderator` | Can close any case |

### `FriendRequestStatus`
| Value | Description |
|-------|-------------|
| `Pending` | Request sent, not yet responded to |
| `Accepted` | Both users are now friends |
| `Declined` | Request was rejected |

---

## Records

### `AppUser`
Represents a registered user.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier |
| `UserName` | `string` | Short handle (e.g. `alex_t`) |
| `DisplayName` | `string` | Human-friendly name (e.g. `Alex`) |
| `Role` | `UserRole` | `Member` or `Moderator` |

---

### `ArgumentPost`
One side's opening argument in a case.

| Field | Type | Description |
|-------|------|-------------|
| `Side` | `CaseSide` | Which side this post belongs to |
| `UserId` | `Guid` | Posting user's ID |
| `UserName` | `string` | Posting user's handle |
| `Claim` | `string` | The argument text |
| `PostedAtUtc` | `DateTime` | When posted |

---

### `CommunityVerdict`
Live vote tally for a case. Recomputed on every read.

| Field | Type | Description |
|-------|------|-------------|
| `VotesForSideA` | `int` | Total votes for Side A |
| `VotesForSideB` | `int` | Total votes for Side B |

---

### `ArgumentCase`
The central entity representing a debate case.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier |
| `Title` | `string` | Short description of the dispute |
| `Category` | `string` | Topic category (e.g. `Relationships`) |
| `Summary` | `string` | Neutral summary of the disagreement |
| `SideA` | `ArgumentPost` | Side A's argument (always set) |
| `SideB` | `ArgumentPost?` | Side B's argument — `null` while status is `Pending` |
| `InvitedUserId` | `Guid?` | The user invited to write Side B; `null` once they respond |
| `Verdict` | `CommunityVerdict` | Current vote counts |
| `Status` | `CaseStatus` | `Pending`, `Open`, or `Closed` |
| `WinnerSide` | `CaseSide?` | `null` until closed; `null` on a tie or declined invitation |
| `CreatedAtUtc` | `DateTime` | When case was created |

---

### `FriendRequest`
A directed friend request between two users.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier |
| `FromUserId` | `Guid` | User who sent the request |
| `ToUserId` | `Guid` | User who received the request |
| `Status` | `FriendRequestStatus` | `Pending`, `Accepted`, or `Declined` |
| `CreatedAtUtc` | `DateTime` | When the request was sent |

---

### `CaseVote`
An individual community vote (internal, not exposed via API).

| Field | Type | Description |
|-------|------|-------------|
| `CaseId` | `Guid` | The case being voted on |
| `UserId` | `Guid` | The voter |
| `Side` | `CaseSide` | Which side they voted for |
| `CreatedAtUtc` | `DateTime` | When the vote was cast |

---

### `RewardBadge`
Definition of an available badge (static catalog).

| Field | Type | Description |
|-------|------|-------------|
| `Code` | `string` | Unique badge code |
| `Label` | `string` | Display name |
| `IconKey` | `string` | UI icon identifier |
| `Tier` | `string` | `Bronze`, `Silver`, or `Gold` |
| `Description` | `string` | How the badge is earned |

**Badge Catalog**

| Code | Label | Tier | Trigger |
|------|-------|------|---------|
| `VOTE_PARTICIPATION` | Community Juror | Bronze | Any vote cast |
| `VOTE_WINNER_MATCH` | Sharp Eye | Silver | Vote matched the winning side |
| `POST_PARTICIPATION` | Case Contributor | Bronze | Posted a side in any case |
| `CASE_VICTOR` | Court Victor | Gold | Winning side poster when case closes |

---

### `UserReward`
A badge award earned by a specific user (internal).

| Field | Type | Description |
|-------|------|-------------|
| `UserId` | `Guid` | Recipient |
| `BadgeCode` | `string` | Which badge |
| `SourceType` | `string` | Event type (`CaseCreate`, `CaseVote`, `CaseClose`) |
| `SourceId` | `Guid` | ID of the triggering case |
| `Reason` | `string` | Human-readable reason |
| `AwardedAtUtc` | `DateTime` | When awarded |

---

### `UserRewardView`
API-facing reward shape returned by `GET /api/users/{id}/rewards`.

| Field | Type | Description |
|-------|------|-------------|
| `BadgeCode` | `string` | Badge code |
| `BadgeLabel` | `string` | Display name |
| `IconKey` | `string` | UI icon identifier |
| `Tier` | `string` | `Bronze`, `Silver`, or `Gold` |
| `Reason` | `string` | Why it was awarded |
| `AwardedAtUtc` | `DateTime` | When awarded |

---

## Request DTOs

### `CreateCaseRequest`
Creates a new `Pending` case. Side B is filled in when the invited user accepts.

| Field | Type | Description |
|-------|------|-------------|
| `Title` | `string` | Case title |
| `Category` | `string` | Topic category |
| `Summary` | `string` | Neutral summary |
| `SideAUserId` | `Guid` | Case creator (Side A poster) |
| `SideAClaim` | `string` | Creator's argument |
| `InvitedUserId` | `Guid` | User invited to write Side B |

### `AcceptInvitationRequest`
Used by the invited user to accept and provide their Side B claim.

| Field | Type | Description |
|-------|------|-------------|
| `UserId` | `Guid` | Must match `InvitedUserId` on the case |
| `Claim` | `string` | Side B argument text |

### `DeclineInvitationRequest`
Used by the invited user to decline the invitation.

| Field | Type | Description |
|-------|------|-------------|
| `UserId` | `Guid` | Must match `InvitedUserId` on the case |

### `SendFriendRequestDto`
| Field | Type | Description |
|-------|------|-------------|
| `FromUserId` | `Guid` | User sending the request |
| `ToUserId` | `Guid` | User receiving the request |

### `RespondFriendRequestDto`
Used for both accept and decline friend request endpoints.

| Field | Type | Description |
|-------|------|-------------|
| `ActorUserId` | `Guid` | Must match `ToUserId` of the request |

### `CastVoteRequest`
| Field | Type |
|-------|------|
| `UserId` | `Guid` |
| `Side` | `CaseSide` |

### `CloseCaseRequest`
| Field | Type |
|-------|------|
| `ActorUserId` | `Guid` |
