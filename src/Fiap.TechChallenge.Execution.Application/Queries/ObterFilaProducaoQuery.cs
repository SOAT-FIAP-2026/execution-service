using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;

namespace Fiap.TechChallenge.Execution.Application.Queries;

public class ObterFilaProducaoQuery
{
    private readonly IOrdemProducaoRepository _repository;

    public ObterFilaProducaoQuery(IOrdemProducaoRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<IReadOnlyList<OrdemProducaoFilaItemResponse>> ExecuteAsync(
        string? status = null,
        string? filialId = null,
        CancellationToken cancellationToken = default)
    {
        StatusOrdemProducao? statusEnum = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var trimmed = status.Trim();
            if (Enum.TryParse<StatusOrdemProducao>(trimmed, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed))
            {
                statusEnum = parsed;
            }
            else
            {
                try
                {
                    statusEnum = StatusOrdemProducaoExtensions.FromUpperSnake(trimmed);
                }
                catch (ArgumentException ex)
                {
                    throw new DomainException($"O status de filtro '{status}' é inválido.", ex);
                }
            }
        }

        var ordens = await _repository.ObterFilaProducaoAsync(filialId, statusEnum, cancellationToken);
        return ordens.Select(OrdemProducaoFilaItemResponse.FromDomain).ToList();
    }
}
