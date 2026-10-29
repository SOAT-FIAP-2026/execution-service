using System.Text.Json;
using Fiap.TechChallenge.Execution.Api.Middlewares;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fiap.TechChallenge.Execution.UnitTests.Api;

public class ExceptionHandlingMiddlewareTests
{
    public static TheoryData<Exception, int> ExceptionStatusCases => new()
    {
        { new OrdemProducaoNaoEncontradaException(Guid.NewGuid()), StatusCodes.Status404NotFound },
        { new TarefaProducaoNaoEncontradaException(Guid.NewGuid()), StatusCodes.Status404NotFound },
        { new TransicaoEstadoInvalidaException("Transição inválida"), StatusCodes.Status409Conflict },
        { new ConflitoConcorrenciaException(Guid.NewGuid()), StatusCodes.Status409Conflict },
        { new MecanicoNaoAutorizadoException("Outro mecânico"), StatusCodes.Status403Forbidden },
        { new DomainException("Regra violada"), StatusCodes.Status400BadRequest },
        { new UnauthorizedAccessException("Token inválido"), StatusCodes.Status401Unauthorized },
        { new InvalidOperationException("Erro inesperado"), StatusCodes.Status500InternalServerError }
    };

    [Theory]
    [MemberData(nameof(ExceptionStatusCases))]
    public async Task InvokeAsync_QuandoExcecaoLancada_DeveRetornarProblemDetailsComStatusCorreto(
        Exception exceptionToThrow,
        int expectedStatusCode)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/producao/teste";
        context.Items[CorrelationIdMiddleware.HeaderName] = "corr-123";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exceptionToThrow,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(expectedStatusCode);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(expectedStatusCode);
        problem.Instance.Should().Be("/api/v1/producao/teste");
    }
}
