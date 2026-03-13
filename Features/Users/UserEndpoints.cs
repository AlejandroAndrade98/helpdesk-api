using HelpDeskApi.Data;
using HelpDeskApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskApi.Features.Users;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/users");
        

        group.MapPost("/", async (CreateUserRequest req, AppDbContext db) =>
        {
            var email = req.Email.Trim().ToLower();

            var exists = await db.Users.AnyAsync(u => u.Email == email);
            if (exists)
                return Results.Conflict(new { message = "Ya existe un usuario con ese email." });

            var user = new User
            {
                DisplayName = req.DisplayName.Trim(),
                Email = email,
                Role = req.Role
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/users/{user.Id}", new UserResponse(user.Id, user.DisplayName, user.Email, user.Role));
        });

        group.MapGet("/", async (AppDbContext db) =>
{
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new UserResponse(u.Id, u.DisplayName, u.Email, u.Role))
            .ToListAsync();

            return Results.Ok(users);
        });

        return group;
    }

        
}

public record CreateUserRequest(string DisplayName, string Email, UserRole Role);
public record UserResponse(int Id, string DisplayName, string Email, UserRole Role);