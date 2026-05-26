# Decidr Documentation

**Decidr** is a community debate platform where users submit two-sided arguments and the community votes on who wins. Cases can be closed by participants or moderators, at which point a winner is declared and reward badges are distributed.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 8 (Web API) |
| Frontend | Vue 3 + TypeScript + Vite |
| State management | Pinia |
| HTTP client | Axios |
| API docs (dev) | Swagger / OpenAPI (Swashbuckle) |
| Data store | In-memory (no database) |

## Documentation Index

| Document | Description |
|----------|-------------|
| [Getting Started](./getting-started.md) | How to run the app locally |
| [Architecture](./architecture.md) | System layout and component relationships |
| [API Reference](./api-reference.md) | All REST endpoints with request/response shapes |
| [Data Models](./data-models.md) | C# records/enums and their TypeScript counterparts |
| [Frontend Guide](./frontend.md) | Views, stores, router, and service layer |
