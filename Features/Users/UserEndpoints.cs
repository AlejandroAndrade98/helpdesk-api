using HelpDeskApi.Data;
using HelpDeskApi.Domain;
using HelpDeskApi.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskApi.Features.Users;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/users").RequireAuthorization();

        // GET /users/me
        group.MapGet("/me", async (HttpContext http, AppDbContext db, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("UsersMe");

            try
            {
                var userId = http.User.GetUserId();
                logger.LogInformation("GET /users/me userId={UserId}", userId);

                if (userId is null)
                    return Results.Unauthorized();

                var user = await db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId.Value)
                    .Select(u => new UserResponse(
                        u.Id,
                        u.DisplayName,
                        u.Email,
                        u.Role
                    ))
                    .FirstOrDefaultAsync();

                return user is null
                    ? Results.NotFound(new { message = "User not found." })
                    : Results.Ok(user);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GET /users/me failed");
                return Results.Problem(
                    title: "Users/me failed",
                    detail: ex.Message,
                    statusCode: 500
                );
            }
        });

        // GET /users
        // Solo Admin
        group.MapGet("/", async (HttpContext http, AppDbContext db) =>
        {
            if (!http.User.IsInRole(UserRole.Admin.ToString()))
                return Results.Forbid();

            var users = await db.Users
                .AsNoTracking()
                .OrderBy(u => u.DisplayName)
                .Select(u => new UserResponse(
                    u.Id,
                    u.DisplayName,
                    u.Email,
                    u.Role
                ))
                .ToListAsync();

            return Results.Ok(users);
        });

        // GET /users/agents
        // Solo Agent o Admin
        group.MapGet("/agents", async (HttpContext http, AppDbContext db) =>
        {
            if (!http.User.IsAgentOrAdmin())
                return Results.Forbid();

            var agents = await db.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Agent || u.Role == UserRole.Admin)
                .OrderBy(u => u.DisplayName)
                .Select(u => new UserResponse(
                    u.Id,
                    u.DisplayName,
                    u.Email,
                    u.Role
                ))
                .ToListAsync();

            return Results.Ok(agents);
        });

        // PATCH /users/{id}/role
        // Solo Admin
        group.MapPatch("/{id:int}/role", async (
            int id,
            UpdateUserRoleRequest req,
            HttpContext http,
            AppDbContext db) =>
        {
            if (!http.User.IsInRole(UserRole.Admin.ToString()))
                return Results.Forbid();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null)
                return Results.NotFound(new { message = "User not found." });

            // Evita dejar el sistema sin admins
            if (user.Role == UserRole.Admin && req.Role != UserRole.Admin)
            {
                var adminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin);
                if (adminCount <= 1)
                {
                    return Results.BadRequest(new
                    {
                        message = "You cannot remove the last admin."
                    });
                }
            }

            user.Role = req.Role;
            await db.SaveChangesAsync();

            return Results.Ok(new UserResponse(
                user.Id,
                user.DisplayName,
                user.Email,
                user.Role
            ));
        });

        return group;
    }
}

public record UpdateUserRoleRequest(UserRole Role);

public record UserResponse(
    int Id,
    string DisplayName,
    string Email,
    UserRole Role
);