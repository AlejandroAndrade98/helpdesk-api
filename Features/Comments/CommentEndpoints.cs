using HelpDeskApi.Data;
using HelpDeskApi.Domain;
using HelpDeskApi.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskApi.Features.Comments;

public static class CommentEndpoints
{
    public static RouteGroupBuilder MapCommentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tickets/{ticketId:int}/comments")
            .RequireAuthorization();

        group.MapPost("/", async (int ticketId, CreateCommentRequest req, AppDbContext db, HttpContext http) =>
        {
            var userId = http.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Body) || req.Body.Trim().Length > 4000)
                return Results.BadRequest(new { message = "Body es requerido y máximo 4000 caracteres." });

            var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket is null)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            // Ownership: requester solo si es dueño
            if (!http.User.IsAgentOrAdmin() && ticket.CreatedById != userId.Value)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            // Solo Agent/Admin pueden crear internos
            if (req.IsInternal && !http.User.IsAgentOrAdmin())
                return Results.Forbid();

            if (ticket.Status == TicketStatus.Closed)
                return Results.Conflict(new { message = "No se puede comentar un ticket cerrado." });

            var comment = new TicketComment
            {
                TicketId = ticketId,
                AuthorId = userId.Value,
                Body = req.Body.Trim(),
                IsInternal = req.IsInternal
            };

            db.TicketComments.Add(comment);
            ticket.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Created($"/tickets/{ticketId}/comments/{comment.Id}", new { comment.Id });
        });

        group.MapGet("/", async (int ticketId, AppDbContext db, HttpContext http) =>
        {
            var userId = http.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var includeEmail = http.User.IsAgentOrAdmin();

            var ticket = await db.Tickets
                .AsNoTracking()
                .Where(t => t.Id == ticketId)
                .Select(t => new { t.Id, t.CreatedById })
                .FirstOrDefaultAsync();

            if (ticket is null)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            // Ownership: requester solo si es dueño
            if (!http.User.IsAgentOrAdmin() && ticket.CreatedById != userId.Value)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            var query = db.TicketComments
                .AsNoTracking()
                .Where(c => c.TicketId == ticketId);

            // Visibilidad: requester no ve internos
            if (!http.User.IsAgentOrAdmin())
                query = query.Where(c => !c.IsInternal);

            var comments = await query
                .OrderBy(c => c.CreatedAtUtc)
                .Select(c => new CommentListItem(
                    c.Id,
                    c.Body,
                    c.IsInternal,
                    new UserMini(c.Author!.Id, c.Author.DisplayName, includeEmail ? c.Author.Email : null),
                    c.CreatedAtUtc
                ))
                .ToListAsync();

            return Results.Ok(comments);
        });

        return group;
    }
}

public record CreateCommentRequest(string Body, bool IsInternal);

public record CommentListItem(
    int Id,
    string Body,
    bool IsInternal,
    UserMini Author,
    DateTime CreatedAtUtc
);

public record UserMini(int Id, string DisplayName, string? Email);