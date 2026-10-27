using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using FluentAssertions;

namespace Fiap.TechChallenge.Execution.UnitTests.Domain;

public class DiagnosticoTests
{
    [Fact]
    public void Diagnostico_deve_ser_inicializado_com_dados_corretos()
    {
        var opId = Guid.NewGuid();
        var mecId = Guid.NewGuid();
        var inicio = DateTime.UtcNow.AddMinutes(-30);

        var diag = new Diagnostico(opId, mecId, "Carlos Mecânico", inicio);

        diag.OrdemProducaoId.Should().Be(opId);
        diag.IdMecanico.Should().Be(mecId);
        diag.NomeMecanico.Should().Be("Carlos Mecânico");
        diag.DataHoraInicio.Should().Be(inicio);
        diag.DataHoraFim.Should().BeNull();
        diag.LaudoTecnico.Should().BeNull();
        diag.ServicosSugeridos.Should().BeEmpty();
        diag.PecasSugeridas.Should().BeEmpty();
        diag.ObterDuracaoMinutos().Should().BeNull();
    }

    [Fact]
    public void Diagnostico_deve_ser_finalizado_com_laudo_e_itens_calculando_duracao()
    {
        var opId = Guid.NewGuid();
        var mecId = Guid.NewGuid();
        var inicio = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 10, 26, 10, 45, 0, DateTimeKind.Utc);

        var diag = new Diagnostico(opId, mecId, "Carlos", inicio);

        var servicoId = Guid.NewGuid();
        var pecaId = Guid.NewGuid();

        diag.Finalizar(
            "Vazamento na tampa de válvulas",
            [new ServicoSugerido(servicoId, "Troca de Junta")],
            [new PecaSugerida(pecaId, 2, "Junta de Borracha")],
            fim);

        diag.DataHoraFim.Should().Be(fim);
        diag.LaudoTecnico.Should().Be("Vazamento na tampa de válvulas");
        diag.ServicosSugeridos.Should().HaveCount(1);
        diag.ServicosSugeridos.First().IdServicoCatalogo.Should().Be(servicoId);
        diag.PecasSugeridas.Should().HaveCount(1);
        diag.PecasSugeridas.First().Quantidade.Should().Be(2);
        diag.ObterDuracaoMinutos().Should().Be(45);
    }

    [Fact]
    public void Finalizar_diagnostico_com_laudo_vazio_deve_lancar_excecao()
    {
        var diag = new Diagnostico(Guid.NewGuid(), Guid.NewGuid(), "Carlos");

        var act = () => diag.Finalizar("   ");
        act.Should().Throw<DomainException>()
            .WithMessage("*laudo técnico é obrigatório*");
    }

    [Fact]
    public void Finalizar_com_data_fim_anterior_ao_inicio_deve_lancar_excecao()
    {
        var inicio = DateTime.UtcNow;
        var diag = new Diagnostico(Guid.NewGuid(), Guid.NewGuid(), "Carlos", inicio);

        var act = () => diag.Finalizar("Laudo", dataHoraFim: inicio.AddMinutes(-10));
        act.Should().Throw<DomainException>()
            .WithMessage("*término do diagnóstico não pode ser anterior*");
    }

    [Fact]
    public void PecaSugerida_com_quantidade_invalida_deve_lancar_excecao()
    {
        var actZero = () => new PecaSugerida(Guid.NewGuid(), 0);
        actZero.Should().Throw<DomainException>()
            .WithMessage("*quantidade sugerida de peças deve ser maior que zero*");

        var actNegativo = () => new PecaSugerida(Guid.NewGuid(), -1);
        actNegativo.Should().Throw<DomainException>();
    }
}
