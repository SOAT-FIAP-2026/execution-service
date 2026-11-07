using Fiap.TechChallenge.Contracts.Common;
using Fiap.TechChallenge.Execution.Api.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fiap.TechChallenge.Execution.UnitTests.Api;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_QuandoHeaderPresente_DevePropagarParaCorrelationContextERequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "corr-exec-123";

        string? capturedAmbientCorrelationId = null;

        var middleware = new CorrelationIdMiddleware(
            ctx =>
            {
                capturedAmbientCorrelationId = CorrelationContext.Current;
                return Task.CompletedTask;
            },
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        capturedAmbientCorrelationId.Should().Be("corr-exec-123");
        context.Items[CorrelationIdMiddleware.HeaderName].Should().Be("corr-exec-123");
        context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be("corr-exec-123");
        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_QuandoHeaderAusente_DeveGerarNovoCorrelationIdEPropagarParaCorrelationContext()
    {
        var context = new DefaultHttpContext();
        string? capturedAmbientCorrelationId = null;

        var middleware = new CorrelationIdMiddleware(
            ctx =>
            {
                capturedAmbientCorrelationId = CorrelationContext.Current;
                return Task.CompletedTask;
            },
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        capturedAmbientCorrelationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(capturedAmbientCorrelationId, out _).Should().BeTrue();
        context.Items[CorrelationIdMiddleware.HeaderName].Should().Be(capturedAmbientCorrelationId);
        CorrelationContext.Current.Should().BeNull();
    }
}

