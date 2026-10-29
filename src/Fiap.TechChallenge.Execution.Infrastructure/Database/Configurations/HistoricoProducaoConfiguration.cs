using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiap.TechChallenge.Execution.Infrastructure.Database.Configurations;

public class HistoricoProducaoConfiguration : IEntityTypeConfiguration<HistoricoProducao>
{
    public void Configure(EntityTypeBuilder<HistoricoProducao> builder)
    {
        builder.ToTable("historico_ordens_producao");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(h => h.OrdemProducaoId)
            .HasColumnName("ordem_producao_id")
            .IsRequired();

        builder.Property(h => h.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.ToUpperSnake(),
                v => StatusOrdemProducaoExtensions.FromUpperSnake(v))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(h => h.Descricao)
            .HasColumnName("descricao")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(h => h.DataHora)
            .HasColumnName("data_hora")
            .IsRequired();

        builder.Property(h => h.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.Property(h => h.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .IsRequired();

        // Índice para busca cronológica do histórico da ordem (Seção 5.3)
        builder.HasIndex(h => new { h.OrdemProducaoId, h.DataHora })
            .HasDatabaseName("idx_historico_ordem");
    }
}

