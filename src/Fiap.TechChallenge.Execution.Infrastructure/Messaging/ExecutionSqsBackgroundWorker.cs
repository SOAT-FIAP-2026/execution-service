using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.Execution.Infrastructure.Messaging
{
    public sealed class ExecutionSqsBackgroundWorker : BackgroundService
    {
        private readonly IAmazonSQS _sqs;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExecutionSqsBackgroundWorker> _logger;
        private readonly string _queueUrl;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ExecutionSqsBackgroundWorker(
            IAmazonSQS sqs,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            ILogger<ExecutionSqsBackgroundWorker> logger)
        {
            _sqs = sqs ?? throw new ArgumentNullException(nameof(sqs));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _queueUrl = _configuration["EXECUCAO_QUEUE_URL"]
                ?? _configuration["Aws:ExecucaoQueueUrl"]
                ?? "http://floci:4566/000000000000/techchallenge-execucao-eventos-local";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando ExecutionSqsBackgroundWorker na fila {QueueUrl}...", _queueUrl);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var receiveRequest = new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 3,
                        MessageAttributeNames = new List<string> { "All" }
                    };

                    var response = await _sqs.ReceiveMessageAsync(receiveRequest, stoppingToken);

                    if (response.Messages == null || response.Messages.Count == 0)
                    {
                        continue;
                    }

                    foreach (var message in response.Messages)
                    {
                        await ProcessarMensagemAsync(message, stoppingToken);
                        await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro no polling da fila SQS {QueueUrl}. Nova tentativa em 3 segundos...", _queueUrl);
                    await Task.Delay(3000, stoppingToken);
                }
            }

            _logger.LogInformation("ExecutionSqsBackgroundWorker finalizado.");
        }

        private async Task ProcessarMensagemAsync(Message message, CancellationToken stoppingToken)
        {
            var (conteudoJson, tipoEvento) = ExtrairConteudoETipoEvento(message);

            _logger.LogInformation("Mensagem SQS recebida. Tipo: {TipoEvento}", tipoEvento);

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOrdemProducaoRepository>();

            switch (tipoEvento)
            {
                case nameof(OrdemServicoAbertaEvent):
                case "OrdemServicoCriada":
                    await ProcessarAberturaOsAsync(conteudoJson, repository, stoppingToken);
                    break;

                case nameof(OrdemServicoLiberadaParaExecucaoEvent):
                    await ProcessarLiberacaoExecucaoAsync(conteudoJson, repository, stoppingToken);
                    break;

                case nameof(DiagnosticoRejeitadoEvent):
                    await ProcessarDiagnosticoRejeitadoAsync(conteudoJson, repository, stoppingToken);
                    break;

                case nameof(OrdemServicoCanceladaEvent):
                case "OrdemServicoCancelada":
                    await ProcessarCancelamentoOsAsync(conteudoJson, repository, stoppingToken);
                    break;

                default:
                    _logger.LogDebug("Evento {TipoEvento} ignorado pelo execution-service.", tipoEvento);
                    break;
            }
        }

        private (string ConteudoJson, string TipoEvento) ExtrairConteudoETipoEvento(Message message)
        {
            string payload = message.Body;
            string tipoEvento = string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(message.Body);
                var root = doc.RootElement;

                // Envelope SNS Notification
                if (root.TryGetProperty("Type", out var typeProp) && typeProp.GetString() == "Notification")
                {
                    if (root.TryGetProperty("Message", out var msgProp))
                        payload = msgProp.GetString() ?? message.Body;

                    if (root.TryGetProperty("MessageAttributes", out var msgAttrs))
                    {
                        if (msgAttrs.TryGetProperty("tipoEvento", out var teProp) &&
                            teProp.TryGetProperty("Value", out var valProp))
                        {
                            tipoEvento = valProp.GetString() ?? string.Empty;
                        }
                    }
                }
                else
                {
                    // MassTransit envelope caso venha formato antigo
                    if (root.TryGetProperty("message", out var mtProp))
                    {
                        payload = mtProp.GetRawText();
                    }
                }
            }
            catch
            {
                // Usa payload raw em caso de parsing não JSON
            }

            if (string.IsNullOrEmpty(tipoEvento) && message.MessageAttributes.TryGetValue("tipoEvento", out var attrVal))
            {
                tipoEvento = attrVal.StringValue;
            }

            if (string.IsNullOrEmpty(tipoEvento))
            {
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    if (doc.RootElement.TryGetProperty("tipoEvento", out var tProp) ||
                        doc.RootElement.TryGetProperty("TipoEvento", out tProp))
                    {
                        tipoEvento = tProp.GetString() ?? string.Empty;
                    }
                }
                catch
                {
                }
            }

            return (payload, tipoEvento);
        }

        private async Task ProcessarAberturaOsAsync(
            string json,
            IOrdemProducaoRepository repository,
            CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<OrdemServicoAbertaEvent>(json, JsonOptions);
            if (evento == null || evento.OsId == Guid.Empty)
            {
                _logger.LogWarning("Falha ao desserializar OrdemServicoAbertaEvent: {Json}", json);
                return;
            }

            var existe = await repository.ExistePorOrdemServicoIdAsync(evento.OsId, cancellationToken);
            if (existe)
            {
                _logger.LogWarning("OrdemProducao para OS {OsId} já existe. Ignorando evento duplicado.", evento.OsId);
                return;
            }

            var cliente = new ClienteSnapshot(
                evento.Cliente?.IdCliente ?? Guid.NewGuid(),
                evento.Cliente?.Nome ?? "Cliente",
                evento.Cliente?.Telefone);

            var veiculo = new VeiculoSnapshot(
                evento.Veiculo?.Placa ?? "SEM-PLACA",
                evento.Veiculo?.Modelo ?? "Modelo Indefinido",
                evento.Veiculo?.Ano ?? 2022);

            var ordem = OrdemProducao.CriarParaDiagnostico(
                evento.OsId,
                cliente,
                veiculo,
                evento.FilialId ?? "MATRIZ",
                evento.OccurredAt == default ? DateTime.UtcNow : evento.OccurredAt);

            await repository.AdicionarAsync(ordem, cancellationToken);
            _logger.LogInformation("OrdemProducao {Id} criada com sucesso para OS {OsId}", ordem.Id, evento.OsId);
        }

        private async Task ProcessarLiberacaoExecucaoAsync(
            string json,
            IOrdemProducaoRepository repository,
            CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<OrdemServicoLiberadaParaExecucaoEvent>(json, JsonOptions);
            if (evento == null || evento.OsId == Guid.Empty)
            {
                _logger.LogWarning("Falha ao desserializar OrdemServicoLiberadaParaExecucaoEvent: {Json}", json);
                return;
            }

            var ordem = await repository.ObterPorOrdemServicoIdAsync(evento.OsId, cancellationToken);
            if (ordem == null)
            {
                _logger.LogWarning("OrdemProducao não encontrada para OS {OsId} ao liberar execução.", evento.OsId);
                return;
            }

            if (ordem.Status is StatusOrdemProducao.NaFila or StatusOrdemProducao.EmExecucao or StatusOrdemProducao.Concluida)
            {
                _logger.LogWarning("Ordem {Id} já liberada previamente (status {Status}).", ordem.Id, ordem.Status);
                return;
            }

            var tarefas = (evento.Tarefas ?? [])
                .Select(t => new TarefaProducao(
                    idServicoOrigem: t.IdServicoOrigem,
                    descricao: t.Descricao,
                    tempoEstimadoMinutos: t.TempoEstimadoMinutos > 0 ? t.TempoEstimadoMinutos : 60))
                .ToList();

            ordem.LiberarParaExecucao(tarefas, evento.OccurredAt == default ? DateTime.UtcNow : evento.OccurredAt);
            await repository.AtualizarAsync(ordem, cancellationToken);
            _logger.LogInformation("Ordem {Id} (OS {OsId}) liberada para fila de produção.", ordem.Id, evento.OsId);
        }

        private async Task ProcessarDiagnosticoRejeitadoAsync(
            string json,
            IOrdemProducaoRepository repository,
            CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<DiagnosticoRejeitadoEvent>(json, JsonOptions);
            if (evento == null || evento.OsId == Guid.Empty) return;

            var ordem = await repository.ObterPorOrdemServicoIdAsync(evento.OsId, cancellationToken);
            if (ordem == null) return;

            ordem.ReabrirDiagnostico(evento.Motivo, evento.OccurredAt == default ? DateTime.UtcNow : evento.OccurredAt);
            await repository.AtualizarAsync(ordem, cancellationToken);
            _logger.LogInformation("Diagnóstico reaberto para Ordem {Id} (OS {OsId}). Motivo: {Motivo}", ordem.Id, evento.OsId, evento.Motivo);
        }

        private async Task ProcessarCancelamentoOsAsync(
            string json,
            IOrdemProducaoRepository repository,
            CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<OrdemServicoCanceladaEvent>(json, JsonOptions);
            if (evento == null || evento.OsId == Guid.Empty) return;

            var ordem = await repository.ObterPorOrdemServicoIdAsync(evento.OsId, cancellationToken);
            if (ordem == null) return;

            ordem.Cancelar(evento.Motivo, evento.OccurredAt == default ? DateTime.UtcNow : evento.OccurredAt);
            await repository.AtualizarAsync(ordem, cancellationToken);
            _logger.LogInformation("Produção cancelada para Ordem {Id} (OS {OsId}).", ordem.Id, evento.OsId);
        }
    }
}
