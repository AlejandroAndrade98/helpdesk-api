using HelpDeskApi.Data;
using HelpDeskApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskApi.Features.Tickets;

public static class TicketEndpoints
{
    public static RouteGroupBuilder MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tickets");

        // Crear ticket
        group.MapPost("/", async (CreateTicketRequest req, AppDbContext db) =>
        {
            var creatorExists = await db.Users.AnyAsync(u => u.Id == req.CreatedById);
            if (!creatorExists)
                return Results.NotFound(new { message = "CreatedById no existe." });

            if (req.AssignedToId is not null)
            {
                var assigneeExists = await db.Users.AnyAsync(u => u.Id == req.AssignedToId);
                if (!assigneeExists)
                    return Results.NotFound(new { message = "AssignedToId no existe." });
            }

            var ticket = new Ticket
            {
                Title = req.Title.Trim(),
                Description = req.Description.Trim(),
                Priority = req.Priority,
                Status = TicketStatus.New,
                CreatedById = req.CreatedById,
                AssignedToId = req.AssignedToId
            };

            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();

            return Results.Created($"/tickets/{ticket.Id}", new { ticket.Id });
        });

        // Listar tickets (básico)
        group.MapGet("/", async (
            int? status,
            int? priority,
            int? assignedToId,
            int page,
            int pageSize,
            AppDbContext db) =>
        {
            // defaults
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var query = db.Tickets
                .AsNoTracking()
                .AsQueryable();

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
                    new UserMini(t.CreatedBy!.Id, t.CreatedBy.DisplayName, t.CreatedBy.Email),
                    t.AssignedTo == null ? null : new UserMini(t.AssignedTo.Id, t.AssignedTo.DisplayName, t.AssignedTo.Email),
                    t.CreatedAtUtc
                ))
                .ToListAsync();

            return Results.Ok(new
            {
                page,
                pageSize,
                total,
                items
            });
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var ticket = await db.Tickets
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new TicketDetailResponse(
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Status,
                    t.Priority,
                    new UserMini(t.CreatedBy!.Id, t.CreatedBy.DisplayName, t.CreatedBy.Email),
                    t.AssignedTo == null ? null : new UserMini(t.AssignedTo.Id, t.AssignedTo.DisplayName, t.AssignedTo.Email),
                    t.CreatedAtUtc,
                    t.UpdatedAtUtc
                ))
                .FirstOrDefaultAsync();

            return ticket is null
            ? Results.NotFound(new { message = "Ticket no encontrado." })
            : Results.Ok(ticket);
        });

        group.MapPatch("/{id:int}/status", async (int id, UpdateTicketStatusRequest req, AppDbContext db) =>
        
        {
            var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            // Regla: no permitir cambios si está Closed
            if (ticket.Status == TicketStatus.Closed)
                return Results.Conflict(new { message = "No se puede cambiar el estado de un ticket cerrado." });

            // Regla: no permitir pasar a Closed si no está Resolved
            if (req.Status == TicketStatus.Closed && ticket.Status != TicketStatus.Resolved)
                return Results.Conflict(new { message = "Solo se puede cerrar un ticket que esté en Resolved." });

            ticket.Status = req.Status;
            ticket.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new { ticket.Id, ticket.Status, ticket.UpdatedAtUtc });
        })  .RequireAuthorization(policy => policy.RequireRole("Agent", "Admin"));;

        // Asignar o desasignar ticket
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

                // Regla: solo Agent/Admin pueden ser asignados (opcional pero realista)
                if (user.Role != UserRole.Agent && user.Role != UserRole.Admin)
                    return Results.Conflict(new { message = "Solo un Agent o Admin puede ser asignado a un ticket." });
            }

            ticket.AssignedToId = req.AssignedToId;
            ticket.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();


            return Results.Ok(new { ticket.Id, ticket.AssignedToId, ticket.UpdatedAtUtc });
        }) .RequireAuthorization(policy => policy.RequireRole("Agent", "Admin"));

        return group;
    }
}



public record CreateTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority,
    int CreatedById,
    int? AssignedToId
);

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

public record UserMini(int Id, string DisplayName, string Email);
public record UpdateTicketStatusRequest(TicketStatus Status);
public record AssignTicketRequest(int? AssignedToId);