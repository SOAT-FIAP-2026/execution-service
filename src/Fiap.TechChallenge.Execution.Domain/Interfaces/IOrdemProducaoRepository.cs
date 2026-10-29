using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;

namespace Fiap.TechChallenge.Execution.Domain.Interfaces;

/// <summary>
/// Contrato do repositório para persistência do agregado OrdemProducao.
/// </summary>
public interface IOrdemProducaoRepository
{
    /// <summary>Recupera a ordem com Diagnóstico, Tarefas e Histórico pelo ID da ordem de produção.</summary>
    Task<OrdemProducao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Recupera a ordem pelo ID da Ordem de Serviço de origem (chave única de correlação).</summary>
    Task<OrdemProducao?> ObterPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken cancellationToken = default);

    /// <summary>Verifica existência prévia pelo ID da Ordem de Serviço (idempotência no consumo de eventos).</summary>
    Task<bool> ExistePorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken cancellationToken = default);

    /// <summary>Lista as ordens na fila de produção (default status NA_FILA), ordenadas por data de liberação (FIFO).</summary>
    Task<IReadOnlyList<OrdemProducao>> ObterFilaProducaoAsync(
        string? filialId = null,
        StatusOrdemProducao? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lista as ordens aguardando inspeção diagnóstica (status AGUARDANDO_DIAGNOSTICO), ordenadas por data de abertura.</summary>
    Task<IReadOnlyList<OrdemProducao>> ObterDiagnosticosPendentesAsync(string? filialId = null, CancellationToken cancellationToken = default);

    /// <summary>Lista as tarefas concluídas em um intervalo de datas para cálculo de métricas de produtividade por mecânico.</summary>
    Task<IReadOnlyList<TarefaProducao>> ObterTarefasConcluidasPorPeriodoAsync(
        DateTime? de = null,
        DateTime? ate = null,
        Guid? idMecanico = null,
        CancellationToken cancellationToken = default);

    /// <summary>Persiste uma nova ordem de produção.</summary>
    Task AdicionarAsync(OrdemProducao ordem, CancellationToken cancellationToken = default);

    /// <summary>Atualiza o agregado com controle de concorrência otimista.</summary>
    Task AtualizarAsync(OrdemProducao ordem, CancellationToken cancellationToken = default);
}
