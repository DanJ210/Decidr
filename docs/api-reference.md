# API Reference

Base URL: `/api`

All request and response bodies are JSON. Enum values are serialized as strings (e.g., `"Open"`, `"A"`).

---

## Cases

### `GET /api/cases`
Returns all `Open` and `Closed` cases ordered by creation date descending. `Pending` cases are excluded.

**Response `200 OK`** — `ArgumentCase[]`

---

### `GET /api/cases/{id}`
Returns a single case by GUID (any status, including `Pending`).

**Response `200 OK`** — `ArgumentCase`  
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
  "sideAUserId": "guid",
  "sideAClaim": "string",
  "invitedUserId": "guid"
}
```

**Validation**
- All text fields must be non-empty.
- `sideAUserId` and `invitedUserId` must be different.
- Both user IDs must exist.
- `sideAUserId` and `invitedUserId` must be connected as accepted friends.

**Response `201 Created`** — `ArgumentCase` (status `Pending`) with `Location` header  
**Response `400 Bad Request`** — validation failure message

---

### `POST /api/cases/{id}/accept`
The invited user accepts the invitation and provides their Side B claim. The case moves to `Open`.

**Request body**
```json
{
  "userId": "guid",
  "claim": "string"
}
```

**Validation**
- Case must exist and be `Pending`.
- `userId` must match `invitedUserId` on the case.
- `claim` must be non-empty.

**Response `200 OK`** — updated `ArgumentCase` (status `Open`)  
**Response `400 Bad Request`** — error message

---

### `POST /api/cases/{id}/decline`
The invited user declines the invitation. The case moves to `Closed` with no winner.

**Request body**
```json
{
  "userId": "guid"
}
```

**Validation**
- Case must exist and be `Pending`.
- `userId` must match `invitedUserId` on the case.

**Response `204 No Content`**  
**Response `400 Bad Request`** — error message

---

### `POST /api/cases/{id}/vote`
Casts a community vote on a case.

**Request body**
```json
{
  "userId": "guid",
  "side": "A" | "B"
}
```

**Validation**
- Case must exist and be `Open`.
- User must exist.
- User must not be a participant in the case (Side A or Side B poster).
- First vote creates the vote record.
- A voter may switch sides once after their initial vote.
- Additional changes after that are rejected.

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
