using Fiap.TechChallenge.Execution.Domain.Common;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;

namespace Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;

/// <summary>
/// Aggregate Root que gerencia o ciclo de vida completo da Ordem de Produção no chão de oficina.
/// </summary>
public class OrdemProducao : EntidadeBase
{
    public Guid OrdemServicoId { get; private set; }
    public string? FilialId { get; private set; }
    public StatusOrdemProducao Status { get; private set; }
    public int Versao { get; private set; } = 1;

    public ClienteSnapshot Cliente { get; private set; } = null!;
    public VeiculoSnapshot Veiculo { get; private set; } = null!;
    public Diagnostico? Diagnostico { get; private set; }

    public DateTime DataAbertura { get; private set; }
    public DateTime? DataLiberacao { get; private set; }
    public DateTime? DataConclusao { get; private set; }

    private readonly List<TarefaProducao> _tarefas = [];
    public IReadOnlyCollection<TarefaProducao> Tarefas => _tarefas.AsReadOnly();

    private readonly List<HistoricoProducao> _historico = [];
    public IReadOnlyCollection<HistoricoProducao> Historico => _historico.AsReadOnly();

    protected OrdemProducao() { }

    /// <summary>
    /// Factory method para inicialização da ordem a partir do evento de abertura da OS.
    /// </summary>
    public static OrdemProducao CriarParaDiagnostico(
        Guid ordemServicoId,
        ClienteSnapshot cliente,
        VeiculoSnapshot veiculo,
        string? filialId = null,
        DateTime? dataAbertura = null)
    {
        if (ordemServicoId == Guid.Empty)
            throw new DomainException("O identificador da Ordem de Serviço de origem é obrigatório.");

        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(veiculo);

        var agora = dataAbertura ?? DateTime.UtcNow;

        var ordem = new OrdemProducao
        {
            Id = Guid.NewGuid(),
            OrdemServicoId = ordemServicoId,
            Cliente = cliente,
            Veiculo = veiculo,
            FilialId = string.IsNullOrWhiteSpace(filialId) ? null : filialId.Trim(),
            Status = StatusOrdemProducao.AguardandoDiagnostico,
            DataAbertura = agora,
            CriadoEm = agora,
            AtualizadoEm = agora,
            Versao = 1
        };

        ordem.RegistrarHistorico(StatusOrdemProducao.AguardandoDiagnostico, "OS aberta aguardando diagnóstico técnico.", agora);
        return ordem;
    }

    /// <summary>
    /// Inicia o diagnóstico técnico no veículo por um mecânico.
    /// </summary>
    public void IniciarDiagnostico(Guid idMecanico, string nomeMecanico, DateTime? dataHora = null)
    {
        if (Status != StatusOrdemProducao.AguardandoDiagnostico && Status != StatusOrdemProducao.EmDiagnostico)
            throw new TransicaoEstadoInvalidaException($"Não é possível iniciar diagnóstico para ordem no status '{Status}'. A ordem deve estar em 'AguardandoDiagnostico'.");

        var agora = dataHora ?? DateTime.UtcNow;

        Diagnostico = new Diagnostico(Id, idMecanico, nomeMecanico, agora);
        Status = StatusOrdemProducao.EmDiagnostico;
        AtualizarTimestamp();

        RegistrarHistorico(Status, $"Diagnóstico iniciado por {nomeMecanico}.", agora);
    }

    /// <summary>
    /// Conclui o diagnóstico técnico registrando laudo e itens sugeridos.
    /// </summary>
    public void FinalizarDiagnostico(
        Guid idMecanico,
        string laudoTecnico,
        IEnumerable<ServicoSugerido>? servicos = null,
        IEnumerable<PecaSugerida>? pecas = null,
        DateTime? dataHora = null)
    {
        if (Status != StatusOrdemProducao.EmDiagnostico || Diagnostico == null)
            throw new TransicaoEstadoInvalidaException($"O diagnóstico precisa estar em andamento para ser finalizado. Status atual: '{Status}'.");

        if (Diagnostico.IdMecanico != idMecanico)
            throw new MecanicoNaoAutorizadoException("Apenas o mecânico responsável que iniciou a análise diagnóstica pode concluí-la.");

        var agora = dataHora ?? DateTime.UtcNow;

        Diagnostico.Finalizar(laudoTecnico, servicos, pecas, agora);
        Status = StatusOrdemProducao.DiagnosticoConcluido;
        AtualizarTimestamp();

        RegistrarHistorico(Status, "Diagnóstico técnico finalizado com laudo e sugestões.", agora);
    }

    /// <summary>
    /// Retorna o diagnóstico para revisão do mecânico caso o OS Service identifique inconsistências de catálogo ou estoque.
    /// </summary>
    public void ReabrirDiagnostico(string motivo, DateTime? dataHora = null)
    {
        if (Status != StatusOrdemProducao.DiagnosticoConcluido)
            throw new TransicaoEstadoInvalidaException($"Apenas ordens com diagnóstico concluído podem ser reabertas. Status atual: '{Status}'.");

        var agora = dataHora ?? DateTime.UtcNow;
        Status = StatusOrdemProducao.EmDiagnostico;
        AtualizarTimestamp();

        RegistrarHistorico(Status, $"Diagnóstico rejeitado pelo OS Service: {motivo}", agora);
    }

    /// <summary>
    /// Libera a ordem para a esteira de produção (fila operacional FIFO) após confirmação do pagamento do orçamento.
    /// </summary>
    public void LiberarParaExecucao(IEnumerable<TarefaProducao> tarefas, DateTime? dataLiberacao = null)
    {
        if (Status != StatusOrdemProducao.DiagnosticoConcluido)
            throw new TransicaoEstadoInvalidaException($"A ordem deve possuir diagnóstico prévio concluído para ser liberada para produção. Status atual: '{Status}'.");

        var listaTarefas = tarefas?.ToList() ?? [];
        if (listaTarefas.Count == 0)
            throw new DomainException("A ordem liberada para execução deve possuir pelo menos uma tarefa de reparo.");

        var agora = dataLiberacao ?? DateTime.UtcNow;

        foreach (var tarefa in listaTarefas)
        {
            tarefa.AssociarOrdemProducao(Id);
            _tarefas.Add(tarefa);
        }

        Status = StatusOrdemProducao.NaFila;
        DataLiberacao = agora;
        AtualizarTimestamp();

        RegistrarHistorico(Status, "Orçamento aprovado. Ordem liberada para a fila de produção.", agora);
    }

    /// <summary>
    /// Aponta o início de uma tarefa individual por um mecânico.
    /// Retorna verdadeiro se esta foi a primeira tarefa iniciada (sinal para disparar ProducaoIniciadaEvent de forma idempotente).
    /// </summary>
    public bool IniciarTarefa(Guid tarefaId, Guid idMecanico, string nomeMecanico, DateTime? dataHora = null)
    {
        if (Status != StatusOrdemProducao.NaFila && Status != StatusOrdemProducao.EmExecucao)
            throw new TransicaoEstadoInvalidaException($"Não é possível iniciar tarefas em ordens com status '{Status}'. A ordem deve estar 'NaFila' ou 'EmExecucao'.");

        var tarefa = _tarefas.FirstOrDefault(t => t.Id == tarefaId)
            ?? throw new TarefaProducaoNaoEncontradaException(tarefaId);

        var agora = dataHora ?? DateTime.UtcNow;
        tarefa.Iniciar(idMecanico, nomeMecanico, agora);

        var primeiraTarefaIniciada = false;
        if (Status == StatusOrdemProducao.NaFila)
        {
            Status = StatusOrdemProducao.EmExecucao;
            primeiraTarefaIniciada = true;
            RegistrarHistorico(Status, "Início da execução dos reparos na oficina.", agora);
        }

        AtualizarTimestamp();
        return primeiraTarefaIniciada;
    }

    /// <summary>
    /// Aponta a conclusão de uma tarefa de reparo pelo mecânico.
    /// Retorna verdadeiro se todas as tarefas da ordem foram concluídas (sinal para emitir ProducaoFinalizadaEvent).
    /// </summary>
    public bool FinalizarTarefa(Guid tarefaId, Guid idMecanico, string? observacoes = null, DateTime? dataHora = null)
    {
        if (Status != StatusOrdemProducao.EmExecucao)
            throw new TransicaoEstadoInvalidaException($"Não é possível finalizar tarefas em ordens com status '{Status}'. A ordem deve estar 'EmExecucao'.");

        var tarefa = _tarefas.FirstOrDefault(t => t.Id == tarefaId)
            ?? throw new TarefaProducaoNaoEncontradaException(tarefaId);

        var agora = dataHora ?? DateTime.UtcNow;
        tarefa.Finalizar(idMecanico, observacoes, agora);

        var todasConcluidas = false;
        if (_tarefas.Count > 0 && _tarefas.All(t => t.Status == StatusTarefaProducao.Concluida))
        {
            Status = StatusOrdemProducao.Concluida;
            DataConclusao = agora;
            todasConcluidas = true;
            RegistrarHistorico(Status, "Todas as tarefas foram concluídas com sucesso.", agora);
        }

        AtualizarTimestamp();
        return todasConcluidas;
    }

    /// <summary>
    /// Cancela a ordem de produção em qualquer etapa antes da conclusão.
    /// Interrompe automaticamente quaisquer tarefas pendentes ou em andamento.
    /// </summary>
    public void Cancelar(string motivo, DateTime? dataHora = null)
    {
        if (Status == StatusOrdemProducao.Concluida)
            throw new TransicaoEstadoInvalidaException("Não é possível cancelar uma ordem de produção que já foi totalmente concluída.");

        if (Status == StatusOrdemProducao.Cancelada)
            return; // Idempotente

        if (string.IsNullOrWhiteSpace(motivo))
            throw new DomainException("O motivo do cancelamento é obrigatório.");

        var agora = dataHora ?? DateTime.UtcNow;

        foreach (var tarefa in _tarefas.Where(t => t.Status is StatusTarefaProducao.Pendente or StatusTarefaProducao.EmExecucao))
        {
            tarefa.Interromper($"Cancelamento da ordem: {motivo}", agora);
        }

        Status = StatusOrdemProducao.Cancelada;
        DataConclusao ??= agora;
        AtualizarTimestamp();

        RegistrarHistorico(Status, $"Ordem cancelada: {motivo}", agora);
    }

    /// <summary>
    /// Calcula a duração total real em minutos somando o tempo gasto em todas as tarefas concluídas.
    /// </summary>
    public int ObterDuracaoTotalTarefasMinutos()
    {
        return _tarefas
            .Where(t => t.Status == StatusTarefaProducao.Concluida)
            .Sum(t => t.ObterDuracaoRealMinutos() ?? 0);
    }

    /// <summary>
    /// Incrementa o token de versão para controle de concorrência otimista.
    /// </summary>
    public void IncrementarVersao()
    {
        Versao++;
        AtualizarTimestamp();
    }

    private void RegistrarHistorico(StatusOrdemProducao status, string descricao, DateTime? dataHora = null)
    {
        _historico.Add(new HistoricoProducao(Id, status, descricao, dataHora ?? DateTime.UtcNow));
    }
}
