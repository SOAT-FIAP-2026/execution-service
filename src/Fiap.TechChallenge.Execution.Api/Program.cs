using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Execution Service — Diagnóstico e Produção",
        Version = "v1",
        Description = "Chão de oficina: diagnóstico técnico, fila de produção e apontamento de tarefas."
    });
});

// Readiness: as verificações de PostgreSQL e RabbitMQ (tag "ready") entram nas Etapas 3 e 5.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Liveness: apenas confirma que o processo responde (nenhuma dependência externa).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: executa somente as verificações marcadas com a tag "ready".
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

/// <summary>Exposto para testes de integração/BDD com WebApplicationFactory.</summary>
public partial class Program;
