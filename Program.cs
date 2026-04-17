using HelpDeskApi.Data;
using HelpDeskApi.Features.Users;
using Microsoft.EntityFrameworkCore;
using HelpDeskApi.Features.Tickets;
using HelpDeskApi.Features.Comments;
using HelpDeskApi.Features.Reports;
using System.Text;
using HelpDeskApi.Features.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

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

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(schemeId, document)] = new List<string>()
    });
});

// EF Core, solo Postgres / Supabase
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default para Postgres.");

    options.UseNpgsql(conn, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);

        npgsqlOptions.CommandTimeout(60);
    });
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
                    || uri.Host.EndsWith(".lovable.app")
                    || uri.Host.EndsWith(".vercel.app");
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    sw.Stop();

    Console.WriteLine(
        $"[{DateTime.UtcNow:O}] {context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> {context.Response.StatusCode} in {sw.ElapsedMilliseconds}ms");
});

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

app.MapUserEndpoints();
app.MapTicketEndpoints();
app.MapCommentEndpoints();
app.MapReportEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program { }