using System.Security.Claims;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Fiap.TechChallenge.Contracts.AuthService;
using Fiap.TechChallenge.Contracts.Common;
using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Contracts.ExecutionService;
using Fiap.TechChallenge.Execution.Api.Configurations;
using Fiap.TechChallenge.Execution.Api.Controllers.V1;
using Fiap.TechChallenge.Execution.Api.Middlewares;
using Fiap.TechChallenge.Execution.Application;
using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Application.Interfaces;
using Fiap.TechChallenge.Execution.Application.Queries;
using Fiap.TechChallenge.Execution.Application.UseCases;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using Fiap.TechChallenge.Execution.Infrastructure;
using Fiap.TechChallenge.Execution.Infrastructure.Database;
using Fiap.TechChallenge.Execution.Infrastructure.Database.Repositories;
using Fiap.TechChallenge.Execution.Infrastructure.Messaging;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Fiap.TechChallenge.Execution.UnitTests.Infrastructure;

public class ExecutionInfrastructureAndApiUnitTests
{
    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task OrdemProducaoRepository_DevePersistirAtualizarEConsultarTarefasComEfCore()
    {
        await using var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var repo = new OrdemProducaoRepository(db);

        var osId = Guid.NewGuid();
        var idMecanico = Guid.NewGuid();
        var cliente = new ClienteSnapshot(Guid.NewGuid(), "Carlos", "11999999999");
        var veiculo = new VeiculoSnapshot("ABC1D23", "Civic", 2024);

        var ordem = OrdemProducao.CriarParaDiagnostico(osId, cliente, veiculo, "MATRIZ", DateTime.UtcNow.AddHours(-2));
        await repo.AdicionarAsync(ordem);

        (await repo.ExistePorOrdemServicoIdAsync(osId)).Should().BeTrue();

        ordem.IniciarDiagnostico(idMecanico, "Mec 1", DateTime.UtcNow.AddMinutes(-90));
        ordem.FinalizarDiagnostico(
            idMecanico,
            "Troca de amortecedor",
            [new ServicoSugerido(Guid.NewGuid(), "Troca")],
            [new PecaSugerida(Guid.NewGuid(), 2, "Amortecedor")],
            DateTime.UtcNow.AddMinutes(-60));

        var tarefa = new TarefaProducao(Guid.NewGuid(), "Executar troca", 45);
        ordem.LiberarParaExecucao([tarefa], DateTime.UtcNow.AddMinutes(-50));
        await repo.AtualizarAsync(ordem);

        ordem.IniciarTarefa(tarefa.Id, idMecanico, "Mec 1", DateTime.UtcNow.AddMinutes(-40));
        ordem.FinalizarTarefa(tarefa.Id, idMecanico, "OK", DateTime.UtcNow.AddMinutes(-10));
        await repo.AtualizarAsync(ordem);

        // Testa branch Detached do AtualizarAsync
        db.Entry(ordem).State = EntityState.Detached;
        await repo.AtualizarAsync(ordem);

        var concluidas = await repo.ObterTarefasConcluidasPorPeriodoAsync(
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(1),
            idMecanico);
        concluidas.Should().ContainSingle();
    }

    [Fact]
    public async Task ProducaoController_DeveExecutarTodosEndpointsEValidarClaimsJwt()
    {
        var repo = new FakeOrdemProducaoRepository();
        var publisher = new InMemoryEventPublisher();

        var osId = Guid.NewGuid();
        var idMecanico = Guid.NewGuid();
        var ordem = OrdemProducao.CriarParaDiagnostico(
            osId,
            new ClienteSnapshot(Guid.NewGuid(), "Cliente", "11988887777"),
            new VeiculoSnapshot("XYZ9K88", "Corolla", 2023),
            "MATRIZ",
            DateTime.UtcNow.AddMinutes(-30));
        await repo.AdicionarAsync(ordem);

        var controller = new ProducaoController(
            new ObterFilaProducaoQuery(repo),
            new ObterDiagnosticosPendentesQuery(repo),
            new ObterDetalhesOrdemProducaoQuery(repo),
            new ObterMetricasProdutividadeQuery(repo),
            new IniciarDiagnosticoUseCase(repo, publisher),
            new FinalizarDiagnosticoUseCase(repo, publisher),
            new IniciarTarefaUseCase(repo, publisher),
            new FinalizarTarefaUseCase(repo, publisher),
            new CancelarProducaoUseCase(repo, publisher));

        // Sem claim -> lança UnauthorizedAccessException
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.IniciarDiagnostico(ordem.Id, CancellationToken.None));

        // Com claims válidas de mecânico
        var identity = new ClaimsIdentity(
        [
            new Claim(JwtAuthConstants.Claims.UsuarioId, idMecanico.ToString()),
            new Claim(ClaimTypes.Name, "Mecanico Senior")
        ], "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        (await controller.ObterDiagnosticosPendentes("MATRIZ", CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.ObterPorId(ordem.Id, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.ObterPorOrdemServicoId(osId, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();

        (await controller.IniciarDiagnostico(ordem.Id, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();

        var finDiagReq = new FinalizarDiagnosticoRequest(
            "Motor revisado",
            [new ServicoSugeridoRequest(Guid.NewGuid(), "Revisao")],
            [new PecaSugeridaRequest(Guid.NewGuid(), 1, "Vela")]);
        (await controller.FinalizarDiagnostico(ordem.Id, finDiagReq, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();

        // Libera para fila para testar tarefas
        var atualizada = await repo.ObterPorIdAsync(ordem.Id);
        var tarefa = new TarefaProducao(Guid.NewGuid(), "Revisao", 30);
        atualizada!.LiberarParaExecucao([tarefa], DateTime.UtcNow.AddMinutes(-15));
        await repo.AtualizarAsync(atualizada);

        (await controller.ObterFila("NaFila", "MATRIZ", CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.IniciarTarefa(ordem.Id, tarefa.Id, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();

        var okFin = (await controller.FinalizarTarefa(ordem.Id, tarefa.Id, new FinalizarTarefaRequest("Concluido"), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>().Subject;
        var respDto = okFin.Value.Should().BeOfType<OrdemProducaoResponse>().Subject;
        respDto.Id.Should().Be(ordem.Id);
        respDto.OrdemServicoId.Should().Be(osId);
        respDto.Cliente.Nome.Should().Be("Cliente");
        respDto.Veiculo.Placa.Should().Be("XYZ9K88");
        respDto.Diagnostico.Should().NotBeNull();
        respDto.Diagnostico!.ServicosSugeridos.Should().ContainSingle();
        respDto.Diagnostico.PecasSugeridas.Should().ContainSingle();
        respDto.Tarefas.Should().ContainSingle();
        respDto.Historico.Should().NotBeEmpty();

        var okMet = (await controller.ObterMetricasProdutividade(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), idMecanico, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>().Subject;
        var relatorio = okMet.Value.Should().BeOfType<RelatorioProdutividadeResponse>().Subject;
        relatorio.TotalTarefasConcluidas.Should().Be(1);

        // Nova ordem para testar cancelamento
        var ordemCancel = OrdemProducao.CriarParaDiagnostico(
            Guid.NewGuid(),
            new ClienteSnapshot(Guid.NewGuid(), "Cli2", null),
            new VeiculoSnapshot("AAA1A11", "Gol", 2021),
            "MATRIZ");
        await repo.AdicionarAsync(ordemCancel);
        (await controller.CancelarProducao(ordemCancel.Id, new CancelarProducaoRequest("Cliente desistiu"), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();

        publisher.PublishedEvents.Should().NotBeEmpty();
        publisher.Clear();
        publisher.PublishedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_DeveMapearTodasExcecoesParaProblemDetails()
    {
        var exceptionsAndStatusCodes = new (Exception Ex, int ExpectedStatus)[]
        {
            (new OrdemProducaoNaoEncontradaException(Guid.NewGuid()), StatusCodes.Status404NotFound),
            (new TarefaProducaoNaoEncontradaException(Guid.NewGuid()), StatusCodes.Status404NotFound),
            (new TransicaoEstadoInvalidaException("Invalido"), StatusCodes.Status409Conflict),
            (new ConflitoConcorrenciaException(Guid.NewGuid()), StatusCodes.Status409Conflict),
            (new MecanicoNaoAutorizadoException("Nao autorizado"), StatusCodes.Status403Forbidden),
            (new ArgumentException("Arg invalido"), StatusCodes.Status400BadRequest),
            (new UnauthorizedAccessException("Nao autenticado"), StatusCodes.Status401Unauthorized),
            (new InvalidOperationException("Falha inesperada"), StatusCodes.Status500InternalServerError)
        };

        foreach (var (ex, expectedStatus) in exceptionsAndStatusCodes)
        {
            var middleware = new ExceptionHandlingMiddleware(
                _ => throw ex,
                NullLogger<ExceptionHandlingMiddleware>.Instance);

            var ctx = new DefaultHttpContext();
            ctx.Items[CorrelationIdMiddleware.HeaderName] = "corr-err-1";
            ctx.Response.Body = new MemoryStream();

            await middleware.InvokeAsync(ctx);
            ctx.Response.StatusCode.Should().Be(expectedStatus);
        }
    }

    [Fact]
    public async Task Configurations_Jwt_Swagger_Observability_DependencyInjection_E_MassTransitPublisher_DevemFuncionar()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_SERVICE_NAME"] = "execution-unit-test",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddJwtConfiguration(config);
        services.AddSwaggerConfiguration();
        services.AddObservabilityConfiguration(config);
        services.AddExecutionApplication();
        services.AddExecutionInfrastructure(config);

        using var sp = services.BuildServiceProvider();
        var jwtMonitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var bearerOpts = jwtMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
        bearerOpts.TokenValidationParameters.ValidIssuer.Should().Be("TechChallenge");

        var swaggerOpts = sp.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        swaggerOpts.SwaggerGeneratorOptions.SwaggerDocs.Should().ContainKey("v1");

        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var mtPublisher = new MassTransitEventPublisher(publishEndpoint, NullLogger<MassTransitEventPublisher>.Instance);
        var evt = ProducaoIniciadaEvent.Create(Guid.NewGuid(), DateTime.UtcNow);
        await mtPublisher.PublishAsync(evt);
        await publishEndpoint.Received(1).Publish(evt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecutionSqsBackgroundWorker_DeveProcessarLiberacao_Rejeicao_Cancelamento_E_Envelopes()
    {
        var repo = new FakeOrdemProducaoRepository();

        var osLib = Guid.NewGuid();
        var osRej = Guid.NewGuid();
        var osCan = Guid.NewGuid();
        var idMec = Guid.NewGuid();

        // Ordem 1: AguardandoAprovacao -> será liberada para execução
        var ordemLib = OrdemProducao.CriarParaDiagnostico(
            osLib,
            new ClienteSnapshot(Guid.NewGuid(), "C1", null),
            new VeiculoSnapshot("BBB2B22", "Uno", 2020));
        ordemLib.IniciarDiagnostico(idMec, "Mec", DateTime.UtcNow.AddMinutes(-20));
        ordemLib.FinalizarDiagnostico(
            idMec,
            "Laudo 1",
            [new ServicoSugerido(Guid.NewGuid(), "S1")],
            [],
            DateTime.UtcNow.AddMinutes(-10));
        await repo.AdicionarAsync(ordemLib);

        // Ordem 2: AguardandoAprovacao -> diagnóstico rejeitado
        var ordemRej = OrdemProducao.CriarParaDiagnostico(
            osRej,
            new ClienteSnapshot(Guid.NewGuid(), "C2", null),
            new VeiculoSnapshot("CCC3C33", "Palio", 2021));
        ordemRej.IniciarDiagnostico(idMec, "Mec", DateTime.UtcNow.AddMinutes(-20));
        ordemRej.FinalizarDiagnostico(
            idMec,
            "Laudo 2",
            [new ServicoSugerido(Guid.NewGuid(), "S2")],
            [],
            DateTime.UtcNow.AddMinutes(-10));
        await repo.AdicionarAsync(ordemRej);

        // Ordem 3: AguardandoDiagnostico -> será cancelada
        var ordemCan = OrdemProducao.CriarParaDiagnostico(
            osCan,
            new ClienteSnapshot(Guid.NewGuid(), "C3", null),
            new VeiculoSnapshot("DDD4D44", "Fox", 2022));
        await repo.AdicionarAsync(ordemCan);

        var services = new ServiceCollection();
        services.AddScoped<IOrdemProducaoRepository>(_ => repo);
        var sp = services.BuildServiceProvider();

        var libEvt = new OrdemServicoLiberadaParaExecucaoEvent
        {
            OsId = osLib,
            Tarefas = [new TarefaInfo(Guid.NewGuid(), "Troca", 40)]
        };
        var rejEvt = DiagnosticoRejeitadoEvent.Create(osRej, "Falta estoque");
        var canEvt = new OrdemServicoCanceladaEvent { OsId = osCan, Motivo = "Cancelado pelo cliente" };

        var sqsMessages = new List<Message>
        {
            new()
            {
                ReceiptHandle = "rh-lib",
                Body = JsonSerializer.Serialize(libEvt),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { StringValue = nameof(OrdemServicoLiberadaParaExecucaoEvent) }
                }
            },
            new()
            {
                ReceiptHandle = "rh-rej",
                Body = JsonSerializer.Serialize(new { message = rejEvt }),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { StringValue = nameof(DiagnosticoRejeitadoEvent) }
                }
            },
            new()
            {
                ReceiptHandle = "rh-can",
                Body = JsonSerializer.Serialize(canEvt),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { StringValue = nameof(OrdemServicoCanceladaEvent) }
                }
            },
            new()
            {
                ReceiptHandle = "rh-ign",
                Body = "{\"tipoEvento\":\"Ignorado\"}"
            }
        };

        using var cts = new CancellationTokenSource();
        var sqs = Substitute.For<IAmazonSQS>();
        int calls = 0;
        sqs.ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                if (calls == 1)
                    return Task.FromResult(new ReceiveMessageResponse { Messages = sqsMessages });
                cts.Cancel();
                return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
            });
        sqs.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var worker = new ExecutionSqsBackgroundWorker(
            sqs,
            new ConfigurationBuilder().Build(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionSqsBackgroundWorker>.Instance);

        await worker.StartAsync(cts.Token);
        await Task.Delay(150);
        await worker.StopAsync(CancellationToken.None);

        (await repo.ObterPorOrdemServicoIdAsync(osLib))!.Status.Should().Be(StatusOrdemProducao.NaFila);
        (await repo.ObterPorOrdemServicoIdAsync(osRej))!.Status.Should().Be(StatusOrdemProducao.EmDiagnostico);
        (await repo.ObterPorOrdemServicoIdAsync(osCan))!.Status.Should().Be(StatusOrdemProducao.Cancelada);
    }

    private sealed class FakeOrdemProducaoRepository : IOrdemProducaoRepository
    {
        private readonly List<OrdemProducao> _ordens = [];

        public Task<OrdemProducao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_ordens.FirstOrDefault(o => o.Id == id));

        public Task<OrdemProducao?> ObterPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_ordens.FirstOrDefault(o => o.OrdemServicoId == ordemServicoId));

        public Task<bool> ExistePorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_ordens.Any(o => o.OrdemServicoId == ordemServicoId));

        public Task<IReadOnlyList<OrdemProducao>> ObterFilaProducaoAsync(
            string? filialId = null,
            StatusOrdemProducao? status = null,
            CancellationToken cancellationToken = default)
        {
            var s = status ?? StatusOrdemProducao.NaFila;
            IReadOnlyList<OrdemProducao> list = _ordens.Where(o => o.Status == s).ToList();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<OrdemProducao>> ObterDiagnosticosPendentesAsync(
            string? filialId = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<OrdemProducao> list = _ordens.Where(o => o.Status == StatusOrdemProducao.AguardandoDiagnostico).ToList();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<TarefaProducao>> ObterTarefasConcluidasPorPeriodoAsync(
            DateTime? de = null,
            DateTime? ate = null,
            Guid? idMecanico = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TarefaProducao> list = _ordens
                .SelectMany(o => o.Tarefas)
                .Where(t => t.Status == StatusTarefaProducao.Concluida)
                .ToList();
            return Task.FromResult(list);
        }

        public Task AdicionarAsync(OrdemProducao ordem, CancellationToken cancellationToken = default)
        {
            _ordens.Add(ordem);
            return Task.CompletedTask;
        }

        public Task AtualizarAsync(OrdemProducao ordem, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
