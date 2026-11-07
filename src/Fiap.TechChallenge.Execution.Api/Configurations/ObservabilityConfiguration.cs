using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Fiap.TechChallenge.Execution.Api.Configurations;

public static class ObservabilityConfiguration
{
    public static IServiceCollection AddObservabilityConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OTEL_SERVICE_NAME"]
            ?? configuration["Observability:ServiceName"]
            ?? "techchallenge-execution-service";
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes([
                    new KeyValuePair<string, object>("service.namespace", "techchallenge")
                ]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    metrics.AddOtlpExporter();
            });

        return services;
    }
}

