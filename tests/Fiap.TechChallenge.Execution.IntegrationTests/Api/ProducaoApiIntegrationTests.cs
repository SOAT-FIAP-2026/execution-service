using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fiap.TechChallenge.Execution.Api.Configurations;
using Fiap.TechChallenge.Execution.Application.DTOs;
using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using Fiap.TechChallenge.Execution.Infrastructure.Messaging;
using Fiap.TechChallenge.Execution.IntegrationTests.Database;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Fiap.TechChallenge.Execution.IntegrationTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class ProducaoApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtSecret = "test-jwt-secret-key-at-least-32-bytes-long-123456!";
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public ProducaoApiIntegrationTests(PostgresDatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
    {
        _dbFixture = dbFixture;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ExecutionDb"] = _dbFixture.ConnectionString,
                    ["Jwt:Secret"] = TestJwtSecret,
                    ["Jwt:Issuer"] = "fiap-tech-challenge",
                    ["Jwt:Audience"] = "fiap-api"
                });
            });
        });
    }

    [Fact]
    public async Task Endpoints_SemTokenJwt_DevemRetornar401Unauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/producao/fila");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_ComRoleSemPermissao_DeveRetornar403Forbidden()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid(), "Operador Balcão", "Operador");

        // Operador não pode acessar /diagnosticos/pendentes (apenas Mecanico ou Administrador)
        var response = await client.GetAsync("/api/v1/producao/diagnosticos/pendentes");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FluxoDiagnosticoEProducao_ViaHttpApi_DeveExecutarCicloCompletoComRastreabilidade()
    {
        // 1. Prepara uma OrdemProducao aguardando diagnóstico no PostgreSQL 16
        var ordem = OrdemProducao.CriarParaDiagnostico(
            Guid.NewGuid(),
            new ClienteSnapshot(Guid.NewGuid(), "Roberto Carlos", "11977776666"),
            new VeiculoSnapshot("MER1C23", "Volkswagen Golf", 2021),
            "FILIAL-API-E2E");

        await using (var db = _dbFixture.CreateDbContext())
        {
            db.OrdensProducao.Add(ordem);
            await db.SaveChangesAsync();
        }

        var idMecanico = Guid.NewGuid();
        using var mecanicoClient = CreateAuthenticatedClient(idMecanico, "Carlos Mecânico", "Mecanico");
        mecanicoClient.DefaultRequestHeaders.Add("X-Correlation-ID", "corr-e2e-001");

        // 2. Consulta diagnósticos pendentes na filial
        var pendentesResp = await mecanicoClient.GetAsync("/api/v1/producao/diagnosticos/pendentes?filialId=FILIAL-API-E2E");
        pendentesResp.StatusCode.Should().Be(HttpStatusCode.OK);
        pendentesResp.Headers.GetValues("X-Correlation-ID").Should().Contain("corr-e2e-001");

        var pendentes = await pendentesResp.Content.ReadFromJsonAsync<List<OrdemProducaoFilaItemResponse>>();
        pendentes.Should().NotBeNull();
        pendentes!.Should().Contain(i => i.Id == ordem.Id);

        // 3. Inicia o diagnóstico
        var iniciarDiagResp = await mecanicoClient.PatchAsync($"/api/v1/producao/{ordem.Id}/diagnostico/iniciar", null);
        iniciarDiagResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var aposInicioDiag = await iniciarDiagResp.Content.ReadFromJsonAsync<OrdemProducaoResponse>();
        aposInicioDiag!.Status.Should().Be("EM_DIAGNOSTICO");

        // 4. Outro mecânico tenta finalizar o diagnóstico -> deve retornar 403 Forbidden (ProblemDetails)
        using var outroMecanicoClient = CreateAuthenticatedClient(Guid.NewGuid(), "Outro Mecânico", "Mecanico");
        var finalizarReq = new FinalizarDiagnosticoRequest(
            "Troca de amortecedores dianteiros",
            [new ServicoSugeridoRequest(Guid.NewGuid(), "Substituição de amortecedores")],
            [new PecaSugeridaRequest(Guid.NewGuid(), 1, "Par de amortecedores")]);

        var forbiddenResp = await outroMecanicoClient.PostAsJsonAsync(
            $"/api/v1/producao/{ordem.Id}/diagnostico/finalizar",
            finalizarReq);
        forbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. O mecânico responsável finaliza o diagnóstico -> 200 OK
        var finalizarDiagResp = await mecanicoClient.PostAsJsonAsync(
            $"/api/v1/producao/{ordem.Id}/diagnostico/finalizar",
            finalizarReq);
        finalizarDiagResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var aposFimDiag = await finalizarDiagResp.Content.ReadFromJsonAsync<OrdemProducaoResponse>();
        aposFimDiag!.Status.Should().Be("DIAGNOSTICO_CONCLUIDO");

        // 6. Simula a liberação para execução (que virá do evento PagamentoConfirmadoEvent na Etapa 5)
        Guid tarefa1Id;
        Guid tarefa2Id;
        await using (var db = _dbFixture.CreateDbContext())
        {
            var repo = new Fiap.TechChallenge.Execution.Infrastructure.Database.Repositories.OrdemProducaoRepository(db);
            var ordemDb = await repo.ObterPorIdAsync(ordem.Id);
            var t1 = new TarefaProducao(Guid.NewGuid(), "Desmontagem suspensão", 45);
            var t2 = new TarefaProducao(Guid.NewGuid(), "Montagem e alinhamento", 45);
            tarefa1Id = t1.Id;
            tarefa2Id = t2.Id;
            ordemDb!.LiberarParaExecucao([t1, t2]);
            await repo.AtualizarAsync(ordemDb);
        }

        // 7. Consulta a fila de produção como Operador
        using var operadorClient = CreateAuthenticatedClient(Guid.NewGuid(), "Fernanda Operadora", "Operador");
        var filaResp = await operadorClient.GetAsync("/api/v1/producao/fila?filialId=FILIAL-API-E2E&status=NA_FILA");
        filaResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fila = await filaResp.Content.ReadFromJsonAsync<List<OrdemProducaoFilaItemResponse>>();
        fila!.Should().Contain(i => i.Id == ordem.Id);

        // 8. Mecânico inicia e finaliza as 2 tarefas
        var iniT1 = await mecanicoClient.PatchAsync($"/api/v1/producao/{ordem.Id}/tarefas/{tarefa1Id}/iniciar", null);
        iniT1.StatusCode.Should().Be(HttpStatusCode.OK);

        var iniT2 = await mecanicoClient.PatchAsync($"/api/v1/producao/{ordem.Id}/tarefas/{tarefa2Id}/iniciar", null);
        iniT2.StatusCode.Should().Be(HttpStatusCode.OK);

        var fimT1 = await mecanicoClient.PatchAsJsonAsync(
            $"/api/v1/producao/{ordem.Id}/tarefas/{tarefa1Id}/finalizar",
            new FinalizarTarefaRequest("Desmontagem sem intercorrências"));
        fimT1.StatusCode.Should().Be(HttpStatusCode.OK);
        var estadoParcial = await fimT1.Content.ReadFromJsonAsync<OrdemProducaoResponse>();
        estadoParcial!.Status.Should().Be("EM_EXECUCAO");

        var fimT2 = await mecanicoClient.PatchAsJsonAsync(
            $"/api/v1/producao/{ordem.Id}/tarefas/{tarefa2Id}/finalizar",
            new FinalizarTarefaRequest("Alinhamento conferido"));
        fimT2.StatusCode.Should().Be(HttpStatusCode.OK);
        var estadoFinal = await fimT2.Content.ReadFromJsonAsync<OrdemProducaoResponse>();
        estadoFinal!.Status.Should().Be("CONCLUIDA");

        // 9. Consulta detalhes por ID da Ordem de Serviço
        var porOsResp = await operadorClient.GetAsync($"/api/v1/producao/ordem-servico/{ordem.OrdemServicoId}");
        porOsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 10. Administrador consulta métricas de produtividade do mecânico
        using var adminClient = CreateAuthenticatedClient(Guid.NewGuid(), "Diretor Oficina", "Administrador");
        var metricasResp = await adminClient.GetAsync($"/api/v1/producao/metricas/produtividade?idMecanico={idMecanico}");
        metricasResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var metricas = await metricasResp.Content.ReadFromJsonAsync<RelatorioProdutividadeResponse>();
        metricas.Should().NotBeNull();
        metricas!.TotalTarefasConcluidas.Should().Be(2);
        metricas.Mecanicos.Should().ContainSingle(m => m.IdMecanico == idMecanico && m.TarefasConcluidas == 2);

        // 11. Verifica que os eventos de integração foram publicados no InMemoryEventPublisher
        var publisher = _factory.Services.GetRequiredService<InMemoryEventPublisher>();
        publisher.PublishedEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CancelarProducao_PorAdministrador_DeveCancelarOrdemEmAndamento()
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(
            Guid.NewGuid(),
            new ClienteSnapshot(Guid.NewGuid(), "Cliente Cancelamento", "11966665555"),
            new VeiculoSnapshot("CAN9Z99", "Fiat Pulse", 2024),
            "FILIAL-CANCEL");

        await using (var db = _dbFixture.CreateDbContext())
        {
            db.OrdensProducao.Add(ordem);
            await db.SaveChangesAsync();
        }

        using var adminClient = CreateAuthenticatedClient(Guid.NewGuid(), "Admin Oficina", "Administrador");
        var resp = await adminClient.PostAsJsonAsync(
            $"/api/v1/producao/{ordem.Id}/cancelar",
            new CancelarProducaoRequest("Solicitação direta do cliente"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<OrdemProducaoResponse>();
        body!.Status.Should().Be("CANCELADA");
    }

    private HttpClient CreateAuthenticatedClient(Guid usuarioId, string nome, string role)
    {
        var client = _factory.CreateClient();
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(TestJwtSecret);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, nome),
                new Claim(ClaimTypes.Email, $"{usuarioId}@oficina.com"),
                new Claim("UsuarioId", usuarioId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "fiap-tech-challenge",
            Audience = "fiap-api",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(descriptor);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenHandler.WriteToken(token));

        return client;
    }
}
