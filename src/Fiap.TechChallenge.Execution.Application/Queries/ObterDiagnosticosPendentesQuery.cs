using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Domain.Interfaces;

namespace Fiap.TechChallenge.Execution.Application.Queries;

public class ObterDiagnosticosPendentesQuery
{
    private readonly IOrdemProducaoRepository _repository;

    public ObterDiagnosticosPendentesQuery(IOrdemProducaoRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<IReadOnlyList<OrdemProducaoFilaItemResponse>> ExecuteAsync(
        string? filialId = null,
        CancellationToken cancellationToken = default)
    {
        var ordens = await _repository.ObterDiagnosticosPendentesAsync(filialId, cancellationToken);
        return ordens.Select(OrdemProducaoFilaItemResponse.FromDomain).ToList();
    }
}

