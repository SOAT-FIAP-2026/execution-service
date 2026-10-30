using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Application.Consumers;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Fiap.TechChallenge.Execution.UnitTests.Consumers;

public class ConsumersTests
{
    private readonly IOrdemProducaoRepository _repository = Substitute.For<IOrdemProducaoRepository>();

    private static OrdemProducao CriarOrdemPadrao(Guid? osId = null) =>
        OrdemProducao.CriarParaDiagnostico(
            osId ?? Guid.NewGuid(),
            new ClienteSnapshot(Guid.NewGuid(), "Carlos Silva", "11999999999"),
            new VeiculoSnapshot("ABC1D23", "Honda Civic", 2022),
            "SP-01");

    #region OrdemServicoAbertaConsumer Tests

    [Fact]
    public async Task OrdemServicoAbertaConsumer_QuandoOrdemNaoExiste_DeveCriarOrdemComStatusAguardandoDiagnostico()
    {
        var osId = Guid.NewGuid();
        var evento = OrdemServicoAbertaEvent.Create(
            osId,
            new ClienteContrato(Guid.NewGuid(), "Carlos Silva", "11999999999"),
            new VeiculoContrato("ABC1D23", "Honda Civic", 2022),
            "SP-01");

        _repository.ExistePorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns(false);

        var context = Substitute.For<ConsumeContext<OrdemServicoAbertaEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new OrdemServicoAbertaConsumer(_repository, NullLogger<OrdemServicoAbertaConsumer>.Instance);
        await consumer.Consume(context);

        await _repository.Received(1).AdicionarAsync(
            Arg.Is<OrdemProducao>(o =>
                o.OrdemServicoId == osId &&
                o.Status == StatusOrdemProducao.AguardandoDiagnostico &&
                o.FilialId == "SP-01" &&
                o.Cliente.Nome == "Carlos Silva" &&
                o.Veiculo.Placa == "ABC1D23"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrdemServicoAbertaConsumer_QuandoOrdemJaExiste_DeveDescartarDeFormaIdempotente()
    {
        var osId = Guid.NewGuid();
        var evento = OrdemServicoAbertaEvent.Create(
            osId,
            new ClienteContrato(Guid.NewGuid(), "Carlos Silva", "11999999999"),
            new VeiculoContrato("ABC1D23", "Honda Civic", 2022));

        _repository.ExistePorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns(true);

        var context = Substitute.For<ConsumeContext<OrdemServicoAbertaEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new OrdemServicoAbertaConsumer(_repository, NullLogger<OrdemServicoAbertaConsumer>.Instance);
        await consumer.Consume(context);

        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<OrdemProducao>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region DiagnosticoRejeitadoConsumer Tests

    [Fact]
    public async Task DiagnosticoRejeitadoConsumer_QuandoStatusDiagnosticoConcluido_DeveReabrirDiagnostico()
    {
        var osId = Guid.NewGuid();
        var ordem = CriarOrdemPadrao(osId);
        var idMecanico = Guid.NewGuid();
        ordem.IniciarDiagnostico(idMecanico, "Pedro");
        ordem.FinalizarDiagnostico(idMecanico, "Pastilhas gastas");

        ordem.Status.Should().Be(StatusOrdemProducao.DiagnosticoConcluido);
        _repository.ObterPorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns(ordem);

        var evento = DiagnosticoRejeitadoEvent.Create(osId, "Item sem estoque no catálogo");
        var context = Substitute.For<ConsumeContext<DiagnosticoRejeitadoEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new DiagnosticoRejeitadoConsumer(_repository, NullLogger<DiagnosticoRejeitadoConsumer>.Instance);
        await consumer.Consume(context);

        ordem.Status.Should().Be(StatusOrdemProducao.EmDiagnostico);
        await _repository.Received(1).AtualizarAsync(ordem, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiagnosticoRejeitadoConsumer_QuandoJaEmDiagnostico_DeveDescartarDeFormaIdempotente()
    {
        var osId = Guid.NewGuid();
        var ordem = CriarOrdemPadrao(osId);
        ordem.IniciarDiagnostico(Guid.NewGuid(), "Pedro");
        ordem.Status.Should().Be(StatusOrdemProducao.EmDiagnostico);

        _repository.ObterPorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns(ordem);

        var evento = DiagnosticoRejeitadoEvent.Create(osId, "Rejeição duplicada");
        var context = Substitute.For<ConsumeContext<DiagnosticoRejeitadoEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new DiagnosticoRejeitadoConsumer(_repository, NullLogger<DiagnosticoRejeitadoConsumer>.Instance);
        await consumer.Consume(context);

        await _repository.DidNotReceive().AtualizarAsync(ordem, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiagnosticoRejeitadoConsumer_QuandoOrdemNaoEncontrada_DeveLancarExcecaoParaRetry()
    {
        var osId = Guid.NewGuid();
        _repository.ObterPorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns((OrdemProducao?)null);

        var evento = DiagnosticoRejeitadoEvent.Create(osId, "Rejeição");
        var context = Substitute.For<ConsumeContext<DiagnosticoRejeitadoEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new DiagnosticoRejeitadoConsumer(_repository, NullLogger<DiagnosticoRejeitadoConsumer>.Instance);
        var act = () => consumer.Consume(context);

        await act.Should().ThrowAsync<OrdemProducaoNaoEncontradaException>();
    }

    #endregion

    #region OrdemServicoLiberadaParaExecucaoConsumer Tests

    [Fact]
    public async Task OrdemServicoLiberadaParaExecucaoConsumer_DeveAdicionarTarefasETransicionarParaNaFila()
    {
        var osId = Guid.NewGuid();
        var ordem = CriarOrdemPadrao(osId);
        var idMecanico = Guid.NewGuid();
        ordem.IniciarDiagnostico(idMecanico, "Pedro");
        ordem.FinalizarDiagnostico(idMecanico, "Laudo aprovado");

        _repository.ObterPorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns(ordem);

        var t1 = new TarefaItemContrato(Guid.NewGuid(), "Troca de pastilhas", 45);
        var t2 = new TarefaItemContrato(Guid.NewGuid(), "Sangria de freio", 30);
        var evento = OrdemServicoLiberadaParaExecucaoEvent.Create(osId, [t1, t2]);

        var context = Substitute.For<ConsumeContext<OrdemServicoLiberadaParaExecucaoEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new OrdemServicoLiberadaParaExecucaoConsumer(_repository, NullLogger<OrdemServicoLiberadaParaExecucaoConsumer>.Instance);
        await consumer.Consume(context);

        ordem.Status.Should().Be(StatusOrdemProducao.NaFila);
        ordem.Tarefas.Should().HaveCount(2);
        ordem.Tarefas.Select(t => t.Descricao).Should().Contain(["Troca de pastilhas", "Sangria de freio"]);
        await _repository.Received(1).AtualizarAsync(ordem, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrdemServicoLiberadaParaExecucaoConsumer_QuandoJaLiberada_DeveDescartarDeFormaIdempotente()
    {
        var osId = Guid.NewGuid();
        var ordem = CriarOrdemPadrao(osId);
        var idMecanico = Guid.NewGuid();
        ordem.IniciarDiagnostico(idMecanico, "Pedro");
        ordem.FinalizarDiagnostico(idMecanico, "Laudo aprovado");
        ordem.LiberarParaExecucao([new TarefaProducao(Guid.NewGuid(), "Tarefa existente", 30)]);
        ordem.Status.Should().Be(StatusOrdemProducao.NaFila);

        _repository.ObterPorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns(ordem);

        var evento = OrdemServicoLiberadaParaExecucaoEvent.Create(
            osId,
            [new TarefaItemContrato(Guid.NewGuid(), "Tentativa duplicada", 30)]);

        var context = Substitute.For<ConsumeContext<OrdemServicoLiberadaParaExecucaoEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new OrdemServicoLiberadaParaExecucaoConsumer(_repository, NullLogger<OrdemServicoLiberadaParaExecucaoConsumer>.Instance);
        await consumer.Consume(context);

        await _repository.DidNotReceive().AtualizarAsync(ordem, Arg.Any<CancellationToken>());
    }

    #endregion

    #region OrdemServicoCanceladaConsumer Tests (Compensação A)

    [Fact]
    public async Task OrdemServicoCanceladaConsumer_DeveCancelarOrdemEInterromperTarefas()
    {
        var osId = Guid.NewGuid();
        var ordem = CriarOrdemPadrao(osId);
        var idMecanico = Guid.NewGuid();
        ordem.IniciarDiagnostico(idMecanico, "Pedro");
        ordem.FinalizarDiagnostico(idMecanico, "Laudo");
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Troca de amortecedor", 60);
        ordem.LiberarParaExecucao([tarefa]);
        ordem.IniciarTarefa(tarefa.Id, idMecanico, "Pedro");

        _repository.ObterPorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns(ordem);

        var evento = OrdemServicoCanceladaEvent.Create(osId, "Pagamento PIX expirado no Billing Service");
        var context = Substitute.For<ConsumeContext<OrdemServicoCanceladaEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new OrdemServicoCanceladaConsumer(_repository, NullLogger<OrdemServicoCanceladaConsumer>.Instance);
        await consumer.Consume(context);

        ordem.Status.Should().Be(StatusOrdemProducao.Cancelada);
        ordem.Tarefas.First().Status.Should().Be(StatusTarefaProducao.Interrompida);
        await _repository.Received(1).AtualizarAsync(ordem, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrdemServicoCanceladaConsumer_QuandoJaCancelada_DeveDescartarDeFormaIdempotente()
    {
        var osId = Guid.NewGuid();
        var ordem = CriarOrdemPadrao(osId);
        ordem.Cancelar("Cancelamento inicial");
        ordem.Status.Should().Be(StatusOrdemProducao.Cancelada);

        _repository.ObterPorOrdemServicoIdAsync(osId, Arg.Any<CancellationToken>()).Returns(ordem);

        var evento = OrdemServicoCanceladaEvent.Create(osId, "Cancelamento repetido");
        var context = Substitute.For<ConsumeContext<OrdemServicoCanceladaEvent>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        var consumer = new OrdemServicoCanceladaConsumer(_repository, NullLogger<OrdemServicoCanceladaConsumer>.Instance);
        await consumer.Consume(context);

        await _repository.DidNotReceive().AtualizarAsync(ordem, Arg.Any<CancellationToken>());
    }

    #endregion
}
