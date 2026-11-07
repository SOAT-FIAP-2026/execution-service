using System.Diagnostics;
using Fiap.TechChallenge.Contracts.Common;

namespace Fiap.TechChallenge.Execution.Api.Middlewares;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = CorrelationContext.HeaderName;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString().Trim()
            : Guid.NewGuid().ToString();

        context.Items[HeaderName] = correlationId;
        context.Request.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag(CorrelationContext.LogScopeKey, correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (CorrelationContext.Push(correlationId))
        using (logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationContext.LogScopeKey] = correlationId,
            ["trace_id"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
        }))
        {
            await next(context);
        }
    }
}

