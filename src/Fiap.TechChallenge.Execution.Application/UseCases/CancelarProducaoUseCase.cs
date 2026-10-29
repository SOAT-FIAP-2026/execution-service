using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;

namespace Fiap.TechChallenge.Execution.Application.UseCases;

public class CancelarProducaoUseCase
{
    private readonly IOrdemProducaoRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public CancelarProducaoUseCase(
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
        string motivo,
        CancellationToken cancellationToken = default)
    {
        var ordem = await _repository.ObterPorIdAsync(ordemProducaoId, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(ordemProducaoId);

        if (ordem.Status == StatusOrdemProducao.Cancelada)
        {
            return OrdemProducaoResponse.FromDomain(ordem);
        }

        var agora = DateTime.UtcNow;
        ordem.Cancelar(motivo, agora);

        await _repository.AtualizarAsync(ordem, cancellationToken);

        var evento = ProducaoCanceladaEvent.Create(
            ordem.OrdemServicoId,
            motivo.Trim(),
            agora);

        await _eventPublisher.PublishAsync(evento, cancellationToken);

        return OrdemProducaoResponse.FromDomain(ordem);
    }
}

