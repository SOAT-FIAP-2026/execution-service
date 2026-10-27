using Fiap.TechChallenge.Execution.Domain.Common;
using Fiap.TechChallenge.Execution.Domain.Exceptions;

namespace Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;

/// <summary>
/// Tarefa de reparo físico atribuída a um mecânico, com apontamento de início e fim.
/// </summary>
public class TarefaProducao : EntidadeBase
{
    public Guid OrdemProducaoId { get; private set; }
    public Guid IdServicoOrigem { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public int TempoEstimadoMinutos { get; private set; }
    public Guid? IdMecanicoResponsavel { get; private set; }
    public string? NomeMecanicoResponsavel { get; private set; }
    public StatusTarefaProducao Status { get; private set; } = StatusTarefaProducao.Pendente;
    public DateTime? DataHoraInicio { get; private set; }
    public DateTime? DataHoraFim { get; private set; }
    public string? ObservacoesTecnicas { get; private set; }

    protected TarefaProducao() { }

    public TarefaProducao(Guid idServicoOrigem, string descricao, int tempoEstimadoMinutos)
    {
        if (idServicoOrigem == Guid.Empty)
            throw new DomainException("O identificador do serviço de origem é obrigatório.");

        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("A descrição da tarefa é obrigatória.");

        if (tempoEstimadoMinutos <= 0)
            throw new DomainException("O tempo estimado da tarefa deve ser maior que zero minutos.");

        IdServicoOrigem = idServicoOrigem;
        Descricao = descricao.Trim();
        TempoEstimadoMinutos = tempoEstimadoMinutos;
        Status = StatusTarefaProducao.Pendente;
    }

    public void AssociarOrdemProducao(Guid ordemProducaoId)
    {
        if (ordemProducaoId == Guid.Empty)
            throw new DomainException("Ordem de produção inválida.");

        OrdemProducaoId = ordemProducaoId;
        AtualizarTimestamp();
    }

    public void Iniciar(Guid idMecanico, string nomeMecanico, DateTime? dataHoraInicio = null)
    {
        if (idMecanico == Guid.Empty)
            throw new DomainException("O mecânico responsável é obrigatório para iniciar a tarefa.");

        if (string.IsNullOrWhiteSpace(nomeMecanico))
            throw new DomainException("O nome do mecânico é obrigatório.");

        if (Status != StatusTarefaProducao.Pendente)
            throw new TransicaoEstadoInvalidaException($"Não é possível iniciar uma tarefa no status '{Status}'. A tarefa deve estar 'Pendente'.");

        IdMecanicoResponsavel = idMecanico;
        NomeMecanicoResponsavel = nomeMecanico.Trim();
        DataHoraInicio = dataHoraInicio ?? DateTime.UtcNow;
        Status = StatusTarefaProducao.EmExecucao;
        AtualizarTimestamp();
    }

    public void Finalizar(Guid idMecanico, string? observacoes = null, DateTime? dataHoraFim = null)
    {
        if (Status != StatusTarefaProducao.EmExecucao)
            throw new TransicaoEstadoInvalidaException($"Não é possível finalizar uma tarefa no status '{Status}'. A tarefa deve estar 'EmExecucao'.");

        if (IdMecanicoResponsavel != idMecanico)
            throw new MecanicoNaoAutorizadoException("Somente o mecânico responsável que iniciou a tarefa pode finalizá-la.");

        DataHoraFim = dataHoraFim ?? DateTime.UtcNow;

        if (DataHoraInicio.HasValue && DataHoraFim.Value < DataHoraInicio.Value)
            throw new DomainException("A data/hora de finalização da tarefa não pode ser anterior ao início.");

        ObservacoesTecnicas = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
        Status = StatusTarefaProducao.Concluida;
        AtualizarTimestamp();
    }

    public void Interromper(string motivo, DateTime? dataHoraFim = null)
    {
        if (Status is StatusTarefaProducao.Concluida or StatusTarefaProducao.Interrompida)
            return;

        DataHoraFim ??= DataHoraInicio.HasValue ? (dataHoraFim ?? DateTime.UtcNow) : null;
        ObservacoesTecnicas = string.IsNullOrWhiteSpace(motivo) ? "Tarefa interrompida" : motivo.Trim();
        Status = StatusTarefaProducao.Interrompida;
        AtualizarTimestamp();
    }

    public int? ObterDuracaoRealMinutos()
    {
        if (!DataHoraInicio.HasValue || !DataHoraFim.HasValue)
            return null;

        var minutos = (DataHoraFim.Value - DataHoraInicio.Value).TotalMinutes;
        return (int)Math.Max(1, Math.Round(minutos));
    }
}
