using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;

namespace Fiap.TechChallenge.Execution.Application.Queries;

public class ObterDetalhesOrdemProducaoQuery
{
    private readonly IOrdemProducaoRepository _repository;

    public ObterDetalhesOrdemProducaoQuery(IOrdemProducaoRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<OrdemProducaoResponse> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ordem = await _repository.ObterPorIdAsync(id, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(id);

        return OrdemProducaoResponse.FromDomain(ordem);
    }

    public async Task<OrdemProducaoResponse> ObterPorOrdemServicoIdAsync(
        Guid ordemServicoId,
        CancellationToken cancellationToken = default)
    {
        var ordem = await _repository.ObterPorOrdemServicoIdAsync(ordemServicoId, cancellationToken)
            ?? throw new OrdemProducaoNaoEncontradaException(ordemServicoId, buscaPorOrdemServico: true);

        return OrdemProducaoResponse.FromDomain(ordem);
    }
}
