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
| `Open` | Accepting votes |
| `Closed` | Voting ended, winner resolved |

### `UserRole`
| Value | Description |
|-------|-------------|
| `Member` | Standard community member |
| `Moderator` | Can close any case |

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
| `SideA` | `ArgumentPost` | Side A's argument |
| `SideB` | `ArgumentPost` | Side B's argument |
| `Verdict` | `CommunityVerdict` | Current vote counts |
| `Status` | `CaseStatus` | `Open` or `Closed` |
| `WinnerSide` | `CaseSide?` | `null` until closed; `null` on a tie |
| `CreatedAtUtc` | `DateTime` | When case was created |

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
| Field | Type |
|-------|------|
| `Title` | `string` |
| `Category` | `string` |
| `Summary` | `string` |
| `SideAUserId` | `Guid` |
| `SideAClaim` | `string` |
| `SideBUserId` | `Guid` |
| `SideBClaim` | `string` |

### `CastVoteRequest`
| Field | Type |
|-------|------|
| `UserId` | `Guid` |
| `Side` | `CaseSide` |

### `CloseCaseRequest`
| Field | Type |
|-------|------|
| `ActorUserId` | `Guid` |
