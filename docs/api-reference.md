# API Reference

Base URL: `/api`

All request and response bodies are JSON. Enum values are serialized as strings (e.g., `"Open"`, `"A"`).

Authenticated requests require a valid Entra v2 access token with the delegated
`access_as_user` scope. Mutation actors and private viewer state are derived from
that token. In Development only, when Entra is not configured, `X-Dev-User-Id`
may identify a seeded local actor.

When Entra is configured, controller endpoints require `access_as_user` by
default. Only the case feed, case detail, comments, evidence metadata, and result
actions explicitly allow anonymous access. Pending cases and their related public
read surfaces remain visible only to Side A, the invited/Side B user, or a moderator.

---

## Cases

### `GET /api/cases`
Returns all `Open` and `Closed` cases ordered by creation date descending. `Pending` cases are excluded.

**Response `200 OK`** — `ArgumentCase[]`

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

### `POST /api/cases`
Creates a new debate case in `Pending` status. Side B is not set yet — the invited user must accept to add their claim and make the case `Open`.

**Request body**
```json
{
  "title": "string",
  "category": "string",
  "summary": "string",
  "sideAClaim": "string",
  "invitedUserId": "guid"
}
```

**Validation**
- All text fields must be non-empty.
- The authenticated actor and `invitedUserId` must be different.
- The invited user must exist.
- The authenticated actor and invited user must be connected as accepted friends.

**Response `201 Created`** — `ArgumentCase` (status `Pending`) with `Location` header  
**Response `400 Bad Request`** — validation failure message

---

### `POST /api/cases/{id}/accept`
The invited user accepts the invitation and provides their Side B claim. The case moves to `Open`.

**Request body**
```json
{
  "claim": "string"
}
```

**Validation**
- Case must exist and be `Pending`.
- The authenticated actor must match `invitedUserId` on the case.
- `claim` must be non-empty.

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
