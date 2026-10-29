using System.Text.Json.Serialization;
using Fiap.TechChallenge.Execution.Api.Configurations;
using Fiap.TechChallenge.Execution.Api.Middlewares;
using Fiap.TechChallenge.Execution.Application;
using Fiap.TechChallenge.Execution.Infrastructure;
using Fiap.TechChallenge.Execution.Infrastructure.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration);
builder.Services.AddExecutionApplication();
builder.Services.AddExecutionInfrastructure(builder.Configuration);

// Readiness: verificação do PostgreSQL (Etapa 3) e RabbitMQ (Etapa 5) com a tag "ready".
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgresql", tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness: apenas confirma que o processo responde (nenhuma dependência externa).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: executa somente as verificações marcadas com a tag "ready".
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

/// <summary>Exposto para testes de integração/BDD com WebApplicationFactory.</summary>
public partial class Program;
