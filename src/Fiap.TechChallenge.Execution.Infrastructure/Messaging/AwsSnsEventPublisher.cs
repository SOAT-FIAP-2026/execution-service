using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
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
                    ["tipoEvento"] = new() { DataType = "String", StringValue = tipoEvento },
                    ["origem"] = new() { DataType = "String", StringValue = "execution-service" }
                }
            };

            if (!string.IsNullOrWhiteSpace(osIdStr))
            {
                request.MessageAttributes["idOrdemServico"] = new() { DataType = "String", StringValue = osIdStr };
            }

            _logger.LogInformation(
                "Publicando evento {TipoEvento} no SNS ({TopicArn}) para OS {OsId}",
                tipoEvento, _topicArn, osIdStr);

            await _sns.PublishAsync(request, cancellationToken);
        }
    }
}

