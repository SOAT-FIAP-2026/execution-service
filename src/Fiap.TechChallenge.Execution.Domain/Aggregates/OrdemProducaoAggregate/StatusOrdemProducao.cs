namespace Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;

/// <summary>
/// Estados operacionais do ciclo de vida da Ordem de Produção no chão de oficina.
/// </summary>
public enum StatusOrdemProducao
{
    /// <summary>OS aberta pelo balcão; aguarda mecânico assumir para inspeção técnica.</summary>
    AguardandoDiagnostico = 1,

    /// <summary>Mecânico realizando a análise diagnóstica e testes no veículo.</summary>
    EmDiagnostico = 2,

    /// <summary>Laudo técnico emitido com serviços e peças sugeridos; aguardando precificação/aprovação.</summary>
    DiagnosticoConcluido = 3,

    /// <summary>Orçamento aprovado e pago; OS na fila operacional FIFO de produção.</summary>
    NaFila = 4,

    /// <summary>Pelo menos uma tarefa de reparo está em execução na oficina.</summary>
    EmExecucao = 5,

    /// <summary>Todas as tarefas de reparo foram concluídas com sucesso.</summary>
    Concluida = 6,

    /// <summary>Produção cancelada por falha de pagamento (externa) ou decisão técnica/cliente (interna).</summary>
    Cancelada = 7
}

public static class StatusOrdemProducaoExtensions
{
    public static string ToUpperSnake(this StatusOrdemProducao status) => status switch
    {
        StatusOrdemProducao.AguardandoDiagnostico => "AGUARDANDO_DIAGNOSTICO",
        StatusOrdemProducao.EmDiagnostico => "EM_DIAGNOSTICO",
        StatusOrdemProducao.DiagnosticoConcluido => "DIAGNOSTICO_CONCLUIDO",
        StatusOrdemProducao.NaFila => "NA_FILA",
        StatusOrdemProducao.EmExecucao => "EM_EXECUCAO",
        StatusOrdemProducao.Concluida => "CONCLUIDA",
        StatusOrdemProducao.Cancelada => "CANCELADA",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status desconhecido")
    };

    public static StatusOrdemProducao FromUpperSnake(string valor) => valor?.ToUpperInvariant() switch
    {
        "AGUARDANDO_DIAGNOSTICO" => StatusOrdemProducao.AguardandoDiagnostico,
        "EM_DIAGNOSTICO" => StatusOrdemProducao.EmDiagnostico,
        "DIAGNOSTICO_CONCLUIDO" => StatusOrdemProducao.DiagnosticoConcluido,
        "NA_FILA" => StatusOrdemProducao.NaFila,
        "EM_EXECUCAO" => StatusOrdemProducao.EmExecucao,
        "CONCLUIDA" => StatusOrdemProducao.Concluida,
        "CANCELADA" => StatusOrdemProducao.Cancelada,
        _ => throw new ArgumentException($"Valor '{valor}' não é um StatusOrdemProducao válido.", nameof(valor))
    };
}
