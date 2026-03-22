using HelpDeskApi.Data;
using HelpDeskApi.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskApi.Features.Users;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/users")
            .RequireAuthorization();

        // Info del usuario autenticado
        group.MapGet("/me", async (AppDbContext db, HttpContext http) =>
        {
            var userId = http.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var me = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => new { u.Id, u.DisplayName, u.Email, u.Role })
                .FirstOrDefaultAsync();

            return me is null ? Results.Unauthorized() : Results.Ok(me);
        });

        // Lista de agentes (para asignación), solo Agent/Admin
        group.MapGet("/agents", async (AppDbContext db) =>
        {
            var agents = await db.Users
                .AsNoTracking()
                .Where(u => u.Role == Domain.UserRole.Agent || u.Role == Domain.UserRole.Admin)
                .OrderBy(u => u.DisplayName)
                .Select(u => new { u.Id, u.DisplayName, u.Email, u.Role })
                .ToListAsync();

            return Results.Ok(agents);
        })
        .RequireAuthorization(policy => policy.RequireRole("Agent", "Admin"));

        // Lista completa de usuarios, solo Admin
        group.MapGet("/", async (AppDbContext db) =>
        {
            var users = await db.Users
                .AsNoTracking()
                .OrderBy(u => u.Id)
                .Select(u => new { u.Id, u.DisplayName, u.Email, u.Role })
                .ToListAsync();

            return Results.Ok(users);
        })
        .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return group;
    }
}