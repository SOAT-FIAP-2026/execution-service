using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using FluentAssertions;

namespace Fiap.TechChallenge.Execution.UnitTests.Domain;

public class TarefaProducaoTests
{
    [Fact]
    public void TarefaProducao_deve_iniciar_no_status_pendente()
    {
        var servicoId = Guid.NewGuid();
        var tarefa = new TarefaProducao(servicoId, "Troca de Pastilhas", 45);

        tarefa.IdServicoOrigem.Should().Be(servicoId);
        tarefa.Descricao.Should().Be("Troca de Pastilhas");
        tarefa.TempoEstimadoMinutos.Should().Be(45);
        tarefa.Status.Should().Be(StatusTarefaProducao.Pendente);
        tarefa.DataHoraInicio.Should().BeNull();
        tarefa.DataHoraFim.Should().BeNull();
        tarefa.IdMecanicoResponsavel.Should().BeNull();
    }

    [Fact]
    public void Iniciar_tarefa_pendente_deve_transicionar_para_em_execucao()
    {
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de Óleo", 30);
        var mecId = Guid.NewGuid();
        var inicio = DateTime.UtcNow;

        tarefa.Iniciar(mecId, "Carlos Mecânico", inicio);

        tarefa.Status.Should().Be(StatusTarefaProducao.EmExecucao);
        tarefa.IdMecanicoResponsavel.Should().Be(mecId);
        tarefa.NomeMecanicoResponsavel.Should().Be("Carlos Mecânico");
        tarefa.DataHoraInicio.Should().Be(inicio);
    }

    [Fact]
    public void Iniciar_tarefa_ja_em_execucao_deve_lancar_excecao()
    {
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de Óleo", 30);
        tarefa.Iniciar(Guid.NewGuid(), "Carlos");

        var act = () => tarefa.Iniciar(Guid.NewGuid(), "Outro");
        act.Should().Throw<TransicaoEstadoInvalidaException>()
            .WithMessage("*Não é possível iniciar uma tarefa no status*");
    }

    [Fact]
    public void Finalizar_tarefa_pelo_mecanico_responsavel_deve_concluir_e_calcular_duracao()
    {
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de Óleo", 30);
        var mecId = Guid.NewGuid();
        var inicio = new DateTime(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 10, 26, 14, 35, 0, DateTimeKind.Utc);

        tarefa.Iniciar(mecId, "Carlos", inicio);
        tarefa.Finalizar(mecId, "Óleo 5W30 substituído com sucesso", fim);

        tarefa.Status.Should().Be(StatusTarefaProducao.Concluida);
        tarefa.DataHoraFim.Should().Be(fim);
        tarefa.ObservacoesTecnicas.Should().Be("Óleo 5W30 substituído com sucesso");
        tarefa.ObterDuracaoRealMinutos().Should().Be(35);
    }

    [Fact]
    public void Finalizar_tarefa_por_outro_mecanico_deve_lancar_excecao()
    {
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de Óleo", 30);
        var mecOriginal = Guid.NewGuid();
        tarefa.Iniciar(mecOriginal, "Carlos");

        var act = () => tarefa.Finalizar(Guid.NewGuid(), "Concluído");
        act.Should().Throw<MecanicoNaoAutorizadoException>()
            .WithMessage("*Somente o mecânico responsável*");
    }

    [Fact]
    public void Finalizar_tarefa_pendente_deve_lancar_excecao()
    {
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de Óleo", 30);

        var act = () => tarefa.Finalizar(Guid.NewGuid());
        act.Should().Throw<TransicaoEstadoInvalidaException>()
            .WithMessage("*Não é possível finalizar uma tarefa no status*");
    }

    [Fact]
    public void Interromper_tarefa_em_execucao_deve_marcar_interrompida_e_definir_fim()
    {
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de Óleo", 30);
        tarefa.Iniciar(Guid.NewGuid(), "Carlos");

        tarefa.Interromper("Ordem cancelada pelo cliente");

        tarefa.Status.Should().Be(StatusTarefaProducao.Interrompida);
        tarefa.ObservacoesTecnicas.Should().Be("Ordem cancelada pelo cliente");
        tarefa.DataHoraFim.Should().NotBeNull();
    }

    [Fact]
    public void Interromper_tarefa_concluida_nao_deve_alterar_seu_status()
    {
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de Óleo", 30);
        var mecId = Guid.NewGuid();
        tarefa.Iniciar(mecId, "Carlos");
        tarefa.Finalizar(mecId);

        tarefa.Interromper("Tentativa de interrupção após conclusão");

        tarefa.Status.Should().Be(StatusTarefaProducao.Concluida);
    }
}
