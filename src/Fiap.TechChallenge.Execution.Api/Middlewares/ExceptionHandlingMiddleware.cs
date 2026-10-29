using System.Diagnostics;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Fiap.TechChallenge.Execution.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            OrdemProducaoNaoEncontradaException or TarefaProducaoNaoEncontradaException =>
                (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            TransicaoEstadoInvalidaException =>
                (StatusCodes.Status409Conflict, "Transição de estado inválida"),
            ConflitoConcorrenciaException =>
                (StatusCodes.Status409Conflict, "Conflito de concorrência otimista"),
            MecanicoNaoAutorizadoException =>
                (StatusCodes.Status403Forbidden, "Operação não autorizada para o mecânico"),
            DomainException or ArgumentException =>
                (StatusCodes.Status400BadRequest, "Regra de negócio violada"),
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Não autenticado"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno no servidor")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Erro não tratado ao processar requisição {Path}", context.Request.Path);
        else
            logger.LogWarning(exception, "Exceção de domínio ({StatusCode}) em {Path}: {Message}", statusCode, context.Request.Path, exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode >= 500 ? "Ocorreu um erro inesperado ao processar a requisição." : exception.Message,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        if (context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId) && correlationId is not null)
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json");
    }
}
