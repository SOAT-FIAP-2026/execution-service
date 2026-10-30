using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Fiap.TechChallenge.Execution.Infrastructure.Database;

/// <summary>
/// Contexto relacional do Entity Framework Core para o banco PostgreSQL 16 dedicado (techchallenge_execution_db).
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<OrdemProducao> OrdensProducao => Set<OrdemProducao>();
    public DbSet<Diagnostico> Diagnosticos => Set<Diagnostico>();
    public DbSet<ServicoSugerido> DiagnosticoServicosSugeridos => Set<ServicoSugerido>();
    public DbSet<PecaSugerida> DiagnosticoPecasSugeridas => Set<PecaSugerida>();
    public DbSet<TarefaProducao> TarefasProducao => Set<TarefaProducao>();
    public DbSet<HistoricoProducao> HistoricoOrdensProducao => Set<HistoricoProducao>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração de tabelas do Transactional Outbox & Inbox do MassTransit
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

