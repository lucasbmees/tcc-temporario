using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TccSharkTank.Application.Abstractions.Security;
using TccSharkTank.Infrastructure;
using TccSharkTank.WebApi.Middleware;
using TccSharkTank.WebApi.Security;
// Adicione o namespace do seu DbContext se necessário (ex: using TccSharkTank.Infrastructure.Persistence;)

var builder = WebApplication.CreateBuilder(args);

// Configurações de Serviços
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TccSharkTank API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Injeção de Dependência da Infraestrutura e Segurança
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddTransient<ExceptionHandlingMiddleware>();

// Configuração de Autenticação JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = jwtSection["Key"] ?? "dev-secret-change-me-please-dev-secret-change-me-please";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ==========================================
// BLOCO PARA FORÇAR CRIAÇÃO DAS TABELAS
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Substitua 'AppDbContext' pelo nome exato da sua classe de contexto se for diferente
        var context = services.GetRequiredService<TccSharkTank.Infrastructure.Persistence.AppDbContext>();
        context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocorreu um erro ao criar as tabelas do banco de dados.");
    }
}
// ==========================================

// Pipeline de Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger habilitado para todos os ambientes
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TccSharkTank API v1");
    c.RoutePrefix = ""; 
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
