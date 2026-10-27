using Fiap.TechChallenge.Execution.Domain.Common;
using Fiap.TechChallenge.Execution.Domain.Exceptions;

namespace Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;

/// <summary>
/// Registro imutável de auditoria de cada transição de estado da ordem de produção.
/// </summary>
public class HistoricoProducao : EntidadeBase
{
    public Guid OrdemProducaoId { get; private set; }
    public StatusOrdemProducao Status { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public DateTime DataHora { get; private set; }

    protected HistoricoProducao() { }

    public HistoricoProducao(Guid ordemProducaoId, StatusOrdemProducao status, string descricao, DateTime? dataHora = null)
    {
        if (ordemProducaoId == Guid.Empty)
            throw new DomainException("Ordem de produção inválida para registro no histórico.");

        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("A descrição do evento de histórico é obrigatória.");

        OrdemProducaoId = ordemProducaoId;
        Status = status;
        Descricao = descricao.Trim();
        DataHora = dataHora ?? DateTime.UtcNow;
    }
}
