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
            AppDbContext db,
            HttpContext http,
            IHostEnvironment env) =>
        {
            var email = req.Email.Trim().ToLower();

            // Validaciones básicas (pro)
            if (string.IsNullOrWhiteSpace(req.DisplayName))
                return Results.BadRequest(new { message = "DisplayName es requerido." });

            if (string.IsNullOrWhiteSpace(email))
                return Results.BadRequest(new { message = "Email es requerido." });

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                return Results.BadRequest(new { message = "Password debe tener mínimo 8 caracteres." });

            // ✅ Política de register para uso real
            var anyUserExists = await db.Users.AnyAsync();

            if (!env.IsDevelopment())
            {
                if (!anyUserExists)
                {
                    // Primer usuario (bootstrap): debe ser Admin
                    if (req.Role != UserRole.Admin)
                        return Results.BadRequest(new { message = "El primer usuario debe ser Admin." });
                }
                else
                {
                    // Después del bootstrap: solo Admin autenticado puede registrar
                    if (!(http.User?.Identity?.IsAuthenticated ?? false) || !http.User.IsInRole("Admin"))
                        return Results.Forbid();
                }
            }

            var exists = await db.Users.AnyAsync(u => u.Email == email);
            if (exists)
                return Results.Conflict(new { message = "Ya existe un usuario con ese email." });

            var user = new User
            {
                DisplayName = req.DisplayName.Trim(),
                Email = email,
                Role = req.Role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/users/{user.Id}",
                new { user.Id, user.DisplayName, user.Email, user.Role });
        });

        group.MapPost("/login", async (LoginRequest req, AppDbContext db, IConfiguration config) =>
        {
            var email = req.Email.Trim().ToLower();

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
        var key = config["Jwt:Key"]!;
        var issuer = config["Jwt:Issuer"]!;
        var audience = config["Jwt:Audience"]!;

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

public record RegisterRequest(string DisplayName, string Email, string Password, UserRole Role);
public record LoginRequest(string Email, string Password);