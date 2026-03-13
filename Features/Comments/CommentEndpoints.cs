using HelpDeskApi.Data;
using HelpDeskApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskApi.Features.Comments;

public static class CommentEndpoints
{
    public static RouteGroupBuilder MapCommentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tickets/{ticketId:int}/comments");

        // Crear comentario
        group.MapPost("/", async (int ticketId, CreateCommentRequest req, AppDbContext db) =>
        {
            var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket is null)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            if (ticket.Status == TicketStatus.Closed)
                return Results.Conflict(new { message = "No se puede comentar un ticket cerrado." });

            var authorExists = await db.Users.AnyAsync(u => u.Id == req.AuthorId);
            if (!authorExists)
                return Results.NotFound(new { message = "AuthorId no existe." });

            var comment = new TicketComment
            {
                TicketId = ticketId,
                AuthorId = req.AuthorId,
                Body = req.Body.Trim(),
                IsInternal = req.IsInternal
            };

            db.TicketComments.Add(comment);

            // opcional: marcar updated
            ticket.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Created($"/tickets/{ticketId}/comments/{comment.Id}",
                new { comment.Id });
        });

        // Listar comentarios de un ticket
        group.MapGet("/", async (int ticketId, AppDbContext db) =>
        {
            var exists = await db.Tickets.AnyAsync(t => t.Id == ticketId);
            if (!exists)
                return Results.NotFound(new { message = "Ticket no encontrado." });

            var comments = await db.TicketComments
                .AsNoTracking()
                .Where(c => c.TicketId == ticketId)
                .OrderBy(c => c.CreatedAtUtc)
                .Select(c => new CommentListItem(
                    c.Id,
                    c.Body,
                    c.IsInternal,
                    new UserMini(c.Author!.Id, c.Author.DisplayName, c.Author.Email),
                    c.CreatedAtUtc
                ))
                .ToListAsync();

            return Results.Ok(comments);
        });

        return group;
    }
}

public record CreateCommentRequest(int AuthorId, string Body, bool IsInternal);

public record CommentListItem(
    int Id,
    string Body,
    bool IsInternal,
    UserMini Author,
    DateTime CreatedAtUtc
);

public record UserMini(int Id, string DisplayName, string Email);