using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Fiap.TechChallenge.Execution.Application.UseCases;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Fiap.TechChallenge.Execution.UnitTests.Application;

public class UseCasesTests
{
    private readonly IOrdemProducaoRepository _repository = Substitute.For<IOrdemProducaoRepository>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();

    private static OrdemProducao CriarOrdemPadrao() =>
        OrdemProducao.CriarParaDiagnostico(
            Guid.NewGuid(),
            new ClienteSnapshot(Guid.NewGuid(), "Carlos Silva", "11999999999"),
            new VeiculoSnapshot("ABC1D23", "Honda Civic", 2022),
            "SP-01");

    [Fact]
    public async Task IniciarDiagnosticoUseCase_DeveAtualizarOrdemEPublicarEvento()
    {
        var ordem = CriarOrdemPadrao();
        var idMecanico = Guid.NewGuid();
        _repository.ObterPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);

        var useCase = new IniciarDiagnosticoUseCase(_repository, _eventPublisher);

        var response = await useCase.ExecuteAsync(ordem.Id, idMecanico, "João Mecânico");

        response.Status.Should().Be("EM_DIAGNOSTICO");
        response.Diagnostico.Should().NotBeNull();
        response.Diagnostico!.IdMecanico.Should().Be(idMecanico);
        await _repository.Received(1).AtualizarAsync(ordem, Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<DiagnosticoIniciadoEvent>(e => e.OsId == ordem.OrdemServicoId && e.IdMecanico == idMecanico),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IniciarDiagnosticoUseCase_QuandoOrdemNaoExiste_DeveLancarExcecao404()
    {
        var idInexistente = Guid.NewGuid();
        _repository.ObterPorIdAsync(idInexistente, Arg.Any<CancellationToken>()).Returns((OrdemProducao?)null);

        var useCase = new IniciarDiagnosticoUseCase(_repository, _eventPublisher);

        var act = () => useCase.ExecuteAsync(idInexistente, Guid.NewGuid(), "João Mecânico");

        await act.Should().ThrowAsync<OrdemProducaoNaoEncontradaException>();
    }

    [Fact]
    public async Task FinalizarDiagnosticoUseCase_DeveConcluirDiagnosticoEPublicarEventoComItens()
    {
        var ordem = CriarOrdemPadrao();
        var idMecanico = Guid.NewGuid();
        ordem.IniciarDiagnostico(idMecanico, "João Mecânico");
        _repository.ObterPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);

        var useCase = new FinalizarDiagnosticoUseCase(_repository, _eventPublisher);
        var request = new FinalizarDiagnosticoRequest(
            "Pastilhas e discos dianteiros gastos",
            [new ServicoSugeridoRequest(Guid.NewGuid(), "Troca de pastilhas")],
            [new PecaSugeridaRequest(Guid.NewGuid(), 1, "Jogo de pastilhas")]);

        var response = await useCase.ExecuteAsync(ordem.Id, idMecanico, request);

        response.Status.Should().Be("DIAGNOSTICO_CONCLUIDO");
        response.Diagnostico!.ServicosSugeridos.Should().HaveCount(1);
        response.Diagnostico.PecasSugeridas.Should().HaveCount(1);
        await _repository.Received(1).AtualizarAsync(ordem, Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<DiagnosticoTecnicoConcluidoEvent>(e =>
                e.OsId == ordem.OrdemServicoId &&
                e.Servicos.Count == 1 &&
                e.Pecas.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IniciarTarefaUseCase_DevePublicarProducaoIniciadaApenasNaPrimeiraTarefa()
    {
        var ordem = CriarOrdemPadrao();
        var idMecanico = Guid.NewGuid();
        ordem.IniciarDiagnostico(idMecanico, "João Mecânico");
        ordem.FinalizarDiagnostico(
            idMecanico,
            "Laudo completo",
            [new ServicoSugerido(Guid.NewGuid(), "Alinhamento")]);

        var t1 = new TarefaProducao(Guid.NewGuid(), "Alinhamento", 30);
        var t2 = new TarefaProducao(Guid.NewGuid(), "Balanceamento", 30);
        ordem.LiberarParaExecucao([t1, t2]);

        _repository.ObterPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        var useCase = new IniciarTarefaUseCase(_repository, _eventPublisher);

        // 1ª tarefa -> publica ProducaoIniciadaEvent
        var resp1 = await useCase.ExecuteAsync(ordem.Id, t1.Id, idMecanico, "João Mecânico");
        resp1.Status.Should().Be("EM_EXECUCAO");
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Any<ProducaoIniciadaEvent>(),
            Arg.Any<CancellationToken>());

        // 2ª tarefa -> NÃO publica ProducaoIniciadaEvent novamente
        var resp2 = await useCase.ExecuteAsync(ordem.Id, t2.Id, idMecanico, "João Mecânico");
        resp2.Status.Should().Be("EM_EXECUCAO");
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Any<ProducaoIniciadaEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizarTarefaUseCase_DevePublicarProducaoFinalizadaApenasNaUltimaTarefa()
    {
        var ordem = CriarOrdemPadrao();
        var idMecanico = Guid.NewGuid();
        ordem.IniciarDiagnostico(idMecanico, "João Mecânico");
        ordem.FinalizarDiagnostico(
            idMecanico,
            "Laudo completo",
            [new ServicoSugerido(Guid.NewGuid(), "Alinhamento")]);

        var t1 = new TarefaProducao(Guid.NewGuid(), "Alinhamento", 30);
        var t2 = new TarefaProducao(Guid.NewGuid(), "Balanceamento", 30);
        ordem.LiberarParaExecucao([t1, t2]);
        ordem.IniciarTarefa(t1.Id, idMecanico, "João Mecânico");
        ordem.IniciarTarefa(t2.Id, idMecanico, "João Mecânico");

        _repository.ObterPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        var useCase = new FinalizarTarefaUseCase(_repository, _eventPublisher);

        // Finaliza 1ª tarefa -> ainda resta 1 em execução
        var resp1 = await useCase.ExecuteAsync(ordem.Id, t1.Id, idMecanico, "Alinhamento concluído");
        resp1.Status.Should().Be("EM_EXECUCAO");
        await _eventPublisher.DidNotReceive().PublishAsync(
            Arg.Any<ProducaoFinalizadaEvent>(),
            Arg.Any<CancellationToken>());

        // Finaliza 2ª tarefa -> todas concluídas -> publica ProducaoFinalizadaEvent
        var resp2 = await useCase.ExecuteAsync(ordem.Id, t2.Id, idMecanico, "Balanceamento concluído");
        resp2.Status.Should().Be("CONCLUIDA");
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<ProducaoFinalizadaEvent>(e => e.OsId == ordem.OrdemServicoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelarProducaoUseCase_DeveCancelarEPublicarEventoDeFormaIdempotente()
    {
        var ordem = CriarOrdemPadrao();
        _repository.ObterPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        var useCase = new CancelarProducaoUseCase(_repository, _eventPublisher);

        var resp1 = await useCase.ExecuteAsync(ordem.Id, "Desistência do cliente");
        resp1.Status.Should().Be("CANCELADA");

        // Chamada repetida em ordem já cancelada não deve republicar evento
        var resp2 = await useCase.ExecuteAsync(ordem.Id, "Desistência do cliente");
        resp2.Status.Should().Be("CANCELADA");

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<ProducaoCanceladaEvent>(e => e.OsId == ordem.OrdemServicoId && e.Motivo == "Desistência do cliente"),
            Arg.Any<CancellationToken>());
    }
}
