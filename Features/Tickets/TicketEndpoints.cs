using HelpDeskApi.Data;
using HelpDeskApi.Domain;
using HelpDeskApi.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskApi.Features.Tickets;

public static class TicketEndpoints
{
    public static RouteGroupBuilder MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tickets")
            .RequireAuthorization();

        // Crear ticket
        group.MapPost("/", async (CreateTicketRequest req, AppDbContext db, HttpContext http) =>
        {
            var userId = http.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            // Validaciones básicas (pro)
            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Trim().Length > 120)
                return Results.BadRequest(new { message = "Title es requerido y máximo 120 caracteres." });

            if (string.IsNullOrWhiteSpace(req.Description) || req.Description.Trim().Length > 4000)
                return Results.BadRequest(new { message = "Description es requerido y máximo 4000 caracteres." });

            // ✅ Pro: Requester no debería asignar al crear
            if (req.AssignedToId is not null && !http.User.IsAgentOrAdmin())
                return Results.Forbid();

            if (req.AssignedToId is not null)
            {
                // ✅ Pro: el AssignedTo debe existir y ser Agent/Admin
                var assignee = await db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == req.AssignedToId)
                    .Select(u => new { u.Id, u.Role })
                    .FirstOrDefaultAsync();

                if (assignee is null)
                    return Results.NotFound(new { message = "AssignedToId no existe." });

                if (assignee.Role != UserRole.Agent && assignee.Role != UserRole.Admin)
                    return Results.Conflict(new { message = "Solo un Agent o Admin puede ser asignado a un ticket." });
            }

            var ticket = new Ticket
            {
                Title = req.Title.Trim(),
                Description = req.Description.Trim(),
                Priority = req.Priority,
                Status = TicketStatus.New,
                CreatedById = userId.Value,
                AssignedToId = req.AssignedToId
            };

            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();

            return Results.Created($"/tickets/{ticket.Id}", new { ticket.Id });
        });

        // Listar tickets (filters + paging + ownership + privacy)
        group.MapGet("/", async (
            int? status,
            int? priority,
            int? assignedToId,
            int page,
            int pageSize,
            AppDbContext db,
            HttpContext http) =>
        {
            var userId = http.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var includeEmail = http.User.IsAgentOrAdmin();

            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var query = db.Tickets.AsNoTracking().AsQueryable();

            // ✅ Ownership: requester solo ve sus tickets
            if (!http.User.IsAgentOrAdmin())
                query = query.Where(t => t.CreatedById == userId.Value);

            if (status is not null)
                query = query.Where(t => (int)t.Status == status);

            if (priority is not null)
                query = query.Where(t => (int)t.Priority == priority);

            if (assignedToId is not null)
                query = query.Where(t => t.AssignedToId == assignedToId);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(t => t.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TicketListItem(
                    t.Id,
                    t.Title,
                    t.Status,
                    t.Priority,
                    new UserMini(t.CreatedBy!.Id, t.CreatedBy.DisplayName, includeEmail ? t.CreatedBy.Email : null),
                    t.AssignedTo == null ? null : new UserMini(t.AssignedTo.Id, t.AssignedTo.DisplayName, includeEmail ? t.AssignedTo.Email : null),
                    t.CreatedAtUtc
                ))
                .ToListAsync();

            return Results.Ok(new { page, pageSize, total, items });
        });

        // Detalle ticket (ownership + privacy)
        group.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var userId = http.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var includeEmail = http.User.IsAgentOrAdmin();

            var ticketQuery = db.Tickets.AsNoTracking().Where(t => t.Id == id);

            // ✅ Ownership: requester solo si es dueño
            if (!http.User.IsAgentOrAdmin())
                ticketQuery = ticketQuery.Where(t => t.CreatedById == userId.Value);

            var ticket = await ticketQuery
                .Select(t => new TicketDetailResponse(
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Status,
                    t.Priority,
                    new UserMini(t.CreatedBy!.Id, t.CreatedBy.DisplayName, includeEmail ? t.CreatedBy.Email : null),
                    t.AssignedTo == null ? null : new UserMini(t.AssignedTo.Id, t.AssignedTo.DisplayName, includeEmail ? t.AssignedTo.Email : null),
                    t.CreatedAtUtc,
                    t.UpdatedAtUtc
                ))
                .FirstOrDefaultAsync();

            return ticket is null
                ? Results.NotFound(new { message = "Ticket no encontrado." })
                : Results.Ok(ticket);
        });

        // Cambiar estado (solo Agent/Admin)
        group.MapPatch("/{id:int}/status", async (int id, UpdateTicketStatusRequest req, AppDbContext db) =>
        {
            var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            if (ticket.Status == TicketStatus.Closed)
                return Results.Conflict(new { message = "No se puede cambiar el estado de un ticket cerrado." });

            if (req.Status == TicketStatus.Closed && ticket.Status != TicketStatus.Resolved)
                return Results.Conflict(new { message = "Solo se puede cerrar un ticket que esté en Resolved." });

            var now = DateTime.UtcNow;
            ticket.UpdatedAtUtc = now;

            if (req.Status == TicketStatus.Resolved)
                ticket.ResolvedAtUtc = now;

            if (req.Status == TicketStatus.Closed && ticket.ClosedAtUtc is null)
                ticket.ClosedAtUtc = now;

            if (ticket.Status == TicketStatus.Resolved && req.Status != TicketStatus.Resolved && req.Status != TicketStatus.Closed)
                ticket.ResolvedAtUtc = null;

            ticket.Status = req.Status;
            await db.SaveChangesAsync();

            return Results.Ok(new { ticket.Id, ticket.Status, ticket.UpdatedAtUtc, ticket.ResolvedAtUtc, ticket.ClosedAtUtc });
        })
        .RequireAuthorization(policy => policy.RequireRole("Agent", "Admin"));

        // Asignar (solo Agent/Admin)
        group.MapPatch("/{id:int}/assign", async (int id, AssignTicketRequest req, AppDbContext db) =>
        {
            var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            if (ticket.Status == TicketStatus.Closed)
                return Results.Conflict(new { message = "No se puede reasignar un ticket cerrado." });

            if (req.AssignedToId is not null)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.AssignedToId);
                if (user is null)
                    return Results.NotFound(new { message = "AssignedToId no existe." });

                if (user.Role != UserRole.Agent && user.Role != UserRole.Admin)
                    return Results.Conflict(new { message = "Solo un Agent o Admin puede ser asignado a un ticket." });
            }

            ticket.AssignedToId = req.AssignedToId;
            ticket.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new { ticket.Id, ticket.AssignedToId, ticket.UpdatedAtUtc });
        })
        .RequireAuthorization(policy => policy.RequireRole("Agent", "Admin"));

        return group;
    }
}

public record CreateTicketRequest(string Title, string Description, TicketPriority Priority, int? AssignedToId);

public record TicketListItem(
    int Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    UserMini CreatedBy,
    UserMini? AssignedTo,
    DateTime CreatedAtUtc
);

public record TicketDetailResponse(
    int Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    UserMini CreatedBy,
    UserMini? AssignedTo,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record UserMini(int Id, string DisplayName, string? Email);

public record UpdateTicketStatusRequest(TicketStatus Status);
public record AssignTicketRequest(int? AssignedToId);