using HelpDeskApi.Data;
using HelpDeskApi.Features.Users;
using Microsoft.EntityFrameworkCore;
using HelpDeskApi.Features.Tickets;
using HelpDeskApi.Features.Comments;

using System.Text;
using HelpDeskApi.Features.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer(); 

builder.Services.AddSwaggerGen(options =>
{
    const string schemeId = "bearer";

    options.SwaggerDoc("v1", new OpenApiInfo { Title = "HelpDeskApi", Version = "v1" });

    options.AddSecurityDefinition(schemeId, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    // Forma nueva: requirement basado en el documento + reference helper
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(schemeId, document)] = new List<string>()
    });
});

// EF Core (SQLite o Postgres según config)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var provider = builder.Configuration["Db:Provider"]?.ToLowerInvariant();
    var conn = builder.Configuration.GetConnectionString("Default");

    if (provider == "postgres")
    {
        options.UseNpgsql(conn, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);

            npgsqlOptions.CommandTimeout(60);
        });
    }
    else
    {
        options.UseSqlite(conn ?? "Data Source=helpdesk.db");
    }
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta Jwt:Key en la configuración.");

        var issuer = builder.Configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Falta Jwt:Issuer en la configuración.");

        var audience = builder.Configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Falta Jwt:Audience en la configuración.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;

                var uri = new Uri(origin);

                return origin == "http://localhost:5173"
                    || origin == "http://localhost:3000"
                    || origin == "https://lovable.dev"
                    || uri.Host.EndsWith(".lovable.app");
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Middleware temporal para medir tiempos de cada request
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();

    await next();

    sw.Stop();

    Console.WriteLine(
        $"[{DateTime.UtcNow:O}] {context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> {context.Response.StatusCode} in {sw.ElapsedMilliseconds}ms");
});

// Health simple: mide si el contenedor/API está vivo
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        ok = true,
        service = "HelpDeskApi",
        environment = app.Environment.EnvironmentName,
        utc = DateTime.UtcNow
    });
});

// DB health: mide conexión básica a la base
app.MapGet("/db-health", async (AppDbContext db) =>
{
    var sw = Stopwatch.StartNew();

    var canConnect = await db.Database.CanConnectAsync();

    sw.Stop();

    return Results.Ok(new
    {
        ok = canConnect,
        elapsedMs = sw.ElapsedMilliseconds,
        utc = DateTime.UtcNow
    });
});

// DB ping real: ejecuta una consulta mínima para medir lectura real
app.MapGet("/db-ping", async (AppDbContext db) =>
{
    var sw = Stopwatch.StartNew();

    var userCount = await db.Users.CountAsync();

    sw.Stop();

    return Results.Ok(new
    {
        ok = true,
        elapsedMs = sw.ElapsedMilliseconds,
        userCount,
        utc = DateTime.UtcNow
    });
});
// Auto-migrate: en Development siempre, en Production solo si Db:AutoMigrate=true
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Db:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}


if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

// Endpoints del proyecto
app.MapUserEndpoints();
app.MapTicketEndpoints();
app.MapCommentEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program { }