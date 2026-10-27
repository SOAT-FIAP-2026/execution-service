using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using FluentAssertions;

namespace Fiap.TechChallenge.Execution.UnitTests.Domain;

public class StatusEnumTests
{
    [Theory]
    [InlineData(StatusOrdemProducao.AguardandoDiagnostico, "AGUARDANDO_DIAGNOSTICO")]
    [InlineData(StatusOrdemProducao.EmDiagnostico, "EM_DIAGNOSTICO")]
    [InlineData(StatusOrdemProducao.DiagnosticoConcluido, "DIAGNOSTICO_CONCLUIDO")]
    [InlineData(StatusOrdemProducao.NaFila, "NA_FILA")]
    [InlineData(StatusOrdemProducao.EmExecucao, "EM_EXECUCAO")]
    [InlineData(StatusOrdemProducao.Concluida, "CONCLUIDA")]
    [InlineData(StatusOrdemProducao.Cancelada, "CANCELADA")]
    public void StatusOrdemProducao_ToUpperSnake_e_FromUpperSnake_devem_ser_bidirecionais(
        StatusOrdemProducao status, string upperSnakeEsperado)
    {
        status.ToUpperSnake().Should().Be(upperSnakeEsperado);
        StatusOrdemProducaoExtensions.FromUpperSnake(upperSnakeEsperado).Should().Be(status);
        StatusOrdemProducaoExtensions.FromUpperSnake(upperSnakeEsperado.ToLowerInvariant()).Should().Be(status);
    }

    [Fact]
    public void StatusOrdemProducao_FromUpperSnake_invalido_deve_lancar_excecao()
    {
        var act = () => StatusOrdemProducaoExtensions.FromUpperSnake("INVALIDO");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(StatusTarefaProducao.Pendente, "PENDENTE")]
    [InlineData(StatusTarefaProducao.EmExecucao, "EM_EXECUCAO")]
    [InlineData(StatusTarefaProducao.Concluida, "CONCLUIDA")]
    [InlineData(StatusTarefaProducao.Interrompida, "INTERROMPIDA")]
    public void StatusTarefaProducao_ToUpperSnake_e_FromUpperSnake_devem_ser_bidirecionais(
        StatusTarefaProducao status, string upperSnakeEsperado)
    {
        status.ToUpperSnake().Should().Be(upperSnakeEsperado);
        StatusTarefaProducaoExtensions.FromUpperSnake(upperSnakeEsperado).Should().Be(status);
        StatusTarefaProducaoExtensions.FromUpperSnake(upperSnakeEsperado.ToLowerInvariant()).Should().Be(status);
    }

    [Fact]
    public void StatusTarefaProducao_FromUpperSnake_invalido_deve_lancar_excecao()
    {
        var act = () => StatusTarefaProducaoExtensions.FromUpperSnake("INVALIDO");
        act.Should().Throw<ArgumentException>();
    }
}
