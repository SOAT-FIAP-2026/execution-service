using Fiap.TechChallenge.Contracts.Events;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.IntegrationTests.Database;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.RabbitMq;
using Xunit;

namespace Fiap.TechChallenge.Execution.IntegrationTests.Messaging;

[Collection(PostgresCollection.Name)]
public sealed class SagaMessagingIntegrationTests : IAsyncLifetime
{
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public SagaMessagingIntegrationTests(PostgresDatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ExecutionDb"] = _dbFixture.ConnectionString,
                    ["ConnectionStrings:RabbitMq"] = _rabbitMqContainer.GetConnectionString(),
                    ["Jwt:Secret"] = "test-secret-at-least-32-chars-long-12345678!",
                    ["Jwt:Issuer"] = "fiap-tech-challenge",
                    ["Jwt:Audience"] = "fiap-api"
                });
            });
        });

        // Força a inicialização do bus do MassTransit
        _ = _factory.Services.GetRequiredService<IBusControl>();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _rabbitMqContainer.DisposeAsync();
    }

    [Fact]
    public async Task CicloCompletoMensageria_ComRabbitMQRealETargetPostgres_DeveConsumirEventosEAtualizarAgregado()
    {
        var bus = _factory!.Services.GetRequiredService<IBus>();
        var osId = Guid.NewGuid();

        // 1. OS Service publica OrdemServicoAbertaEvent no RabbitMQ
        var aberturaEvent = OrdemServicoAbertaEvent.Create(
            osId,
            new ClienteContrato(Guid.NewGuid(), "Juliana Mendes", "11998877665"),
            new VeiculoContrato("BRA9S88", "Toyota Yaris", 2023),
            "FILIAL-RABBIT-01");

        await bus.Publish(aberturaEvent);

        // Aguarda processamento assíncrono pelo OrdemServicoAbertaConsumer
        OrdemProducao? ordemCriada = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(300);
            await using var db = _dbFixture.CreateDbContext();
            ordemCriada = await db.OrdensProducao
                .Include(o => o.Historico)
                .FirstOrDefaultAsync(o => o.OrdemServicoId == osId);

            if (ordemCriada != null)
                break;
        }

        ordemCriada.Should().NotBeNull();
        ordemCriada!.Status.Should().Be(StatusOrdemProducao.AguardandoDiagnostico);
        ordemCriada.FilialId.Should().Be("FILIAL-RABBIT-01");
        ordemCriada.Cliente.Nome.Should().Be("Juliana Mendes");
        ordemCriada.Veiculo.Placa.Should().Be("BRA9S88");

        // 2. Simula mecânico iniciando e finalizando o diagnóstico
        var idMecanico = Guid.NewGuid();
        await using (var db = _dbFixture.CreateDbContext())
        {
            var ordemDb = await db.OrdensProducao.Include(o => o.Diagnostico).FirstAsync(o => o.OrdemServicoId == osId);
            ordemDb.IniciarDiagnostico(idMecanico, "Marcos Mecânico");
            ordemDb.FinalizarDiagnostico(idMecanico, "Troca de amortecedores");
            await db.SaveChangesAsync();
        }

        // 3. OS Service publica DiagnosticoRejeitadoEvent -> deve retornar a EM_DIAGNOSTICO
        var rejeicaoEvent = DiagnosticoRejeitadoEvent.Create(osId, "Inconsistência de estoque");
        await bus.Publish(rejeicaoEvent);

        OrdemProducao? ordemReaberta = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(300);
            await using var db = _dbFixture.CreateDbContext();
            ordemReaberta = await db.OrdensProducao.FirstOrDefaultAsync(o => o.OrdemServicoId == osId);
            if (ordemReaberta?.Status == StatusOrdemProducao.EmDiagnostico)
                break;
        }

        ordemReaberta!.Status.Should().Be(StatusOrdemProducao.EmDiagnostico);

        // 4. Mecânico refaz e conclui o diagnóstico
        await using (var db = _dbFixture.CreateDbContext())
        {
            var ordemDb = await db.OrdensProducao.Include(o => o.Diagnostico).FirstAsync(o => o.OrdemServicoId == osId);
            ordemDb.FinalizarDiagnostico(idMecanico, "Laudo refeito");
            await db.SaveChangesAsync();
        }

        // 5. Cliente paga -> OS Service publica OrdemServicoLiberadaParaExecucaoEvent
        var liberacaoEvent = OrdemServicoLiberadaParaExecucaoEvent.Create(
            osId,
            [
                new TarefaItemContrato(Guid.NewGuid(), "Substituição de amortecedores", 60),
                new TarefaItemContrato(Guid.NewGuid(), "Geometria e alinhamento", 30)
            ]);

        await bus.Publish(liberacaoEvent);

        OrdemProducao? ordemLiberada = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(300);
            await using var db = _dbFixture.CreateDbContext();
            ordemLiberada = await db.OrdensProducao
                .Include(o => o.Tarefas)
                .FirstOrDefaultAsync(o => o.OrdemServicoId == osId);

            if (ordemLiberada?.Status == StatusOrdemProducao.NaFila && ordemLiberada.Tarefas.Count == 2)
                break;
        }

        ordemLiberada!.Status.Should().Be(StatusOrdemProducao.NaFila);
        ordemLiberada.Tarefas.Should().HaveCount(2);

        // 6. Compensação A: OS cancelada externamente -> publica OrdemServicoCanceladaEvent
        var cancelamentoEvent = OrdemServicoCanceladaEvent.Create(osId, "Cliente solicitou cancelamento do serviço");
        await bus.Publish(cancelamentoEvent);

        OrdemProducao? ordemCancelada = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(300);
            await using var db = _dbFixture.CreateDbContext();
            ordemCancelada = await db.OrdensProducao
                .Include(o => o.Tarefas)
                .FirstOrDefaultAsync(o => o.OrdemServicoId == osId);

            if (ordemCancelada?.Status == StatusOrdemProducao.Cancelada)
                break;
        }

        ordemCancelada!.Status.Should().Be(StatusOrdemProducao.Cancelada);
        ordemCancelada.Tarefas.All(t => t.Status == StatusTarefaProducao.Interrompida).Should().BeTrue();
    }
}
