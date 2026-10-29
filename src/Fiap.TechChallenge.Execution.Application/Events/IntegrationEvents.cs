namespace Fiap.TechChallenge.Contracts.Events;

/// <summary>
/// Publicado quando o mecânico inicia a análise diagnóstica da Ordem de Serviço no chão de oficina.
/// </summary>
public sealed record DiagnosticoIniciadoEvent(
    Guid OsId,
    Guid IdMecanico,
    string NomeMecanico,
    DateTime DataHora,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static DiagnosticoIniciadoEvent Create(Guid osId, Guid idMecanico, string nomeMecanico, DateTime dataHora) =>
        new(osId, idMecanico, nomeMecanico, dataHora, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

/// <summary>
/// Publicado quando o mecânico conclui o laudo diagnóstico sugerindo serviços e peças/insumos.
/// </summary>
public sealed record DiagnosticoTecnicoConcluidoEvent(
    Guid OsId,
    string Laudo,
    IReadOnlyList<ServicoSugeridoItemEvent> Servicos,
    IReadOnlyList<PecaSugeridaItemEvent> Pecas,
    int? DuracaoDiagnosticoMinutos,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static DiagnosticoTecnicoConcluidoEvent Create(
        Guid osId,
        string laudo,
        IReadOnlyList<ServicoSugeridoItemEvent> servicos,
        IReadOnlyList<PecaSugeridaItemEvent> pecas,
        int? duracaoMinutos = null) =>
        new(osId, laudo, servicos, pecas, duracaoMinutos, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

public sealed record ServicoSugeridoItemEvent(Guid IdServico, string? Descricao = null);

public sealed record PecaSugeridaItemEvent(Guid IdPeca, int Quantidade, string? Descricao = null);

/// <summary>
/// Publicado de forma idempotente exclusivamente quando a 1ª tarefa de reparo entra em execução (NA_FILA -> EM_EXECUCAO).
/// </summary>
public sealed record ProducaoIniciadaEvent(
    Guid OsId,
    DateTime DataHoraInicio,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static ProducaoIniciadaEvent Create(Guid osId, DateTime dataHoraInicio) =>
        new(osId, dataHoraInicio, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

/// <summary>
/// Publicado quando todas as tarefas de reparo da Ordem de Produção são concluídas (EM_EXECUCAO -> CONCLUIDA).
/// </summary>
public sealed record ProducaoFinalizadaEvent(
    Guid OsId,
    DateTime DataHoraFim,
    int DuracaoTotalMinutos,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static ProducaoFinalizadaEvent Create(Guid osId, DateTime dataHoraFim, int duracaoTotalMinutos) =>
        new(osId, dataHoraFim, duracaoTotalMinutos, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

/// <summary>
/// Publicado quando a produção é cancelada internamente na oficina (Compensação B -> estorno no Billing + liberação de estoque no OS).
/// </summary>
public sealed record ProducaoCanceladaEvent(
    Guid OsId,
    string Motivo,
    DateTime DataHoraCancelamento,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static ProducaoCanceladaEvent Create(Guid osId, string motivo, DateTime dataHoraCancelamento) =>
        new(osId, motivo, dataHoraCancelamento, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

