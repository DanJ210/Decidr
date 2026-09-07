---
description: Keeps docs/ authoritative and drift-free, and prevents .github/copilot-instructions.md from duplicating behavior documentation.
applyTo: "docs/**/*.md,.github/copilot-instructions.md,.github/instructions/**/*.md,backend/Controllers/**/*.cs,backend/Models/**/*.cs,backend/Services/ICommunityCourtService.cs,frontend/src/types.ts,frontend/src/services/api.ts"
---

# Documentation Consistency

## Ownership boundary

Each fact belongs to exactly one file. Do not restate it elsewhere — link instead.

| Topic | Owner |
|---|---|
| REST endpoints, request/response shapes, status codes, auth requirements | `docs/api-reference.md` |
| C# records, enums, and their TypeScript counterparts | `docs/data-models.md` |
| System design, auth flow, service layering, rate limiting | `docs/architecture.md` |
| Local setup, configuration, Entra enable/disable | `docs/getting-started.md` |
| Views, stores, router, UX | `docs/frontend.md` |
| Active product direction | `docs/video-implementation-plan.md` |
| Build, run, test, and validation commands; repo layout | `.github/copilot-instructions.md` |

`.github/copilot-instructions.md` describes **how to build and validate** the
repo. It must not become a second copy of the architecture or API reference. Its
Architecture Highlights section is navigation only; keep it to controller and
service names and link to `docs/` for detail.

On any conflict between `docs/` and `.github/copilot-instructions.md`, `docs/`
wins, and the instructions file is the one to correct.

## Update rules

Update the owning document **in the same change** as the code, not afterwards:

- Added, removed, or renamed an endpoint, or changed its route, auth attribute,
  request body, or response type → `docs/api-reference.md`
- Changed a record, enum, or entity in `backend/Models/` or `backend/Data/Entities/`,
  or its counterpart in `frontend/src/types.ts` → `docs/data-models.md`
- Changed `ICommunityCourtService`, added a service, or altered the auth or
  actor-resolution flow → `docs/architecture.md`
- Changed configuration keys, environment variables, or setup steps → `docs/getting-started.md`
- Added a view, store, or route → `docs/frontend.md`
- Changed build, run, test, or CI commands → `.github/copilot-instructions.md`

When adding a new document to `docs/`, add it to the index table in
`docs/README.md` and to the `docs/` tree in `.github/copilot-instructions.md`.

## Accuracy requirements

- Describe behavior that exists on the current branch. Do not document planned
  work as if it shipped; put forward-looking material in the plan document and
  mark it as planned.
- Do not describe the auth model as absent or as client-supplied user ids. The
  acting user is resolved server-side; `X-Dev-User-Id` is a Development-only
  fallback that applies solely when Entra is not configured.
- Keep test-coverage descriptions general enough to survive new tests, or update
  them when the covered areas change. Do not state exact test counts.
- Verify endpoint lists against `backend/Controllers/` and model lists against
  `backend/Models/` before editing, rather than copying an existing list.

## Review pass

When asked to check docs for drift, report findings as a table of
claim / location / actual behavior / suggested fix, and flag any fact that
appears in more than one owner file as a duplication to collapse into a link.
