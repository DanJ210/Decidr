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

### `CaseEvidenceType`
| Value | Description |
|-------|-------------|
| `Link` | External URL evidence |
| `Image` | Uploaded image evidence |
| `Document` | Uploaded document evidence |

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

### `PlayerRecord`
An outcome-derived public record for a user.

| Field | Type | Description |
|-------|------|-------------|
| `UserId` | `Guid` | Player identifier |
| `UserName` | `string` | Player handle |
| `DisplayName` | `string` | Player display name |
| `Wins` | `int` | Closed, decided cases won |
| `Losses` | `int` | Closed, decided cases lost |
| `Ties` | `int` | Closed cases with no winner |
| `CompletedCases` | `int` | Wins, losses, and ties combined |
| `WinRate` | `double` | Wins divided by decided cases |
| `IsQualified` | `bool` | Whether the player completed at least three cases |
| `Rank` | `int?` | Qualified standing, or `null` while provisional |

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

### `CaseComment`
A public comment posted to a case discussion. Comments are shared at the case level (not tied to Side A or Side B).

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier |
| `CaseId` | `Guid` | Case this comment belongs to |
| `UserId` | `Guid` | Comment author's ID |
| `UserName` | `string` | Comment author's handle |
| `Message` | `string` | Comment text (max 1024 chars) |
| `CreatedAtUtc` | `DateTime` | When the comment was posted |

---

### `CaseEvidenceItem`
A side-scoped supporting item attached to a case.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier |
| `CaseId` | `Guid` | Case this evidence belongs to |
| `Side` | `CaseSide` | Side A or Side B |
| `AddedByUserId` | `Guid` | User who added the evidence |
| `AddedByUserName` | `string` | Evidence author's handle |
| `Type` | `CaseEvidenceType` | `Link`, `Image`, or `Document` |
| `Title` | `string` | Display title (max 160 chars) |
| `ResourceUrl` | `string` | External URL or uploaded file URL |
| `MimeType` | `string?` | MIME type for uploaded files; `null` for links |
| `SizeBytes` | `long?` | File size for uploads; `null` for links |
| `CreatedAtUtc` | `DateTime` | When evidence was added |

---

### `CaseEvidenceCollection`
Public API shape returned by `GET /api/cases/{id}/evidence`.

| Field | Type | Description |
|-------|------|-------------|
| `SideA` | `CaseEvidenceItem[]` | Evidence items for Side A |
| `SideB` | `CaseEvidenceItem[]` | Evidence items for Side B |

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
| `ChangeCount` | `int` | Deprecated legacy field; currently always `0` |
| `CreatedAtUtc` | `DateTime` | When the vote was cast |

---

### `CaseVoteStatus`
API-facing vote status for a specific user and case.

| Field | Type | Description |
|-------|------|-------------|
| `hasVoted` | `bool` | `true` when the requested user has already cast a vote on the case |

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

### `RemoveFriendDto`
Used to remove an accepted friend connection.

| Field | Type | Description |
|-------|------|-------------|
| `ActorUserId` | `Guid` | User performing the removal |
| `FriendUserId` | `Guid` | Friend to disconnect |

### `CastVoteRequest`
| Field | Type |
|-------|------|
| `UserId` | `Guid` |
| `Side` | `CaseSide` |

### `CloseCaseRequest`
| Field | Type |
|-------|------|
| `ActorUserId` | `Guid` |

### `CreateCaseCommentRequest`
| Field | Type | Description |
|-------|------|-------------|
| `UserId` | `Guid` | Author posting the comment |
| `Message` | `string` | Comment text |

### `AddCaseEvidenceLinkRequest`
Used by side owners to add external links as evidence.

| Field | Type | Description |
|-------|------|-------------|
| `UserId` | `Guid` | Must match the owner of the selected side |
| `Side` | `CaseSide` | Side to attach evidence to |
| `Title` | `string` | Evidence label (non-empty, max 160 chars) |
| `Url` | `string` | Must be a valid `http`/`https` URL |

### `AddCaseEvidenceFileRequest`
Internal service contract used after upload handling.

| Field | Type | Description |
|-------|------|-------------|
| `UserId` | `Guid` | Must match the owner of the selected side |
| `Side` | `CaseSide` | Side to attach evidence to |
| `Type` | `CaseEvidenceType` | `Image` or `Document` |
| `Title` | `string` | Evidence label (non-empty, max 160 chars) |
| `ResourceUrl` | `string` | Public URL of the stored uploaded file |
| `MimeType` | `string` | Uploaded file MIME type |
| `SizeBytes` | `long` | Uploaded file size |
