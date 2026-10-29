using Fiap.TechChallenge.Execution.Application.Queries;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Fiap.TechChallenge.Execution.UnitTests.Application;

public class QueriesTests
{
    private readonly IOrdemProducaoRepository _repository = Substitute.For<IOrdemProducaoRepository>();

    private static OrdemProducao CriarOrdemPadrao() =>
        OrdemProducao.CriarParaDiagnostico(
            Guid.NewGuid(),
            new ClienteSnapshot(Guid.NewGuid(), "Ana Souza", "11988887777"),
            new VeiculoSnapshot("XYZ9K88", "Toyota Corolla", 2023),
            "SP-01");

    [Fact]
    public async Task ObterFilaProducaoQuery_ComStatusValido_DeveFiltrarCorretamente()
    {
        var ordem = CriarOrdemPadrao();
        _repository.ObterFilaProducaoAsync("SP-01", StatusOrdemProducao.NaFila, Arg.Any<CancellationToken>())
            .Returns([ordem]);

        var query = new ObterFilaProducaoQuery(_repository);
        var result = await query.ExecuteAsync("NA_FILA", "SP-01");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(ordem.Id);
    }

    [Fact]
    public async Task ObterFilaProducaoQuery_ComStatusInvalido_DeveLancarDomainException()
    {
        var query = new ObterFilaProducaoQuery(_repository);

        var act = () => query.ExecuteAsync("STATUS_INEXISTENTE", "SP-01");

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ObterDiagnosticosPendentesQuery_DeveRetornarItensPendentes()
    {
        var ordem = CriarOrdemPadrao();
        _repository.ObterDiagnosticosPendentesAsync("SP-01", Arg.Any<CancellationToken>())
            .Returns([ordem]);

        var query = new ObterDiagnosticosPendentesQuery(_repository);
        var result = await query.ExecuteAsync("SP-01");

        result.Should().HaveCount(1);
        result[0].Status.Should().Be("AGUARDANDO_DIAGNOSTICO");
    }

    [Fact]
    public async Task ObterDetalhesOrdemProducaoQuery_QuandoNaoEncontrado_DeveLancar404()
    {
        var query = new ObterDetalhesOrdemProducaoQuery(_repository);

        var actPorId = () => query.ObterPorIdAsync(Guid.NewGuid());
        var actPorOs = () => query.ObterPorOrdemServicoIdAsync(Guid.NewGuid());

        await actPorId.Should().ThrowAsync<OrdemProducaoNaoEncontradaException>();
        await actPorOs.Should().ThrowAsync<OrdemProducaoNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterMetricasProdutividadeQuery_DeveCalcularIndiceEficienciaPorMecanico()
    {
        var idMecanico = Guid.NewGuid();
        var inicio = new DateTime(2026, 1, 10, 10, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddMinutes(40); // 40 min realizados para tarefa estimada em 60 min

        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de embreagem", 60);
        tarefa.AssociarOrdemProducao(Guid.NewGuid());
        tarefa.Iniciar(idMecanico, "Pedro Mecânico", inicio);
        tarefa.Finalizar(idMecanico, "Concluído antes do prazo", fim);

        _repository.ObterTarefasConcluidasPorPeriodoAsync(null, null, idMecanico, Arg.Any<CancellationToken>())
            .Returns([tarefa]);

        var query = new ObterMetricasProdutividadeQuery(_repository);
        var relatorio = await query.ExecuteAsync(null, null, idMecanico);

        relatorio.TotalTarefasConcluidas.Should().Be(1);
        relatorio.Mecanicos.Should().HaveCount(1);
        var m = relatorio.Mecanicos[0];
        m.IdMecanico.Should().Be(idMecanico);
        m.NomeMecanico.Should().Be("Pedro Mecânico");
        m.TempoTotalEstimadoMinutos.Should().Be(60);
        m.TempoTotalRealizadoMinutos.Should().Be(40);
        m.IndiceEficienciaPercentual.Should().Be(150.0);
    }

    [Fact]
    public async Task ObterMetricasProdutividadeQuery_QuandoDataInicialMaiorQueFinal_DeveLancarDomainException()
    {
        var query = new ObterMetricasProdutividadeQuery(_repository);
        var de = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var act = () => query.ExecuteAsync(de, ate);

        await act.Should().ThrowAsync<DomainException>();
    }
}
