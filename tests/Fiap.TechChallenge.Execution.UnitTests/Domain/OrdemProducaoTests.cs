using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using FluentAssertions;

namespace Fiap.TechChallenge.Execution.UnitTests.Domain;

public class OrdemProducaoTests
{
    private readonly ClienteSnapshot _clientePadrao = new(Guid.NewGuid(), "João Silva", "11999998888");
    private readonly VeiculoSnapshot _veiculoPadrao = new("ABC1D23", "Civic 2.0", 2021);

    #region Criação

    [Fact]
    public void CriarParaDiagnostico_deve_inicializar_em_aguardando_diagnostico_com_historico()
    {
        var osId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        var ordem = OrdemProducao.CriarParaDiagnostico(osId, _clientePadrao, _veiculoPadrao, "sp-matriz", agora);

        ordem.Id.Should().NotBeEmpty();
        ordem.OrdemServicoId.Should().Be(osId);
        ordem.FilialId.Should().Be("sp-matriz");
        ordem.Status.Should().Be(StatusOrdemProducao.AguardandoDiagnostico);
        ordem.Versao.Should().Be(1);
        ordem.Cliente.Should().Be(_clientePadrao);
        ordem.Veiculo.Should().Be(_veiculoPadrao);
        ordem.DataAbertura.Should().Be(agora);
        ordem.DataLiberacao.Should().BeNull();
        ordem.DataConclusao.Should().BeNull();
        ordem.Diagnostico.Should().BeNull();
        ordem.Tarefas.Should().BeEmpty();

        ordem.Historico.Should().HaveCount(1);
        ordem.Historico.First().Status.Should().Be(StatusOrdemProducao.AguardandoDiagnostico);
    }

    [Fact]
    public void CriarParaDiagnostico_com_osId_vazio_deve_lancar_excecao()
    {
        var act = () => OrdemProducao.CriarParaDiagnostico(Guid.Empty, _clientePadrao, _veiculoPadrao);
        act.Should().Throw<DomainException>()
            .WithMessage("*identificador da Ordem de Serviço*obrigatório*");
    }

    #endregion

    #region Diagnóstico

    [Fact]
    public void IniciarDiagnostico_a_partir_de_aguardando_deve_transicionar_para_em_diagnostico()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        var mecId = Guid.NewGuid();

        ordem.IniciarDiagnostico(mecId, "Carlos Mecânico");

        ordem.Status.Should().Be(StatusOrdemProducao.EmDiagnostico);
        ordem.Diagnostico.Should().NotBeNull();
        ordem.Diagnostico!.IdMecanico.Should().Be(mecId);
        ordem.Diagnostico.NomeMecanico.Should().Be("Carlos Mecânico");
        ordem.Historico.Should().HaveCount(2);
        ordem.Historico.Last().Status.Should().Be(StatusOrdemProducao.EmDiagnostico);
    }

    [Fact]
    public void FinalizarDiagnostico_com_sucesso_deve_transicionar_para_diagnostico_concluido()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        var mecId = Guid.NewGuid();
        ordem.IniciarDiagnostico(mecId, "Carlos Mecânico");

        var servicos = new[] { new ServicoSugerido(Guid.NewGuid(), "Troca de Óleo") };
        var pecas = new[] { new PecaSugerida(Guid.NewGuid(), 4, "Óleo 5W30") };

        ordem.FinalizarDiagnostico(mecId, "Laudo pericial completo", servicos, pecas);

        ordem.Status.Should().Be(StatusOrdemProducao.DiagnosticoConcluido);
        ordem.Diagnostico!.LaudoTecnico.Should().Be("Laudo pericial completo");
        ordem.Diagnostico.ServicosSugeridos.Should().HaveCount(1);
        ordem.Diagnostico.PecasSugeridas.Should().HaveCount(1);
        ordem.Historico.Last().Status.Should().Be(StatusOrdemProducao.DiagnosticoConcluido);
    }

    [Fact]
    public void FinalizarDiagnostico_por_outro_mecanico_deve_lancar_excecao()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        ordem.IniciarDiagnostico(Guid.NewGuid(), "Carlos");

        var outroMecanico = Guid.NewGuid();
        var act = () => ordem.FinalizarDiagnostico(outroMecanico, "Laudo");

        act.Should().Throw<MecanicoNaoAutorizadoException>()
            .WithMessage("*Apenas o mecânico responsável*");
    }

    [Fact]
    public void ReabrirDiagnostico_deve_retornar_para_em_diagnostico()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        var mecId = Guid.NewGuid();
        ordem.IniciarDiagnostico(mecId, "Carlos");
        ordem.FinalizarDiagnostico(mecId, "Laudo prévio");

        ordem.ReabrirDiagnostico("Peça sugerida fora de linha no fornecedor");

        ordem.Status.Should().Be(StatusOrdemProducao.EmDiagnostico);
        ordem.Historico.Last().Descricao.Should().Contain("fora de linha no fornecedor");
    }

    [Fact]
    public void ReabrirDiagnostico_em_ordem_nao_concluida_de_diagnostico_deve_lancar_excecao()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);

        var act = () => ordem.ReabrirDiagnostico("Motivo");
        act.Should().Throw<TransicaoEstadoInvalidaException>()
            .WithMessage("*Apenas ordens com diagnóstico concluído podem ser reabertas*");
    }

    #endregion

    #region Fila de Produção e Execução de Tarefas

    [Fact]
    public void LiberarParaExecucao_deve_transicionar_para_na_fila_e_anexar_tarefas()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        var tarefas = new[]
        {
            new TarefaProducao(Guid.NewGuid(), "Troca de Óleo", 30),
            new TarefaProducao(Guid.NewGuid(), "Alinhamento", 40)
        };
        var agora = DateTime.UtcNow;

        ordem.LiberarParaExecucao(tarefas, agora);

        ordem.Status.Should().Be(StatusOrdemProducao.NaFila);
        ordem.DataLiberacao.Should().Be(agora);
        ordem.Tarefas.Should().HaveCount(2);
        ordem.Tarefas.Should().OnlyContain(t => t.OrdemProducaoId == ordem.Id);
        ordem.Historico.Last().Status.Should().Be(StatusOrdemProducao.NaFila);
    }

    [Fact]
    public void LiberarParaExecucao_sem_diagnostico_concluido_deve_lancar_excecao()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        var tarefas = new[] { new TarefaProducao(Guid.NewGuid(), "Tarefa", 30) };

        var act = () => ordem.LiberarParaExecucao(tarefas);
        act.Should().Throw<TransicaoEstadoInvalidaException>()
            .WithMessage("*diagnóstico prévio concluído*");
    }

    [Fact]
    public void LiberarParaExecucao_sem_tarefas_deve_lancar_excecao()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();

        var act = () => ordem.LiberarParaExecucao([]);
        act.Should().Throw<DomainException>()
            .WithMessage("*pelo menos uma tarefa de reparo*");
    }

    [Fact]
    public void IniciarTarefa_primeira_tarefa_deve_transicionar_para_em_execucao_e_retornar_true()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        var t1 = new TarefaProducao(Guid.NewGuid(), "Tarefa 1", 30);
        var t2 = new TarefaProducao(Guid.NewGuid(), "Tarefa 2", 30);
        ordem.LiberarParaExecucao([t1, t2]);

        var primeiraIniciada = ordem.IniciarTarefa(t1.Id, Guid.NewGuid(), "Mecânico A");

        primeiraIniciada.Should().BeTrue("a primeira tarefa iniciada dispara ProducaoIniciadaEvent");
        ordem.Status.Should().Be(StatusOrdemProducao.EmExecucao);
        t1.Status.Should().Be(StatusTarefaProducao.EmExecucao);
    }

    [Fact]
    public void IniciarTarefa_segunda_tarefa_deve_manter_em_execucao_e_retornar_false()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        var t1 = new TarefaProducao(Guid.NewGuid(), "Tarefa 1", 30);
        var t2 = new TarefaProducao(Guid.NewGuid(), "Tarefa 2", 30);
        ordem.LiberarParaExecucao([t1, t2]);

        ordem.IniciarTarefa(t1.Id, Guid.NewGuid(), "Mecânico A");
        var segundaIniciada = ordem.IniciarTarefa(t2.Id, Guid.NewGuid(), "Mecânico B");

        segundaIniciada.Should().BeFalse("a segunda tarefa não deve reemitir ProducaoIniciadaEvent");
        ordem.Status.Should().Be(StatusOrdemProducao.EmExecucao);
    }

    [Fact]
    public void FinalizarTarefa_parcial_deve_manter_ordem_em_execucao_e_retornar_false()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        var t1 = new TarefaProducao(Guid.NewGuid(), "Tarefa 1", 30);
        var t2 = new TarefaProducao(Guid.NewGuid(), "Tarefa 2", 30);
        ordem.LiberarParaExecucao([t1, t2]);

        var mecA = Guid.NewGuid();
        ordem.IniciarTarefa(t1.Id, mecA, "Mecânico A");

        var todasConcluidas = ordem.FinalizarTarefa(t1.Id, mecA, "Concluído");

        todasConcluidas.Should().BeFalse();
        ordem.Status.Should().Be(StatusOrdemProducao.EmExecucao);
        ordem.DataConclusao.Should().BeNull();
    }

    [Fact]
    public void FinalizarTarefa_ultima_deve_transicionar_ordem_para_concluida_e_retornar_true()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        var t1 = new TarefaProducao(Guid.NewGuid(), "Tarefa 1", 30);
        ordem.LiberarParaExecucao([t1]);

        var mecA = Guid.NewGuid();
        var inicio = new DateTime(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 10, 26, 15, 40, 0, DateTimeKind.Utc);

        ordem.IniciarTarefa(t1.Id, mecA, "Mecânico A", inicio);
        var todasConcluidas = ordem.FinalizarTarefa(t1.Id, mecA, "Feito", fim);

        todasConcluidas.Should().BeTrue("todas as tarefas concluídas disparam ProducaoFinalizadaEvent");
        ordem.Status.Should().Be(StatusOrdemProducao.Concluida);
        ordem.DataConclusao.Should().Be(fim);
        ordem.ObterDuracaoTotalTarefasMinutos().Should().Be(40);
        ordem.Historico.Last().Status.Should().Be(StatusOrdemProducao.Concluida);
    }

    [Fact]
    public void IniciarTarefa_com_id_inexistente_deve_lancar_TarefaProducaoNaoEncontradaException()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        ordem.LiberarParaExecucao([new TarefaProducao(Guid.NewGuid(), "T1", 30)]);

        var tarefaInexistente = Guid.NewGuid();
        var act = () => ordem.IniciarTarefa(tarefaInexistente, Guid.NewGuid(), "Mecânico");

        act.Should().Throw<TarefaProducaoNaoEncontradaException>()
            .WithMessage($"*{tarefaInexistente}*");
    }

    [Fact]
    public void FinalizarTarefa_com_id_inexistente_deve_lancar_TarefaProducaoNaoEncontradaException()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        var t1 = new TarefaProducao(Guid.NewGuid(), "T1", 30);
        ordem.LiberarParaExecucao([t1]);
        ordem.IniciarTarefa(t1.Id, Guid.NewGuid(), "Mecânico");

        var tarefaInexistente = Guid.NewGuid();
        var act = () => ordem.FinalizarTarefa(tarefaInexistente, Guid.NewGuid());

        act.Should().Throw<TarefaProducaoNaoEncontradaException>()
            .WithMessage($"*{tarefaInexistente}*");
    }

    [Fact]
    public void IncrementarVersao_deve_avancar_token_de_concorrencia()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        ordem.Versao.Should().Be(1);

        ordem.IncrementarVersao();

        ordem.Versao.Should().Be(2);
    }

    #endregion

    #region Cancelamento e Compensações

    [Theory]
    [InlineData(StatusOrdemProducao.AguardandoDiagnostico)]
    [InlineData(StatusOrdemProducao.EmDiagnostico)]
    [InlineData(StatusOrdemProducao.DiagnosticoConcluido)]
    [InlineData(StatusOrdemProducao.NaFila)]
    public void Cancelar_em_qualquer_etapa_previa_a_conclusao_deve_transicionar_para_cancelada(StatusOrdemProducao statusAlvo)
    {
        var ordem = CriarOrdemNoStatus(statusAlvo);

        ordem.Cancelar("Cancelamento solicitado");

        ordem.Status.Should().Be(StatusOrdemProducao.Cancelada);
        ordem.Historico.Last().Status.Should().Be(StatusOrdemProducao.Cancelada);
    }

    [Fact]
    public void Cancelar_em_execucao_deve_interromper_tarefas_pendentes_e_em_andamento()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        var t1 = new TarefaProducao(Guid.NewGuid(), "Tarefa 1", 30);
        var t2 = new TarefaProducao(Guid.NewGuid(), "Tarefa 2", 30);
        ordem.LiberarParaExecucao([t1, t2]);

        ordem.IniciarTarefa(t1.Id, Guid.NewGuid(), "Mecânico A");

        ordem.Cancelar("Motor com trinca no bloco irreparável");

        ordem.Status.Should().Be(StatusOrdemProducao.Cancelada);
        t1.Status.Should().Be(StatusTarefaProducao.Interrompida);
        t2.Status.Should().Be(StatusTarefaProducao.Interrompida);
    }

    [Fact]
    public void Cancelar_ordem_ja_concluida_deve_lancar_excecao()
    {
        var ordem = CriarOrdemComDiagnosticoConcluido();
        var t1 = new TarefaProducao(Guid.NewGuid(), "Tarefa 1", 30);
        ordem.LiberarParaExecucao([t1]);
        var mecA = Guid.NewGuid();
        ordem.IniciarTarefa(t1.Id, mecA, "A");
        ordem.FinalizarTarefa(t1.Id, mecA);

        var act = () => ordem.Cancelar("Tentativa de cancelamento pós-conclusão");

        act.Should().Throw<TransicaoEstadoInvalidaException>()
            .WithMessage("*Não é possível cancelar uma ordem de produção que já foi totalmente concluída*");
    }

    [Fact]
    public void Cancelar_ordem_ja_cancelada_deve_ser_idempotente()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        ordem.Cancelar("Motivo 1");

        var totalHistorico = ordem.Historico.Count;

        ordem.Cancelar("Motivo 2");

        ordem.Status.Should().Be(StatusOrdemProducao.Cancelada);
        ordem.Historico.Count.Should().Be(totalHistorico, "não duplica histórico no cancelamento idempotente");
    }

    #endregion

    #region Helpers

    private OrdemProducao CriarOrdemComDiagnosticoConcluido()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        var mecId = Guid.NewGuid();
        ordem.IniciarDiagnostico(mecId, "Carlos");
        ordem.FinalizarDiagnostico(mecId, "Diagnóstico finalizado");
        return ordem;
    }

    private OrdemProducao CriarOrdemNoStatus(StatusOrdemProducao status)
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao);
        var mecId = Guid.NewGuid();

        switch (status)
        {
            case StatusOrdemProducao.AguardandoDiagnostico:
                return ordem;

            case StatusOrdemProducao.EmDiagnostico:
                ordem.IniciarDiagnostico(mecId, "Carlos");
                return ordem;

            case StatusOrdemProducao.DiagnosticoConcluido:
                ordem.IniciarDiagnostico(mecId, "Carlos");
                ordem.FinalizarDiagnostico(mecId, "Laudo");
                return ordem;

            case StatusOrdemProducao.NaFila:
                ordem.IniciarDiagnostico(mecId, "Carlos");
                ordem.FinalizarDiagnostico(mecId, "Laudo");
                ordem.LiberarParaExecucao([new TarefaProducao(Guid.NewGuid(), "T1", 30)]);
                return ordem;

            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    #endregion
}
