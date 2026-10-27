namespace Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;

/// <summary>
/// Estados de uma tarefa individual de reparo alocada a um mecânico.
/// </summary>
public enum StatusTarefaProducao
{
    /// <summary>Tarefa aguardando início por um mecânico.</summary>
    Pendente = 1,

    /// <summary>Mecânico apontou início da execução do reparo.</summary>
    EmExecucao = 2,

    /// <summary>Mecânico concluiu a execução da tarefa.</summary>
    Concluida = 3,

    /// <summary>Tarefa cancelada/interrompida devido ao cancelamento da ordem.</summary>
    Interrompida = 4
}

public static class StatusTarefaProducaoExtensions
{
    public static string ToUpperSnake(this StatusTarefaProducao status) => status switch
    {
        StatusTarefaProducao.Pendente => "PENDENTE",
        StatusTarefaProducao.EmExecucao => "EM_EXECUCAO",
        StatusTarefaProducao.Concluida => "CONCLUIDA",
        StatusTarefaProducao.Interrompida => "INTERROMPIDA",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status de tarefa desconhecido")
    };

    public static StatusTarefaProducao FromUpperSnake(string valor) => valor?.ToUpperInvariant() switch
    {
        "PENDENTE" => StatusTarefaProducao.Pendente,
        "EM_EXECUCAO" => StatusTarefaProducao.EmExecucao,
        "CONCLUIDA" => StatusTarefaProducao.Concluida,
        "INTERROMPIDA" => StatusTarefaProducao.Interrompida,
        _ => throw new ArgumentException($"Valor '{valor}' não é um StatusTarefaProducao válido.", nameof(valor))
    };
}
