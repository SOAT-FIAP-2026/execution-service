using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;

namespace Fiap.TechChallenge.Execution.Application.DTOs;

public sealed record OrdemProducaoResponse(
    Guid Id,
    Guid OrdemServicoId,
    string? FilialId,
    string Status,
    int Versao,
    ClienteSnapshotResponse Cliente,
    VeiculoSnapshotResponse Veiculo,
    DiagnosticoResponse? Diagnostico,
    DateTime DataAbertura,
    DateTime? DataLiberacao,
    DateTime? DataConclusao,
    int DuracaoTotalTarefasMinutos,
    IReadOnlyList<TarefaProducaoResponse> Tarefas,
    IReadOnlyList<HistoricoProducaoResponse> Historico)
{
    public static OrdemProducaoResponse FromDomain(OrdemProducao ordem)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        return new OrdemProducaoResponse(
            Id: ordem.Id,
            OrdemServicoId: ordem.OrdemServicoId,
            FilialId: ordem.FilialId,
            Status: ordem.Status.ToUpperSnake(),
            Versao: ordem.Versao,
            Cliente: new ClienteSnapshotResponse(ordem.Cliente.IdCliente, ordem.Cliente.Nome, ordem.Cliente.Telefone),
            Veiculo: new VeiculoSnapshotResponse(ordem.Veiculo.Placa, ordem.Veiculo.Modelo, ordem.Veiculo.Ano),
            Diagnostico: ordem.Diagnostico is null ? null : DiagnosticoResponse.FromDomain(ordem.Diagnostico),
            DataAbertura: ordem.DataAbertura,
            DataLiberacao: ordem.DataLiberacao,
            DataConclusao: ordem.DataConclusao,
            DuracaoTotalTarefasMinutos: ordem.ObterDuracaoTotalTarefasMinutos(),
            Tarefas: ordem.Tarefas.Select(TarefaProducaoResponse.FromDomain).ToList(),
            Historico: ordem.Historico.OrderBy(h => h.DataHora).Select(HistoricoProducaoResponse.FromDomain).ToList());
    }
}

public sealed record ClienteSnapshotResponse(
    Guid IdCliente,
    string Nome,
    string? Telefone);

public sealed record VeiculoSnapshotResponse(
    string Placa,
    string Modelo,
    int Ano);

public sealed record DiagnosticoResponse(
    Guid Id,
    Guid IdMecanico,
    string NomeMecanico,
    DateTime DataHoraInicio,
    DateTime? DataHoraFim,
    int? DuracaoMinutos,
    string? LaudoTecnico,
    IReadOnlyList<ServicoSugeridoResponse> ServicosSugeridos,
    IReadOnlyList<PecaSugeridaResponse> PecasSugeridas)
{
    public static DiagnosticoResponse FromDomain(Diagnostico diag) =>
        new(
            Id: diag.Id,
            IdMecanico: diag.IdMecanico,
            NomeMecanico: diag.NomeMecanico,
            DataHoraInicio: diag.DataHoraInicio,
            DataHoraFim: diag.DataHoraFim,
            DuracaoMinutos: diag.ObterDuracaoMinutos(),
            LaudoTecnico: diag.LaudoTecnico,
            ServicosSugeridos: diag.ServicosSugeridos
                .Select(s => new ServicoSugeridoResponse(s.IdServicoCatalogo, s.Descricao))
                .ToList(),
            PecasSugeridas: diag.PecasSugeridas
                .Select(p => new PecaSugeridaResponse(p.IdPecaCatalogo, p.Quantidade, p.Descricao))
                .ToList());
}

public sealed record ServicoSugeridoResponse(
    Guid IdServico,
    string? Descricao);

public sealed record PecaSugeridaResponse(
    Guid IdPeca,
    int Quantidade,
    string? Descricao);

public sealed record TarefaProducaoResponse(
    Guid Id,
    Guid IdServicoOrigem,
    string Descricao,
    int TempoEstimadoMinutos,
    int? DuracaoRealMinutos,
    Guid? IdMecanicoResponsavel,
    string? NomeMecanicoResponsavel,
    string Status,
    DateTime? DataHoraInicio,
    DateTime? DataHoraFim,
    string? ObservacoesTecnicas)
{
    public static TarefaProducaoResponse FromDomain(TarefaProducao tarefa) =>
        new(
            Id: tarefa.Id,
            IdServicoOrigem: tarefa.IdServicoOrigem,
            Descricao: tarefa.Descricao,
            TempoEstimadoMinutos: tarefa.TempoEstimadoMinutos,
            DuracaoRealMinutos: tarefa.ObterDuracaoRealMinutos(),
            IdMecanicoResponsavel: tarefa.IdMecanicoResponsavel,
            NomeMecanicoResponsavel: tarefa.NomeMecanicoResponsavel,
            Status: tarefa.Status.ToUpperSnake(),
            DataHoraInicio: tarefa.DataHoraInicio,
            DataHoraFim: tarefa.DataHoraFim,
            ObservacoesTecnicas: tarefa.ObservacoesTecnicas);
}

public sealed record HistoricoProducaoResponse(
    Guid Id,
    string Status,
    string Descricao,
    DateTime DataHora)
{
    public static HistoricoProducaoResponse FromDomain(HistoricoProducao hist) =>
        new(hist.Id, hist.Status.ToUpperSnake(), hist.Descricao, hist.DataHora);
}

public sealed record OrdemProducaoFilaItemResponse(
    Guid Id,
    Guid OrdemServicoId,
    string? FilialId,
    string Status,
    ClienteSnapshotResponse Cliente,
    VeiculoSnapshotResponse Veiculo,
    DateTime DataAbertura,
    DateTime? DataLiberacao,
    int TotalTarefas,
    int TarefasConcluidas,
    int TempoTotalEstimadoMinutos)
{
    public static OrdemProducaoFilaItemResponse FromDomain(OrdemProducao ordem)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        return new OrdemProducaoFilaItemResponse(
            Id: ordem.Id,
            OrdemServicoId: ordem.OrdemServicoId,
            FilialId: ordem.FilialId,
            Status: ordem.Status.ToUpperSnake(),
            Cliente: new ClienteSnapshotResponse(ordem.Cliente.IdCliente, ordem.Cliente.Nome, ordem.Cliente.Telefone),
            Veiculo: new VeiculoSnapshotResponse(ordem.Veiculo.Placa, ordem.Veiculo.Modelo, ordem.Veiculo.Ano),
            DataAbertura: ordem.DataAbertura,
            DataLiberacao: ordem.DataLiberacao,
            TotalTarefas: ordem.Tarefas.Count,
            TarefasConcluidas: ordem.Tarefas.Count(t => t.Status == StatusTarefaProducao.Concluida),
            TempoTotalEstimadoMinutos: ordem.Tarefas.Sum(t => t.TempoEstimadoMinutos));
    }
}

public sealed record RelatorioProdutividadeResponse(
    DateTime? PeriodoInicio,
    DateTime? PeriodoFim,
    int TotalTarefasConcluidas,
    IReadOnlyList<MetricaProdutividadeMecanicoResponse> Mecanicos);

public sealed record MetricaProdutividadeMecanicoResponse(
    Guid IdMecanico,
    string NomeMecanico,
    int TarefasConcluidas,
    int TempoTotalEstimadoMinutos,
    int TempoTotalRealizadoMinutos,
    double MediaEstimadaMinutos,
    double MediaRealizadaMinutos,
    double IndiceEficienciaPercentual);

