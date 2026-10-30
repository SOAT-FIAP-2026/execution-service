using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.Execution.Application.Consumers;

/// <summary>
/// Consome o evento OrdemServicoCanceladaEvent emitido pelo OS Service (Compensação A).
/// Ocorre quando a OS é cancelada antes ou durante a execução (ex.: pagamento expirado/recusado no Billing).
/// Cancela a produção na oficina e interrompe tarefas pendentes ou em andamento.
/// </summary>
public sealed class OrdemServicoCanceladaConsumer(
    IOrdemProducaoRepository repository,
    ILogger<OrdemServicoCanceladaConsumer> logger) : IConsumer<OrdemServicoCanceladaEvent>
{
    public async Task Consume(ConsumeContext<OrdemServicoCanceladaEvent> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        logger.LogInformation(
            "Consumindo OrdemServicoCanceladaEvent para OS {OsId}. Motivo: {Motivo}.",
            message.OsId,
            message.Motivo);

        var ordem = await repository.ObterPorOrdemServicoIdAsync(message.OsId, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(message.OsId, buscaPorOrdemServico: true);

        if (ordem.Status == StatusOrdemProducao.Cancelada)
        {
            logger.LogWarning(
                "Ordem {OrdemProducaoId} (OS {OsId}) já se encontra cancelada. Mensagem descartada de forma idempotente.",
                ordem.Id,
                message.OsId);
            return;
        }

        ordem.Cancelar(message.Motivo, message.OccurredAt);
        await repository.AtualizarAsync(ordem, cancellationToken);

        logger.LogInformation(
            "Ordem {OrdemProducaoId} (OS {OsId}) cancelada via compensação externa com sucesso. Status atual: {Status}.",
            ordem.Id,
            message.OsId,
            ordem.Status);
    }
}
