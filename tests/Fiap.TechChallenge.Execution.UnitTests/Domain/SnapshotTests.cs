using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using FluentAssertions;

namespace Fiap.TechChallenge.Execution.UnitTests.Domain;

public class SnapshotTests
{
    [Fact]
    public void ClienteSnapshot_com_dados_validos_deve_ser_criado()
    {
        var id = Guid.NewGuid();
        var snapshot = new ClienteSnapshot(id, "  Maria da Silva  ", "  11988887777  ");

        snapshot.IdCliente.Should().Be(id);
        snapshot.Nome.Should().Be("Maria da Silva");
        snapshot.Telefone.Should().Be("11988887777");
    }

    [Fact]
    public void ClienteSnapshot_sem_telefone_deve_permitir_nulo()
    {
        var id = Guid.NewGuid();
        var snapshot = new ClienteSnapshot(id, "Carlos", "   ");

        snapshot.Telefone.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ClienteSnapshot_com_nome_invalido_deve_lancar_excecao(string? nomeInvalido)
    {
        var act = () => new ClienteSnapshot(Guid.NewGuid(), nomeInvalido!);
        act.Should().Throw<DomainException>()
            .WithMessage("*nome do cliente é obrigatório*");
    }

    [Fact]
    public void ClienteSnapshot_com_id_vazio_deve_lancar_excecao()
    {
        var act = () => new ClienteSnapshot(Guid.Empty, "Carlos");
        act.Should().Throw<DomainException>()
            .WithMessage("*identificador do cliente não pode ser vazio*");
    }

    [Fact]
    public void VeiculoSnapshot_com_dados_validos_deve_ser_criado()
    {
        var snapshot = new VeiculoSnapshot("  abc1d23  ", "  Honda Civic  ", 2021);

        snapshot.Placa.Should().Be("ABC1D23");
        snapshot.Modelo.Should().Be("Honda Civic");
        snapshot.Ano.Should().Be(2021);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2099)]
    public void VeiculoSnapshot_com_ano_invalido_deve_lancar_excecao(int anoInvalido)
    {
        var act = () => new VeiculoSnapshot("ABC1D23", "Civic", anoInvalido);
        act.Should().Throw<DomainException>()
            .WithMessage("*ano do veículo*inválido*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void VeiculoSnapshot_com_placa_invalida_deve_lancar_excecao(string? placa)
    {
        var act = () => new VeiculoSnapshot(placa!, "Civic", 2020);
        act.Should().Throw<DomainException>()
            .WithMessage("*placa do veículo é obrigatória*");
    }
}
