using System.Security.Claims;
using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Application.Queries;
using Fiap.TechChallenge.Execution.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiap.TechChallenge.Execution.Api.Controllers.V1;

[ApiController]
[Route("api/v1/producao")]
[Authorize]
public sealed class ProducaoController(
    ObterFilaProducaoQuery obterFilaProducaoQuery,
    ObterDiagnosticosPendentesQuery obterDiagnosticosPendentesQuery,
    ObterDetalhesOrdemProducaoQuery obterDetalhesOrdemProducaoQuery,
    ObterMetricasProdutividadeQuery obterMetricasProdutividadeQuery,
    IniciarDiagnosticoUseCase iniciarDiagnosticoUseCase,
    FinalizarDiagnosticoUseCase finalizarDiagnosticoUseCase,
    IniciarTarefaUseCase iniciarTarefaUseCase,
    FinalizarTarefaUseCase finalizarTarefaUseCase,
    CancelarProducaoUseCase cancelarProducaoUseCase) : ControllerBase
{
    [HttpGet("fila")]
    [Authorize(Roles = "Mecanico,Operador,Administrador")]
    [ProducesResponseType(typeof(IReadOnlyList<OrdemProducaoFilaItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrdemProducaoFilaItemResponse>>> ObterFila(
        [FromQuery] string? status,
        [FromQuery] string? filialId,
        CancellationToken cancellationToken)
    {
        var resultado = await obterFilaProducaoQuery.ExecuteAsync(status, filialId, cancellationToken);
        return Ok(resultado);
    }

    [HttpGet("diagnosticos/pendentes")]
    [Authorize(Roles = "Mecanico,Administrador")]
    [ProducesResponseType(typeof(IReadOnlyList<OrdemProducaoFilaItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrdemProducaoFilaItemResponse>>> ObterDiagnosticosPendentes(
        [FromQuery] string? filialId,
        CancellationToken cancellationToken)
    {
        var resultado = await obterDiagnosticosPendentesQuery.ExecuteAsync(filialId, cancellationToken);
        return Ok(resultado);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Mecanico,Operador,Administrador")]
    [ProducesResponseType(typeof(OrdemProducaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdemProducaoResponse>> ObterPorId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resultado = await obterDetalhesOrdemProducaoQuery.ObterPorIdAsync(id, cancellationToken);
        return Ok(resultado);
    }

    [HttpGet("ordem-servico/{osId:guid}")]
    [Authorize(Roles = "Mecanico,Operador,Administrador")]
    [ProducesResponseType(typeof(OrdemProducaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdemProducaoResponse>> ObterPorOrdemServicoId(
        Guid osId,
        CancellationToken cancellationToken)
    {
        var resultado = await obterDetalhesOrdemProducaoQuery.ObterPorOrdemServicoIdAsync(osId, cancellationToken);
        return Ok(resultado);
    }

    [HttpPatch("{id:guid}/diagnostico/iniciar")]
    [Authorize(Roles = "Mecanico,Administrador")]
    [ProducesResponseType(typeof(OrdemProducaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrdemProducaoResponse>> IniciarDiagnostico(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (idMecanico, nomeMecanico) = ObterIdentidadeMecanico();
        var resultado = await iniciarDiagnosticoUseCase.ExecuteAsync(id, idMecanico, nomeMecanico, cancellationToken);
        return Ok(resultado);
    }

    [HttpPost("{id:guid}/diagnostico/finalizar")]
    [Authorize(Roles = "Mecanico,Administrador")]
    [ProducesResponseType(typeof(OrdemProducaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrdemProducaoResponse>> FinalizarDiagnostico(
        Guid id,
        [FromBody] FinalizarDiagnosticoRequest request,
        CancellationToken cancellationToken)
    {
        var (idMecanico, _) = ObterIdentidadeMecanico();
        var resultado = await finalizarDiagnosticoUseCase.ExecuteAsync(id, idMecanico, request, cancellationToken);
        return Ok(resultado);
    }

    [HttpPatch("{id:guid}/tarefas/{tarefaId:guid}/iniciar")]
    [Authorize(Roles = "Mecanico,Administrador")]
    [ProducesResponseType(typeof(OrdemProducaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrdemProducaoResponse>> IniciarTarefa(
        Guid id,
        Guid tarefaId,
        CancellationToken cancellationToken)
    {
        var (idMecanico, nomeMecanico) = ObterIdentidadeMecanico();
        var resultado = await iniciarTarefaUseCase.ExecuteAsync(id, tarefaId, idMecanico, nomeMecanico, cancellationToken);
        return Ok(resultado);
    }

    [HttpPatch("{id:guid}/tarefas/{tarefaId:guid}/finalizar")]
    [Authorize(Roles = "Mecanico,Administrador")]
    [ProducesResponseType(typeof(OrdemProducaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrdemProducaoResponse>> FinalizarTarefa(
        Guid id,
        Guid tarefaId,
        [FromBody] FinalizarTarefaRequest? request,
        CancellationToken cancellationToken)
    {
        var (idMecanico, _) = ObterIdentidadeMecanico();
        var resultado = await finalizarTarefaUseCase.ExecuteAsync(
            id,
            tarefaId,
            idMecanico,
            request?.Observacoes,
            cancellationToken);
        return Ok(resultado);
    }

    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(OrdemProducaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrdemProducaoResponse>> CancelarProducao(
        Guid id,
        [FromBody] CancelarProducaoRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await cancelarProducaoUseCase.ExecuteAsync(id, request.Motivo, cancellationToken);
        return Ok(resultado);
    }

    [HttpGet("metricas/produtividade")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(RelatorioProdutividadeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RelatorioProdutividadeResponse>> ObterMetricasProdutividade(
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] Guid? idMecanico,
        CancellationToken cancellationToken)
    {
        var resultado = await obterMetricasProdutividadeQuery.ExecuteAsync(de, ate, idMecanico, cancellationToken);
        return Ok(resultado);
    }

    private (Guid IdMecanico, string NomeMecanico) ObterIdentidadeMecanico()
    {
        var rawId = User.FindFirstValue("UsuarioId")
                    ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawId, out var idMecanico) || idMecanico == Guid.Empty)
            throw new UnauthorizedAccessException("Claim 'UsuarioId' ausente ou inválida no token JWT.");

        var nomeMecanico = User.FindFirstValue(ClaimTypes.Name)
                           ?? User.Identity?.Name
                           ?? User.FindFirstValue("unique_name")
                           ?? "Mecânico";

        return (idMecanico, nomeMecanico);
    }
}
