using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;

namespace Fiap.TechChallenge.Execution.Application.UseCases;

public class FinalizarDiagnosticoUseCase
{
    private readonly IOrdemProducaoRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public FinalizarDiagnosticoUseCase(
        IOrdemProducaoRepository repository,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task<OrdemProducaoResponse> ExecuteAsync(
        Guid ordemProducaoId,
        Guid idMecanico,
        FinalizarDiagnosticoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ordem = await _repository.ObterPorIdAsync(ordemProducaoId, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(ordemProducaoId);

        var servicos = (request.ServicosSugeridos ?? [])
            .Select(s => new ServicoSugerido(s.IdServico, s.Descricao))
            .ToList();

        var pecas = (request.PecasSugeridas ?? [])
            .Select(p => new PecaSugerida(p.IdPeca, p.Quantidade, p.Descricao))
            .ToList();

        var agora = DateTime.UtcNow;
        ordem.FinalizarDiagnostico(idMecanico, request.LaudoTecnico, servicos, pecas, agora);

        await _repository.AtualizarAsync(ordem, cancellationToken);

        var diag = ordem.Diagnostico!;
        var evento = DiagnosticoTecnicoConcluidoEvent.Create(
            osId: ordem.OrdemServicoId,
            laudo: diag.LaudoTecnico!,
            servicos: diag.ServicosSugeridos
                .Select(s => new ServicoSugeridoItemEvent(s.IdServicoCatalogo, s.Descricao))
                .ToList(),
            pecas: diag.PecasSugeridas
                .Select(p => new PecaSugeridaItemEvent(p.IdPecaCatalogo, p.Quantidade, p.Descricao))
                .ToList(),
            duracaoMinutos: diag.ObterDuracaoMinutos());

        await _eventPublisher.PublishAsync(evento, cancellationToken);

        return OrdemProducaoResponse.FromDomain(ordem);
    }
}

