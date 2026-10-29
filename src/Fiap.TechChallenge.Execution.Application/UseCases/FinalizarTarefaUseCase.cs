using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;

namespace Fiap.TechChallenge.Execution.Application.UseCases;

public class FinalizarTarefaUseCase
{
    private readonly IOrdemProducaoRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public FinalizarTarefaUseCase(
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
        Guid tarefaId,
        Guid idMecanico,
        string? observacoes = null,
        CancellationToken cancellationToken = default)
    {
        var ordem = await _repository.ObterPorIdAsync(ordemProducaoId, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(ordemProducaoId);

        var agora = DateTime.UtcNow;
        var todasConcluidas = ordem.FinalizarTarefa(tarefaId, idMecanico, observacoes, agora);

        await _repository.AtualizarAsync(ordem, cancellationToken);

        if (todasConcluidas)
        {
            var evento = ProducaoFinalizadaEvent.Create(
                ordem.OrdemServicoId,
                ordem.DataConclusao ?? agora,
                ordem.ObterDuracaoTotalTarefasMinutos());

            await _eventPublisher.PublishAsync(evento, cancellationToken);
        }

        return OrdemProducaoResponse.FromDomain(ordem);
    }
}

