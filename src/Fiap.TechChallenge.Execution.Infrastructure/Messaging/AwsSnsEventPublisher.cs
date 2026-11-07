using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Fiap.TechChallenge.Contracts;
using Fiap.TechChallenge.Contracts.Common;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.Execution.Infrastructure.Messaging
{
    public sealed class AwsSnsEventPublisher : IEventPublisher
    {
        private readonly IAmazonSimpleNotificationService _sns;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AwsSnsEventPublisher> _logger;
        private readonly string _topicArn;

        public AwsSnsEventPublisher(
            IAmazonSimpleNotificationService sns,
            IConfiguration configuration,
            ILogger<AwsSnsEventPublisher> logger)
        {
            _sns = sns ?? throw new ArgumentNullException(nameof(sns));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _topicArn = _configuration["EVENTOS_TOPIC_ARN"]
                ?? _configuration["Aws:EventosTopicArn"]
                ?? "arn:aws:sns:sa-east-1:000000000000:techchallenge-eventos-local";
        }

        public async Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(evento);

            var tipoEvento = typeof(TEvent).Name;
            var jsonPayload = JsonSerializer.Serialize(evento);

            string osIdStr = string.Empty;
            var propOsId = typeof(TEvent).GetProperty("OsId") ?? typeof(TEvent).GetProperty("IdOrdemServico");
            if (propOsId != null)
            {
                var val = propOsId.GetValue(evento);
                if (val != null)
                    osIdStr = val.ToString() ?? string.Empty;
            }

            var request = new PublishRequest
            {
                TopicArn = _topicArn,
                Message = jsonPayload,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { DataType = "String", StringValue = tipoEvento },
                    [SnsMessageAttributes.Origem] = new() { DataType = "String", StringValue = SnsMessageAttributes.Origens.ExecutionService }
                }
            };

            if (!string.IsNullOrWhiteSpace(osIdStr))
            {
                request.MessageAttributes[SnsMessageAttributes.IdOrdemServico] = new() { DataType = "String", StringValue = osIdStr };
            }

            var correlationId = (evento as IntegrationEvent)?.ObterCorrelationId()
                ?? CorrelationContext.Resolve(fallback: osIdStr);

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                request.MessageAttributes[SnsMessageAttributes.CorrelationId] = new()
                {
                    DataType = "String",
                    StringValue = correlationId
                };
            }

            _logger.LogInformation(
                "Publicando evento {TipoEvento} no SNS ({TopicArn}) para OS {OsId} [CorrelationId={CorrelationId}]",
                tipoEvento, _topicArn, osIdStr, correlationId);

            await _sns.PublishAsync(request, cancellationToken);
        }
    }
}

