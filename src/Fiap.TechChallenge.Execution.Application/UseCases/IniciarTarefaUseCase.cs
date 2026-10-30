using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;

namespace Fiap.TechChallenge.Execution.Application.UseCases;

public class IniciarTarefaUseCase
{
    private readonly IOrdemProducaoRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public IniciarTarefaUseCase(
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
        string nomeMecanico,
        CancellationToken cancellationToken = default)
    {
        var ordem = await _repository.ObterPorIdAsync(ordemProducaoId, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(ordemProducaoId);

        var agora = DateTime.UtcNow;
        var primeiraTarefaIniciada = ordem.IniciarTarefa(tarefaId, idMecanico, nomeMecanico, agora);

        if (primeiraTarefaIniciada)
        {
            var evento = ProducaoIniciadaEvent.Create(ordem.OrdemServicoId, agora);
            await _eventPublisher.PublishAsync(evento, cancellationToken);
        }

        await _repository.AtualizarAsync(ordem, cancellationToken);

        return OrdemProducaoResponse.FromDomain(ordem);
    }
}

