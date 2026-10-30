using Fiap.TechChallenge.Execution.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.TechChallenge.Execution.Infrastructure.Messaging;

/// <summary>
/// Publicador de eventos de integração utilizando o MassTransit e Transactional Outbox do EF Core.
/// As mensagens publicadas são gravadas no mesmo commit do AppDbContext e despachadas para o RabbitMQ
/// de forma atômica e resiliente.
/// </summary>
public sealed class MassTransitEventPublisher(
    IPublishEndpoint publishEndpoint,
    ILogger<MassTransitEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(evento);

        logger.LogInformation(
            "Publicando evento de integração via MassTransit Outbox: {EventType} {@Event}",
            typeof(TEvent).Name,
            evento);

        await publishEndpoint.Publish(evento, cancellationToken);
    }
}
