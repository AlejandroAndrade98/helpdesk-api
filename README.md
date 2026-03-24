# HelpDesk API (Mini ServiceNow)

REST API for a HelpDesk / Ticketing System inspired by ServiceNow.
Built to practice backend engineering with .NET: clean minimal APIs, database persistence, auth, business rules, tests, and real deployment.

## Tech Stack
- C# / .NET 9
- ASP.NET Core (Minimal APIs)
- Entity Framework Core
  - SQLite (local dev)
  - PostgreSQL (Docker + Supabase)
- JWT Authentication + Role-based Authorization
- BCrypt password hashing
- Swagger / OpenAPI (Swashbuckle)
- Docker (production-like runtime + local Postgres)
- xUnit integration tests (`Microsoft.AspNetCore.Mvc.Testing`)

## Features
### Auth
- Register (`POST /auth/register`)
- Login (JWT) (`POST /auth/login`)

### Users
- Get current user (`GET /users/me`)
- List users (`GET /users`)
- List agents (Agent/Admin only) (`GET /users/agents`)

### Tickets
- Create ticket (`POST /tickets`)
- List tickets (pagination + optional filters) (`GET /tickets`)
- Get ticket details (`GET /tickets/{id}`)
- Assign/unassign ticket (Agent/Admin only) (`PATCH /tickets/{id}/assign`)
- Update ticket status (workflow rules) (`PATCH /tickets/{id}/status`)

### Comments
- Add ticket comment (Agent/Admin can post internal notes) (`POST /tickets/{ticketId}/comments`)
- List ticket comments (`GET /tickets/{ticketId}/comments`)
  - Public comments are visible to Requesters
  - Internal comments are restricted (role-based)

## API Docs (Swagger)
When enabled:
- `/swagger`

> Note: In the current setup Swagger is typically enabled in Development. If you want it in Production, enable it explicitly.

---

## Configuration
This API is environment-driven. You can switch providers and behavior without code changes.

### Environment Variables
**Database**
- `Db__Provider`
  - `sqlite` (default)
  - `postgres`
- `Db__AutoMigrate`
  - `true` to run `db.Database.Migrate()` at startup (recommended for demo environments)
- `ConnectionStrings__Default`
  - SQLite example: `Data Source=helpdesk.db`
  - Postgres example:
    `Host=localhost;Port=15432;Database=helpdesk;Username=postgres;Password=postgres;SSL Mode=Disable`

**JWT**
- `Jwt__Key` (required)
- `Jwt__Issuer` (required)
- `Jwt__Audience` (required)

**CORS (for browser frontends)**
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc.
  - Example:
    - `Cors__AllowedOrigins__0=https://your-frontend-domain.com`
    - `Cors__AllowedOrigins__1=http://localhost:5173`

---

## Run Locally

### Option A: Docker (Postgres) — recommended
This spins up:
- Postgres in a container (data persisted in a Docker volume)
- API container on port `8080`

```bash
docker compose up --build