namespace Fiap.TechChallenge.Contracts.Events;

#region Eventos Inbound (Consumidos pelo Execution Service)

/// <summary>
/// Publicado pelo OS Service quando uma nova Ordem de Serviço é aberta pelo balcão.
/// Cria uma OrdemProducao com status AGUARDANDO_DIAGNOSTICO.
/// </summary>
public sealed record OrdemServicoAbertaEvent(
    Guid OsId,
    ClienteContrato Cliente,
    VeiculoContrato Veiculo,
    string? FilialId,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static OrdemServicoAbertaEvent Create(
        Guid osId,
        ClienteContrato cliente,
        VeiculoContrato veiculo,
        string? filialId = null) =>
        new(osId, cliente, veiculo, filialId, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

public sealed record ClienteContrato(Guid IdCliente, string Nome, string? Telefone);

public sealed record VeiculoContrato(string Placa, string Modelo, int Ano);

/// <summary>
/// Publicado pelo OS Service quando itens do diagnóstico técnico são rejeitados (ex.: catálogo ou estoque).
/// Retorna o diagnóstico para o mecânico responsável no status EM_DIAGNOSTICO.
/// </summary>
public sealed record DiagnosticoRejeitadoEvent(
    Guid OsId,
    string Motivo,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static DiagnosticoRejeitadoEvent Create(Guid osId, string motivo) =>
        new(osId, motivo, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

/// <summary>
/// Publicado pelo OS Service após a aprovação e confirmação de pagamento do orçamento pelo cliente.
/// Libera a ordem para a esteira de reparos (NA_FILA) com a lista de tarefas aprovadas.
/// </summary>
public sealed record OrdemServicoLiberadaParaExecucaoEvent(
    Guid OsId,
    IReadOnlyList<TarefaItemContrato> Tarefas,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static OrdemServicoLiberadaParaExecucaoEvent Create(
        Guid osId,
        IReadOnlyList<TarefaItemContrato> tarefas) =>
        new(osId, tarefas, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

public sealed record TarefaItemContrato(Guid IdServicoOrigem, string Descricao, int TempoEstimadoMinutos);

/// <summary>
/// Publicado pelo OS Service quando a Ordem de Serviço é cancelada externamente (ex.: pagamento expirado/recusado - Compensação A).
/// Cancela a produção na oficina e interrompe tarefas pendentes ou em andamento.
/// </summary>
public sealed record OrdemServicoCanceladaEvent(
    Guid OsId,
    string Motivo,
    Guid EventId,
    Guid CorrelationId,
    DateTime OccurredAt,
    int Version = 1)
{
    public static OrdemServicoCanceladaEvent Create(Guid osId, string motivo) =>
        new(osId, motivo, Guid.NewGuid(), osId, DateTime.UtcNow, 1);
}

#endregion

#region Eventos Outbound (Publicados pelo Execution Service)

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

#endregion
