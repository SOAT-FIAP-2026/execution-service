using System.Text.Json.Serialization;
using Fiap.TechChallenge.Execution.Api.Configurations;
using Fiap.TechChallenge.Execution.Api.Middlewares;
using Fiap.TechChallenge.Execution.Application;
using Fiap.TechChallenge.Execution.Infrastructure;
using Fiap.TechChallenge.Execution.Infrastructure.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Logging.AddOpenTelemetry(options =>
    {
        options.IncludeScopes = true;
        options.IncludeFormattedMessage = true;
        options.ParseStateValues = true;
        options.AddOtlpExporter();
    });
}

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration);
builder.Services.AddObservabilityConfiguration(builder.Configuration);
builder.Services.AddExecutionApplication();
builder.Services.AddExecutionInfrastructure(builder.Configuration);

// Readiness: verificação do PostgreSQL (Etapa 3) e RabbitMQ (Etapa 5) com a tag "ready".
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgresql", tags: ["ready"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

// Liveness: apenas confirma que o processo responde (nenhuma dependência externa).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: executa somente as verificações marcadas com a tag "ready".
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();

/// <summary>Exposto para testes de integração/BDD com WebApplicationFactory.</summary>
public partial class Program;
