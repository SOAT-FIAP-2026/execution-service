using System.Reflection;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Fiap.TechChallenge.Contracts.Common;
using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Contracts.ExecutionService;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Fiap.TechChallenge.Execution.UnitTests.Infrastructure;

public class MessagingCorrelationTests
{
    [Fact]
    public async Task AwsSnsEventPublisher_QuandoCorrelationContextAtivo_DeveIncluirCorrelationIdNosMessageAttributes()
    {
        var sns = Substitute.For<IAmazonSimpleNotificationService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EVENTOS_TOPIC_ARN"] = "arn:aws:sns:sa-east-1:000000000000:techchallenge-eventos-local"
            })
            .Build();

        PublishRequest? capturedRequest = null;
        sns.PublishAsync(Arg.Do<PublishRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-1" });

        var publisher = new AwsSnsEventPublisher(sns, config, NullLogger<AwsSnsEventPublisher>.Instance);
        var osId = Guid.NewGuid();

        using (CorrelationContext.Push("corr-e2e-execution-01"))
        {
            var evento = DiagnosticoIniciadoEvent.Create(osId, Guid.NewGuid(), "João Mecânico", DateTime.UtcNow);
            await publisher.PublishAsync(evento, CancellationToken.None);
        }

        capturedRequest.Should().NotBeNull();
        capturedRequest!.MessageAttributes.Should().ContainKey(SnsMessageAttributes.CorrelationId);
        capturedRequest.MessageAttributes[SnsMessageAttributes.CorrelationId].StringValue.Should().Be("corr-e2e-execution-01");
        capturedRequest.MessageAttributes[SnsMessageAttributes.IdOrdemServico].StringValue.Should().Be(osId.ToString());
    }

    [Fact]
    public async Task ExecutionSqsBackgroundWorker_QuandoRecebeEnvelopeSnsComCorrelationId_DeveAtivarCorrelationContextDuranteProcessamento()
    {
        var sqs = Substitute.For<IAmazonSQS>();
        var repository = Substitute.For<IOrdemProducaoRepository>();
        var osId = Guid.NewGuid();
        string? capturedCorrelationDuringRepoCall = null;

        repository.ExistePorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                capturedCorrelationDuringRepoCall = CorrelationContext.Current;
                return false;
            });

        var services = new ServiceCollection();
        services.AddScoped(_ => repository);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var config = new ConfigurationBuilder().Build();
        var worker = new ExecutionSqsBackgroundWorker(
            sqs,
            config,
            scopeFactory,
            NullLogger<ExecutionSqsBackgroundWorker>.Instance);

        var evt = OrdemServicoAbertaEvent.Create(
            osId,
            new ClienteContrato(Guid.NewGuid(), "João", "11999999999"),
            new VeiculoContrato("ABC1D23", "Civic", 2023),
            "SP-01");
        var innerPayload = JsonSerializer.Serialize(evt);

        var snsEnvelope = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            Message = innerPayload,
            MessageAttributes = new Dictionary<string, object>
            {
                [SnsMessageAttributes.TipoEvento] = new { Type = "String", Value = nameof(OrdemServicoAbertaEvent) },
                [SnsMessageAttributes.CorrelationId] = new { Type = "String", Value = "corr-sns-exec-99" }
            }
        });

        var sqsMessage = new Message
        {
            MessageId = "sqs-1",
            Body = snsEnvelope,
            MessageAttributes = new Dictionary<string, Amazon.SQS.Model.MessageAttributeValue>()
        };

        var method = typeof(ExecutionSqsBackgroundWorker)
            .GetMethod("ProcessarMensagemAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = (Task)method!.Invoke(worker, [sqsMessage, CancellationToken.None])!;
        await task;

        capturedCorrelationDuringRepoCall.Should().Be("corr-sns-exec-99");
        await repository.Received(1).AdicionarAsync(Arg.Any<OrdemProducao>(), Arg.Any<CancellationToken>());
    }
}
