# HelpDesk API (Mini ServiceNow)

REST API for a HelpDesk / Ticketing System inspired by ServiceNow.
Built to practice backend engineering with .NET: clean endpoints, database persistence, auth, and business rules.

## Tech Stack
- C# / .NET 9
- ASP.NET Core (Minimal APIs)
- Entity Framework Core + SQLite (migrations)
- JWT Authentication + Role-based Authorization
- BCrypt password hashing
- Swagger / OpenAPI (Swashbuckle)

## Features
- Users
  - Create user
  - List users
- Tickets
  - Create ticket
  - List tickets (filters + pagination)
  - Get ticket details
  - Assign / unassign ticket
  - Update ticket status (workflow rules)
- Comments
  - Add ticket comments (public/internal)
  - List comments by ticket
- Auth
  - Register
  - Login (JWT)

## Run Locally
```bash
dotnet restore
dotnet tool run dotnet-ef database update
dotnet run