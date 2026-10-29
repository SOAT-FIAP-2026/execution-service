using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.ValueObjects;
using Fiap.TechChallenge.Execution.Infrastructure.Database.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fiap.TechChallenge.Execution.IntegrationTests.Database;

[Collection(PostgresCollection.Name)]
public class OrdemProducaoRepositoryIntegrationTests
{
    private readonly PostgresDatabaseFixture _fixture;
    private readonly ClienteSnapshot _clientePadrao = new(Guid.NewGuid(), "Maria Oliveira", "11988887777");
    private readonly VeiculoSnapshot _veiculoPadrao = new("BRA2E19", "Corolla XEi", 2022);

    public OrdemProducaoRepositoryIntegrationTests(PostgresDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migrations_devem_criar_indices_parciais_e_constraints_no_postgresql_16()
    {
        await using var context = _fixture.CreateDbContext();

        var indices = await context.Database
            .SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
            .ToListAsync();

        indices.Should().Contain([
            "uk_ordens_producao_os",
            "uk_diagnostico_ordem_producao",
            "idx_ordens_producao_status",
            "idx_ordens_producao_fila_diag",
            "idx_ordens_producao_fila_prod",
            "idx_tarefas_mecanico_fim",
            "idx_historico_ordem"
        ]);
    }

    [Fact]
    public async Task CicloDeVidaCompleto_deve_persistir_diagnostico_tarefas_e_historico_com_integridade_relacional()
    {
        var osId = Guid.NewGuid();
        var mecanicoId = Guid.NewGuid();
        var servicoCatalogoId = Guid.NewGuid();
        var pecaCatalogoId = Guid.NewGuid();

        // 1. Abertura da Ordem de Produção
        Guid ordemId;
        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var novaOrdem = OrdemProducao.CriarParaDiagnostico(osId, _clientePadrao, _veiculoPadrao, "sp-centro");
            ordemId = novaOrdem.Id;

            await repo.AdicionarAsync(novaOrdem);
            (await repo.ExistePorOrdemServicoIdAsync(osId)).Should().BeTrue();
        }

        // 2. Início e Finalização do Diagnóstico Técnico
        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var ordem = await repo.ObterPorOrdemServicoIdAsync(osId);
            ordem.Should().NotBeNull();

            ordem!.IniciarDiagnostico(mecanicoId, "Carlos Mecânico");
            await repo.AtualizarAsync(ordem);

            ordem.FinalizarDiagnostico(
                mecanicoId,
                "Pastilhas e discos dianteiros desgastados",
                [new ServicoSugerido(servicoCatalogoId, "Substituição de Pastilhas de Freio")],
                [new PecaSugerida(pecaCatalogoId, 2, "Jogo de Pastilhas Cerâmicas")]);

            await repo.AtualizarAsync(ordem);
        }

        // 3. Reabertura de Diagnóstico e Nova Finalização
        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var ordem = await repo.ObterPorIdAsync(ordemId);
            ordem.Should().NotBeNull();

            ordem!.ReabrirDiagnostico("Peça sem estoque imediato");
            await repo.AtualizarAsync(ordem);

            ordem.FinalizarDiagnostico(
                mecanicoId,
                "Laudo revisado com peça equivalente em estoque",
                [new ServicoSugerido(servicoCatalogoId, "Substituição de Pastilhas de Freio")],
                [new PecaSugerida(pecaCatalogoId, 1, "Kit Pastilhas Standard")]);

            await repo.AtualizarAsync(ordem);
        }

        // 4. Liberação para Execução (Fila Operacional)
        Guid tarefaId;
        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var ordem = await repo.ObterPorIdAsync(ordemId);
            var tarefa = new TarefaProducao(servicoCatalogoId, "Substituição de Pastilhas de Freio", 45);
            tarefaId = tarefa.Id;

            ordem!.LiberarParaExecucao([tarefa]);
            await repo.AtualizarAsync(ordem);
        }

        // 5. Início e Finalização da Tarefa de Reparo
        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var ordem = await repo.ObterPorIdAsync(ordemId);

            var inicio = DateTime.UtcNow.AddMinutes(1);
            var fim = inicio.AddMinutes(40);

            ordem!.IniciarTarefa(tarefaId, mecanicoId, "Carlos Mecânico", inicio).Should().BeTrue();
            await repo.AtualizarAsync(ordem);

            ordem.FinalizarTarefa(tarefaId, mecanicoId, "Freios testados na bancada", fim).Should().BeTrue();
            await repo.AtualizarAsync(ordem);
        }

        // 6. Verificação em novo contexto isolado
        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var persistida = await repo.ObterPorIdAsync(ordemId);

            persistida.Should().NotBeNull();
            persistida!.Status.Should().Be(StatusOrdemProducao.Concluida);
            persistida.Versao.Should().BeGreaterThan(1);
            persistida.DataConclusao.Should().NotBeNull();
            persistida.Cliente.Nome.Should().Be("Maria Oliveira");
            persistida.Veiculo.Placa.Should().Be("BRA2E19");

            persistida.Diagnostico.Should().NotBeNull();
            persistida.Diagnostico!.LaudoTecnico.Should().Be("Laudo revisado com peça equivalente em estoque");
            persistida.Diagnostico.ServicosSugeridos.Should().ContainSingle();
            persistida.Diagnostico.PecasSugeridas.Should().ContainSingle(p => p.Quantidade == 1);

            persistida.Tarefas.Should().ContainSingle(t =>
                t.Id == tarefaId &&
                t.Status == StatusTarefaProducao.Concluida &&
                t.ObservacoesTecnicas == "Freios testados na bancada");

            persistida.Historico.Should().HaveCount(8);
            persistida.Historico.Last().Status.Should().Be(StatusOrdemProducao.Concluida);
        }
    }

    [Fact]
    public async Task ObterFilaProducaoAsync_deve_retornar_ordens_na_fila_ordenadas_por_fifo_de_liberacao_e_filtrar_por_filial()
    {
        var filial = $"filial-fifo-{Guid.NewGuid():N}"[..20];
        var baseTime = new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);

        var ordemMaisRecente = CriarOrdemNaFila(filial, baseTime.AddMinutes(30));
        var ordemMaisAntiga = CriarOrdemNaFila(filial, baseTime.AddMinutes(5));
        var ordemOutraFilial = CriarOrdemNaFila("outra-filial", baseTime.AddMinutes(1));

        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            await repo.AdicionarAsync(ordemMaisRecente);
            await repo.AdicionarAsync(ordemMaisAntiga);
            await repo.AdicionarAsync(ordemOutraFilial);
        }

        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var fila = await repo.ObterFilaProducaoAsync(filial);

            fila.Should().HaveCount(2);
            fila[0].Id.Should().Be(ordemMaisAntiga.Id, "a fila deve respeitar a ordem FIFO por DataLiberacao");
            fila[1].Id.Should().Be(ordemMaisRecente.Id);
        }
    }

    [Fact]
    public async Task ObterDiagnosticosPendentesAsync_deve_retornar_ordens_aguardando_diagnostico_ordenadas_por_abertura()
    {
        var filial = $"filial-diag-{Guid.NewGuid():N}"[..20];
        var baseTime = new DateTime(2026, 10, 29, 7, 0, 0, DateTimeKind.Utc);

        var ordemPosterior = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao, filial, baseTime.AddMinutes(20));
        var ordemAnterior = OrdemProducao.CriarParaDiagnostico(Guid.NewGuid(), _clientePadrao, _veiculoPadrao, filial, baseTime.AddMinutes(2));

        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            await repo.AdicionarAsync(ordemPosterior);
            await repo.AdicionarAsync(ordemAnterior);
        }

        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var pendentes = await repo.ObterDiagnosticosPendentesAsync(filial);

            pendentes.Should().HaveCount(2);
            pendentes[0].Id.Should().Be(ordemAnterior.Id);
            pendentes[1].Id.Should().Be(ordemPosterior.Id);
        }
    }

    [Fact]
    public async Task AtualizarAsync_concorrente_em_tarefa_deve_lancar_ConflitoConcorrenciaException_via_token_versao()
    {
        var ordemSetup = CriarOrdemNaFila("sp-concorrencia", DateTime.UtcNow);
        var ordemId = ordemSetup.Id;
        var tarefaId = ordemSetup.Tarefas.First().Id;

        await using (var ctxSetup = _fixture.CreateDbContext())
        {
            var repoSetup = new OrdemProducaoRepository(ctxSetup);
            await repoSetup.AdicionarAsync(ordemSetup);
        }

        await using var ctxMecanico1 = _fixture.CreateDbContext();
        await using var ctxMecanico2 = _fixture.CreateDbContext();

        var repo1 = new OrdemProducaoRepository(ctxMecanico1);
        var repo2 = new OrdemProducaoRepository(ctxMecanico2);

        // Ambos os mecânicos leem a mesma versão da OrdemProducao na fila
        var ordemMec1 = await repo1.ObterPorIdAsync(ordemId);
        var ordemMec2 = await repo2.ObterPorIdAsync(ordemId);

        ordemMec1!.IniciarTarefa(tarefaId, Guid.NewGuid(), "Mecânico 1");
        await repo1.AtualizarAsync(ordemMec1);

        ordemMec2!.IniciarTarefa(tarefaId, Guid.NewGuid(), "Mecânico 2");
        var actConcorrente = async () => await repo2.AtualizarAsync(ordemMec2);

        await actConcorrente.Should().ThrowAsync<ConflitoConcorrenciaException>()
            .WithMessage($"*{ordemId}*");
    }

    [Fact]
    public async Task Cancelar_ordem_em_execucao_deve_persistir_tarefas_interrompidas_e_status_cancelada()
    {
        var ordem = CriarOrdemNaFila("sp-sul", DateTime.UtcNow);
        var tarefaId = ordem.Tarefas.First().Id;

        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            await repo.AdicionarAsync(ordem);
        }

        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var carregada = await repo.ObterPorIdAsync(ordem.Id);

            carregada!.IniciarTarefa(tarefaId, Guid.NewGuid(), "Mecânico");
            await repo.AtualizarAsync(carregada);

            carregada.Cancelar("Cancelamento solicitado por reprovação financeira");
            await repo.AtualizarAsync(carregada);
        }

        await using (var ctx = _fixture.CreateDbContext())
        {
            var repo = new OrdemProducaoRepository(ctx);
            var cancelada = await repo.ObterPorIdAsync(ordem.Id);

            cancelada!.Status.Should().Be(StatusOrdemProducao.Cancelada);
            cancelada.Tarefas.Should().OnlyContain(t => t.Status == StatusTarefaProducao.Interrompida);
        }
    }

    private OrdemProducao CriarOrdemNaFila(string filialId, DateTime dataLiberacao)
    {
        var ordem = OrdemProducao.CriarParaDiagnostico(
            Guid.NewGuid(),
            _clientePadrao,
            _veiculoPadrao,
            filialId,
            dataLiberacao.AddHours(-1));

        var mecId = Guid.NewGuid();
        ordem.IniciarDiagnostico(mecId, "Carlos", dataLiberacao.AddMinutes(-45));
        ordem.FinalizarDiagnostico(mecId, "Diagnóstico aprovado", dataHora: dataLiberacao.AddMinutes(-15));
        ordem.LiberarParaExecucao(
            [new TarefaProducao(Guid.NewGuid(), "Revisão de Freios", 40)],
            dataLiberacao);

        return ordem;
    }
}
