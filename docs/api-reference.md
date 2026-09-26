# API Reference

Base URL: `/api`

All request and response bodies are JSON. Enum values are serialized as strings (e.g., `"Open"`, `"A"`).

In `Entra` mode, authenticated requests require a valid Entra v2 access token
with the delegated `access_as_user` scope. Mutation actors and private viewer
state are derived from that token. In `SeededTesting` mode, `X-Dev-User-Id`
identifies a seeded actor instead.

In `Entra` mode, controller endpoints require `access_as_user` by
default. The case feed, case detail, comments, evidence metadata, result, and
authentication configuration actions are the only anonymous API surfaces. Pending
cases and their related public read surfaces remain visible only to Side A, the invited/Side B user, or a moderator.

---

## Configuration

### `GET /api/config/authentication`
Returns the active runtime authentication mode before the SPA initializes its
authentication client. This endpoint is anonymous in both modes.

**Response `200 OK`** — `{ "mode": "Entra" | "SeededTesting" }`

---

## Authentication

### `GET /api/auth/me`
Returns the local Decidr profile mapped from the caller's valid Entra access
token. The endpoint returns `401 Unauthorized` for anonymous requests in both
authentication modes; the seeded-account workflow uses the user directory and
does not call this endpoint.

**Response `200 OK`** — authenticated `AppUser`
**Response `401 Unauthorized`** — missing, invalid, or unmapped identity

---

## Cases

### `GET /api/cases`
Returns all `Open` and `Closed` cases ordered by creation date descending. `Pending` cases are excluded.

**Response `200 OK`** — `ArgumentCase[]`

### `GET /api/cases/feed`
Returns a cursor-paginated page of publication-ready `Open` and `Closed` cases.
The opaque cursor is ordered by creation timestamp and case ID.

**Query parameters**

| Name | Type | Description |
|------|------|-------------|
| `cursor` | `string?` | Cursor returned by the previous page |
| `limit` | `int?` | Number of cases, clamped to 1-20; default 5 |

**Response `200 OK`** — `CaseFeedPage` (`items`, `nextCursor`, `hasMore`)
**Response `400 Bad Request`** — invalid cursor

### `POST /api/cases/{id}/report`
Submits an authenticated user's report for a case. Reports are queued for
moderation and are not exposed through the public feed response.

**Request body** — `{ "reason": "string" }`, 1-256 characters

**Response `202 Accepted`** — report queued
**Response `400 Bad Request`** — invalid reason
**Response `401 Unauthorized`** — actor unavailable

### `GET /api/cases/moderation/reports`
Returns queued case reports for moderators.

**Response `200 OK`** — `ModerationReport[]`
**Response `401 Unauthorized`** — actor unavailable
**Response `403 Forbidden`** — actor is not a moderator

### `POST /api/cases/{id}/moderation`
Sets the moderator visibility state for a case. Hidden cases are excluded from
the cursor feed.

**Request body** — `{ "hidden": true }`

**Response `204 No Content`** — moderation state changed
**Response `403 Forbidden`** — actor is not a moderator
**Response `404 Not Found`** — case does not exist

### `GET /api/cases/observability/metrics`
Returns in-process trust, upload, moderation, and playback counters to moderators.

**Response `200 OK`** — `object` containing metric names and counts
**Response `403 Forbidden`** — actor is not a moderator

### `POST /api/users/{id}/block` and `DELETE /api/users/{id}/block`
Blocks or unblocks a user for the authenticated actor. Blocked participants are
removed from the actor's feed results.

**Response `204 No Content`** — state changed
**Response `400 Bad Request`** — actor attempted to block themselves
**Response `404 Not Found`** — target user does not exist (block only)
**Response `404 Not Found`** — case does not exist

### `POST /api/cases/{id}/playback-events`
Records an authenticated playback event for analytics.

**Request body** — `{ "side": "A" | "B", "event": "string", "positionSeconds": 0 }`

**Response `204 No Content`** — event accepted
**Response `400 Bad Request`** — invalid event
**Response `401 Unauthorized`** — actor unavailable

---

### `GET /api/cases/{id}`
Returns an `Open` or `Closed` case by GUID. A `Pending` case is returned only when
the resolved actor is Side A, the invited/Side B user, or a moderator. When an
actor is available, `currentUserVote` is populated for that actor.

**Response `200 OK`** — `ArgumentCase`  
**Response `404 Not Found`** — case does not exist or a pending case is not visible to the caller

---

### `GET /api/cases/{id}/vote-status`
Returns whether the authenticated actor has already voted on a case.

**Validation**
- Case must exist.
- The request must resolve to an authenticated actor.

**Response `200 OK`**
```json
{
  "hasVoted": true
}
```
**Response `401 Unauthorized`** — actor is unavailable or invalid  
**Response `404 Not Found`** — case does not exist

---

### `POST /api/cases/media`
Uploads a case video and returns the URL to store on the case. Called before
creating a case or accepting an invitation, so the clip is partitioned by
uploader rather than by case.

Media supplied on creation or acceptance is recorded as `Ready`; sides with no
media stay `None`. The public feed requires both sides to have ready media, so
text-only and partial cases remain hidden.

**Request** — `multipart/form-data`

| Field | Type | Description |
|-------|------|-------------|
| `file` | file | The video clip |

**Validation**
- The authenticated actor must resolve to a Decidr profile.
- Extension must be `.mp4`, `.m4v`, `.mov`, or `.webm`.
- File contents must match the extension's signature.
- Size cannot exceed 64 MB.
- The server reads the container duration; it must be between 1 and 30 seconds.

**Response `200 OK`** — `CaseMediaUploadResponse`  
**Response `400 Bad Request`** — validation failure message  
**Response `401 Unauthorized`** — unresolved actor

### `POST /api/cases/media/initiate`
Starts an authorized media upload session for an authenticated user before the file is submitted. The session is owned by the resolved actor and stays in `Pending` until content is uploaded.

**Request body**
```json
{
  "fileName": "clip.webm",
  "contentType": "video/webm",
  "sizeBytes": 1536000,
  "durationSeconds": 12
}
```

**Validation**
- Authenticated actor must resolve to a Decidr profile.
- File name must use an allowed video extension and stay within the 64 MB limit.
- Duration must be present and between 1 and 30 seconds when supplied.

**Response `200 OK`** — `CaseMediaUploadSession` with `status: "Pending"`
**Response `400 Bad Request`** — validation failure message  
**Response `401 Unauthorized`** — unresolved actor

### `PUT /api/cases/media/{uploadId}/content`
Uploads the raw video bytes to the authorized session. The request body must be
the complete file and its `Content-Length` must match the initiated session.

**Response `202 Accepted`** — content stored for processing
**Response `400 Bad Request`** — size or upload state mismatch
**Response `403 Forbidden`** — upload belongs to another user
**Response `404 Not Found`** — unknown upload id

### `POST /api/cases/media/{uploadId}/finalize`
Queues an uploaded session for asynchronous validation. The upload must belong
to the current user and have content stored. The legacy multipart body remains
accepted for compatibility.

**Request** — `multipart/form-data`

| Field | Type | Description |
|-------|------|-------------|
| `file` | file | The final video clip |

**Validation**
- `uploadId` must exist and belong to the authenticated user.
- File contents must match the selected video type and the stored metadata.
- Duration must be determined and remain within the 30-second limit.

**Response `202 Accepted`** — `CaseMediaUploadStatusResponse` with `status: "Processing"`
**Response `400 Bad Request`** — validation or state failure message  
**Response `401 Unauthorized`** — unresolved actor  
**Response `403 Forbidden`** — upload belongs to another user  
**Response `404 Not Found`** — unknown upload id

### `GET /api/cases/media/{uploadId}/status`
Polls the status of a previously initiated upload session. Processing validates
the stored signature and container duration. Rejected objects are deleted;
ready responses include the playback URL and server-derived metadata.

**Response `200 OK`** — `CaseMediaUploadStatusResponse` with `status: "Pending" | "Processing" | "Ready" | "Failed"`
**Response `401 Unauthorized`** — unresolved actor  
**Response `403 Forbidden`** — upload belongs to another user  
**Response `404 Not Found`** — unknown upload id

---

### `GET /api/cases/media/{ownerId}/{fileName}`
Streams an uploaded case video. Anonymous, because cases are publicly viewable,
and range requests are enabled so clips can seek.

Both segments must be 32-character hex identifiers with an allowed video
extension; anything else returns `404` without touching storage.

**Response `200 OK`** — video stream  
**Response `404 Not Found`** — unknown or malformed key

---

### `POST /api/cases`
Creates a new debate case in `Pending` status. Side B is not set yet — the invited user must accept to add their claim and make the case `Open`.

**Request body**
```json
{
  "title": "string",
  "category": "string",
  "summary": "string",
  "sideAClaim": "string",
  "invitedUserId": "guid",
  "sideARecordUrl": "string | null",
  "sideAThumbnailUrl": "string | null",
  "sideADurationSeconds": "number | null"
}
```

**Validation**
- All text fields must be non-empty.
- The authenticated actor and `invitedUserId` must be different.
- The invited user must exist.
- The authenticated actor and invited user must be connected as accepted friends.
- The media fields are optional and are stored as supplied.

**Response `201 Created`** — `ArgumentCase` (status `Pending`) with `Location` header  
**Response `400 Bad Request`** — validation failure message

---

### `POST /api/cases/{id}/accept`
The invited user accepts the invitation and provides their Side B claim. The case moves to `Open`.

**Request body**
```json
{
  "claim": "string",
  "sideBRecordUrl": "string | null",
  "sideBThumbnailUrl": "string | null",
  "sideBDurationSeconds": "number | null"
}
```

**Validation**
- Case must exist and be `Pending`.
- The authenticated actor must match `invitedUserId` on the case.
- `claim` must be non-empty.
- The media fields are optional and are stored as supplied.

**Response `200 OK`** — updated `ArgumentCase` (status `Open`)  
**Response `400 Bad Request`** — error message

---

### `POST /api/cases/{id}/decline`
The invited user declines the invitation. The case moves to `Closed` with no winner.

**Validation**
- Case must exist and be `Pending`.
- The authenticated actor must match `invitedUserId` on the case.

**Response `204 No Content`**  
**Response `400 Bad Request`** — error message

---

### `POST /api/cases/{id}/vote`
Casts a community vote on a case.

**Request body**
```json
{
  "side": "A" | "B"
}
```

**Validation**
- Case must exist and be `Open`.
- The authenticated actor must not be a participant in the case (Side A or Side B poster).
- The authenticated actor may only vote once per case; changing an existing vote is not supported.

**Response `200 OK`** — updated `ArgumentCase`  
**Response `400 Bad Request`** — error message

---

### `POST /api/cases/{id}/close`
Closes a case and determines the winner.

**Request body**
```json
{
  "actorUserId": "guid"
}
```

**Validation**
- `actorUserId` must exist.
- Case must exist and be `Open`.
- Actor must be a case participant (Side A or B poster) or a Moderator.
- Closing an already-closed case is a no-op (returns `200`).

**Response `200 OK`** — updated `ArgumentCase`  
**Response `400 Bad Request`** — error message

---

### `GET /api/cases/{id}/comments`
Returns all comments for a case in chronological order. Comments are case-level (one shared pool), not side-specific.
Pending-case comments use the same participant/invitee/moderator visibility rule
as case detail.

**Response `200 OK`** — `CaseComment[]`  
**Response `404 Not Found`** — case does not exist

---

### `POST /api/cases/{id}/comments`
Adds a new case-level comment to the shared comment pool.

**Request body**
```json
{
  "userId": "guid",
  "message": "string"
}
```

**Validation**
- Case must exist.
- `userId` must exist.
- `message` must be non-empty and at most 1024 characters.

**Response `200 OK`** — created `CaseComment`  
**Response `400 Bad Request`** — error message

---

### `GET /api/cases/{id}/evidence`
Returns side-scoped supporting materials for a case.
Pending-case evidence metadata uses the same participant/invitee/moderator
visibility rule as case detail.

**Response `200 OK`**
```json
{
  "sideA": [],
  "sideB": []
}
```
Each list contains `CaseEvidenceItem` entries.
Uploaded-file entries expose an authenticated application content URL rather than
an Azure Blob URL or internal storage key.

**Response `404 Not Found`** — case does not exist

---

### `GET /api/cases/{id}/evidence/{evidenceId}/content`
Streams an uploaded evidence file from private storage through the authenticated
API. External link evidence is not available through this endpoint.

**Response `200 OK`** — binary content with its validated media type and a safe download filename

**Response `401 Unauthorized`** — actor is unavailable or invalid

**Response `404 Not Found`** — case, evidence metadata, or stored object does not exist

---

### `POST /api/cases/{id}/evidence/link`
Adds a new link evidence item to one side of an open case.

**Request body**
```json
{
  "side": "A" | "B",
  "title": "string",
  "url": "https://example.com/source"
}
```

**Validation**
- Case must exist and be `Open`.
- The authenticated actor must own the targeted side.
- `title` is required (max 160 chars).
- `url` must be a valid `http` or `https` URL.
- Targeted side can hold at most 20 evidence items.

**Response `200 OK`** — created `CaseEvidenceItem`  
**Response `400 Bad Request`** — validation or permission error

---

### `POST /api/cases/{id}/evidence/upload`
Uploads a document/image evidence item and attaches it to one side of an open case.

**Request content type**
- `multipart/form-data`

**Form fields**
- `side` (`A` or `B`)
- `title` (`string`, optional; defaults to filename without extension)
- `file` (`binary`, required)

**Validation**
- Case must exist and be `Open`.
- The authenticated actor must own the targeted side.
- File is required and must be non-empty.
- Max file size: 10 MB.
- Allowed extensions/types:
  - Images: `jpg`, `jpeg`, `png`, `webp`, `gif`
  - Documents: `pdf`, `txt`, `doc`, `docx`
- File bytes must match the claimed file type; DOCX uploads must contain the
  expected Open XML document structure and text files must be valid UTF-8.
- Targeted side can hold at most 20 evidence items.

The API stores the object in private evidence storage before writing metadata. If
the metadata write fails, it deletes the uploaded object as rollback.

**Response `200 OK`** — created `CaseEvidenceItem`  
**Response `400 Bad Request`** — validation or permission error

---

### `GET /api/cases/{id}/result`
Returns a summary of the case outcome.

**Response `200 OK`**
```json
{
  "id": "guid",
  "status": "Pending" | "Open" | "Closed",
  "winnerSide": "A" | "B" | null,
  "verdict": {
    "votesForSideA": 0,
    "votesForSideB": 0
  }
}
```
**Response `404 Not Found`** — case does not exist

---

## Users

### `GET /api/users`
Returns all registered users.

**Response `200 OK`** — `AppUser[]`

---

### `GET /api/users/records`
Returns public player records. Qualified players are ranked first; provisional
players follow.

**Response `200 OK`** — `PlayerRecord[]`

---

### `GET /api/users/{id}/record`
Returns one user's public player record.

**Response `200 OK`** — `PlayerRecord`  
**Response `404 Not Found`** — user does not exist

---

### `GET /api/users/{id}/rewards`
Returns reward badges earned by a user.

**Response `200 OK`** — `UserRewardView[]`  
**Response `404 Not Found`** — user does not exist

---

### `GET /api/users/{id}/friends`
Returns the user's accepted friends.

**Response `200 OK`** — `AppUser[]`  
**Response `404 Not Found`** — user does not exist

---

### `GET /api/users/{id}/friend-requests`
Returns incoming pending friend requests for the user.

**Response `200 OK`** — `FriendRequest[]`  
**Response `404 Not Found`** — user does not exist

---

### `GET /api/users/{id}/sent-requests`
Returns outgoing pending friend requests sent by the user.

**Response `200 OK`** — `FriendRequest[]`  
**Response `404 Not Found`** — user does not exist

---

### `GET /api/users/{id}/invitations`
Returns pending case invitations where the user is the invited Side B participant.

**Response `200 OK`** — `ArgumentCase[]` (all `Pending` status)  
**Response `404 Not Found`** — user does not exist

---

## Friends

### `POST /api/friends/request`
Sends a friend request from one user to another.

**Request body**
```json
{
  "fromUserId": "guid",
  "toUserId": "guid"
}
```

**Validation**
- Both users must exist and be different.
- No existing accepted friendship or pending request between them.

**Response `204 No Content`**  
**Response `400 Bad Request`** — error message

---

### `POST /api/friends/{requestId}/accept`
Accepts a pending friend request. Only the recipient (`toUserId`) may accept.

**Request body**
```json
{
  "actorUserId": "guid"
}
```

**Response `204 No Content`**  
**Response `400 Bad Request`** — error message

---

### `POST /api/friends/{requestId}/decline`
Declines a pending friend request. Only the recipient (`toUserId`) may decline.

**Request body**
```json
{
  "actorUserId": "guid"
}
```

**Response `204 No Content`**  
**Response `400 Bad Request`** — error message

---

### `POST /api/friends/remove`
Removes an accepted friendship connection between two users.

**Request body**
```json
{
  "actorUserId": "guid",
  "friendUserId": "guid"
}
```

**Validation**
- Both users must exist and be different.
- Users must already be connected as accepted friends.

**Response `204 No Content`**  
**Response `400 Bad Request`** — error message
