using System.Collections.Concurrent;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fiap.TechChallenge.Execution.Infrastructure.Messaging;

/// <summary>
/// Publicador de eventos em memória/log estruturado utilizado na Etapa 4 e em testes,
/// substituído pelo MassTransitEventPublisher na Etapa 5 (Mensageria e Saga).
/// </summary>
public class InMemoryEventPublisher : IEventPublisher
{
    private readonly ILogger<InMemoryEventPublisher> _logger;
    private readonly ConcurrentQueue<object> _publishedEvents = new();

    public InMemoryEventPublisher(ILogger<InMemoryEventPublisher>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemoryEventPublisher>.Instance;
    }

    public IReadOnlyCollection<object> PublishedEvents => _publishedEvents.ToArray();

    public Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(evento);

        _publishedEvents.Enqueue(evento);
        _logger.LogInformation("Evento de integração enfileirado para publicação: {EventType} {@EventPayload}",
            typeof(TEvent).Name, evento);

        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_publishedEvents.TryDequeue(out _)) { }
    }
}

