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

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=helpdesk.db"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = builder.Configuration["Jwt:Key"]!;
        var issuer = builder.Configuration["Jwt:Issuer"]!;
        var audience = builder.Configuration["Jwt:Audience"]!;

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

var app = builder.Build();



if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Endpoints del proyecto
app.MapUserEndpoints();
app.MapTicketEndpoints();
app.MapCommentEndpoints();
app.MapAuthEndpoints();

app.Run();