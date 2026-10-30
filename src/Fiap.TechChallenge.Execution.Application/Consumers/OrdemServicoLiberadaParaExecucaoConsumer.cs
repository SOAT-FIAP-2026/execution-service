using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.Execution.Application.Consumers;

/// <summary>
/// Consome o evento OrdemServicoLiberadaParaExecucaoEvent emitido pelo OS Service após a confirmação
/// do pagamento do orçamento pelo cliente. Insere as tarefas aprovadas e posiciona a ordem na fila FIFO.
/// </summary>
public sealed class OrdemServicoLiberadaParaExecucaoConsumer(
    IOrdemProducaoRepository repository,
    ILogger<OrdemServicoLiberadaParaExecucaoConsumer> logger) : IConsumer<OrdemServicoLiberadaParaExecucaoEvent>
{
    public async Task Consume(ConsumeContext<OrdemServicoLiberadaParaExecucaoEvent> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        logger.LogInformation(
            "Consumindo OrdemServicoLiberadaParaExecucaoEvent para OS {OsId} com {TotalTarefas} tarefas.",
            message.OsId,
            message.Tarefas?.Count ?? 0);

        var ordem = await repository.ObterPorOrdemServicoIdAsync(message.OsId, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(message.OsId, buscaPorOrdemServico: true);

        if (ordem.Status is StatusOrdemProducao.NaFila or StatusOrdemProducao.EmExecucao or StatusOrdemProducao.Concluida)
        {
            logger.LogWarning(
                "Ordem {OrdemProducaoId} (OS {OsId}) já foi liberada previamente (status: '{Status}'). Mensagem descartada de forma idempotente.",
                ordem.Id,
                message.OsId,
                ordem.Status);
            return;
        }

        var tarefas = (message.Tarefas ?? [])
            .Select(t => new TarefaProducao(
                idServicoOrigem: t.IdServicoOrigem,
                descricao: t.Descricao,
                tempoEstimadoMinutos: t.TempoEstimadoMinutos))
            .ToList();

        ordem.LiberarParaExecucao(tarefas, message.OccurredAt);
        await repository.AtualizarAsync(ordem, cancellationToken);

        logger.LogInformation(
            "Ordem {OrdemProducaoId} (OS {OsId}) liberada para produção com sucesso. Status atual: {Status}.",
            ordem.Id,
            message.OsId,
            ordem.Status);
    }
}
