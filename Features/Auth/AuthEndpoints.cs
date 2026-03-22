using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HelpDeskApi.Data;
using HelpDeskApi.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HelpDeskApi.Features.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (
            RegisterRequest req,
            AppDbContext db) =>
        {
            var firstName = req.FirstName?.Trim() ?? string.Empty;
            var lastName = req.LastName?.Trim() ?? string.Empty;
            var email = req.Email.Trim().ToLowerInvariant();
            var displayName = $"{firstName} {lastName}".Trim();

            if (string.IsNullOrWhiteSpace(firstName))
                return Results.BadRequest(new { message = "FirstName is required." });

            if (string.IsNullOrWhiteSpace(lastName))
                return Results.BadRequest(new { message = "LastName is required." });

            if (string.IsNullOrWhiteSpace(email))
                return Results.BadRequest(new { message = "Email is required." });

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                return Results.BadRequest(new { message = "Password must be at least 8 characters long." });

            var exists = await db.Users.AnyAsync(u => u.Email == email);
            if (exists)
                return Results.Conflict(new { message = "A user with that email already exists." });

            var anyUserExists = await db.Users.AnyAsync();

            // Cambia Requester por Customer si ese es el nombre real de tu enum
            var assignedRole = anyUserExists ? UserRole.Requester : UserRole.Admin;

            var user = new User
            {
                DisplayName = displayName,
                Email = email,
                Role = assignedRole,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/users/{user.Id}", new
            {
                user.Id,
                user.DisplayName,
                user.Email,
                user.Role
            });
        });

        group.MapPost("/login", async (LoginRequest req, AppDbContext db, IConfiguration config) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
                return Results.Unauthorized();

            var ok = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
            if (!ok) return Results.Unauthorized();

            var token = CreateJwt(user, config);

            return Results.Ok(new
            {
                accessToken = token,
                user = new { user.Id, user.DisplayName, user.Email, user.Role }
            });
        });

        return group;
    }

    private static string CreateJwt(User user, IConfiguration config)
    {
        var key = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Missing Jwt:Key");

        var issuer = config["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Missing Jwt:Issuer");

        var audience = config["Jwt:Audience"]
            ?? throw new InvalidOperationException("Missing Jwt:Audience");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(6),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

public record RegisterRequest(string FirstName, string LastName, string Email, string Password);
public record LoginRequest(string Email, string Password);