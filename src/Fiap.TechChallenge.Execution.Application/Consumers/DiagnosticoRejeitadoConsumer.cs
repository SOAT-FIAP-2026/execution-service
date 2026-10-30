using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.Execution.Application.Consumers;

/// <summary>
/// Consome o evento DiagnosticoRejeitadoEvent emitido pelo OS Service quando os itens do laudo técnico
/// apresentam inconsistências de catálogo ou estoque. Reabre o diagnóstico para o mecânico responsável.
/// </summary>
public sealed class DiagnosticoRejeitadoConsumer(
    IOrdemProducaoRepository repository,
    ILogger<DiagnosticoRejeitadoConsumer> logger) : IConsumer<DiagnosticoRejeitadoEvent>
{
    public async Task Consume(ConsumeContext<DiagnosticoRejeitadoEvent> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        logger.LogInformation(
            "Consumindo DiagnosticoRejeitadoEvent para OS {OsId}. Motivo: {Motivo}.",
            message.OsId,
            message.Motivo);

        var ordem = await repository.ObterPorOrdemServicoIdAsync(message.OsId, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(message.OsId, buscaPorOrdemServico: true);

        if (ordem.Status == StatusOrdemProducao.EmDiagnostico)
        {
            logger.LogWarning(
                "Ordem {OrdemProducaoId} (OS {OsId}) já se encontra em diagnóstico. Mensagem descartada de forma idempotente.",
                ordem.Id,
                message.OsId);
            return;
        }

        if (ordem.Status != StatusOrdemProducao.DiagnosticoConcluido)
        {
            logger.LogWarning(
                "Ordem {OrdemProducaoId} (OS {OsId}) com status '{Status}' incompatível para reabertura de diagnóstico.",
                ordem.Id,
                message.OsId,
                ordem.Status);
            return;
        }

        ordem.ReabrirDiagnostico(message.Motivo, message.OccurredAt);
        await repository.AtualizarAsync(ordem, cancellationToken);

        logger.LogInformation(
            "Diagnóstico da Ordem {OrdemProducaoId} reaberto com sucesso. Status atual: {Status}.",
            ordem.Id,
            ordem.Status);
    }
}
