namespace Fiap.TechChallenge.Execution.Application.Interfaces;

/// <summary>
/// Abstração para publicação de eventos de integração da Saga Coreografada.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
        where TEvent : class;
}

