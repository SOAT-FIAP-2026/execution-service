using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.Execution.Application.Consumers;

/// <summary>
/// Consome o evento OrdemServicoAbertaEvent emitido pelo OS Service quando uma nova OS é criada.
/// Inicializa a Ordem de Produção no status AGUARDANDO_DIAGNOSTICO de forma idempotente.
/// </summary>
public sealed class OrdemServicoAbertaConsumer(
    IOrdemProducaoRepository repository,
    ILogger<OrdemServicoAbertaConsumer> logger) : IConsumer<OrdemServicoAbertaEvent>
{
    public async Task Consume(ConsumeContext<OrdemServicoAbertaEvent> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        logger.LogInformation(
            "Consumindo OrdemServicoAbertaEvent para OS {OsId} (CorrelationId: {CorrelationId}).",
            message.OsId,
            message.CorrelationId);

        var existe = await repository.ExistePorOrdemServicoIdAsync(message.OsId, cancellationToken);
        if (existe)
        {
            logger.LogWarning(
                "Ordem de produção para OS {OsId} já existe. Mensagem descartada de forma idempotente.",
                message.OsId);
            return;
        }

        var cliente = new ClienteSnapshot(
            message.Cliente.IdCliente,
            message.Cliente.Nome,
            message.Cliente.Telefone);

        var veiculo = new VeiculoSnapshot(
            message.Veiculo.Placa,
            message.Veiculo.Modelo,
            message.Veiculo.Ano);

        var ordem = OrdemProducao.CriarParaDiagnostico(
            message.OsId,
            cliente,
            veiculo,
            message.FilialId,
            message.OccurredAt);

        await repository.AdicionarAsync(ordem, cancellationToken);

        logger.LogInformation(
            "Ordem de Produção {OrdemProducaoId} criada com sucesso para OS {OsId}.",
            ordem.Id,
            message.OsId);
    }
}
