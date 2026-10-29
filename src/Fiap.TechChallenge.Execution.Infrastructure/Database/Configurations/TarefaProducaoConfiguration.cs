using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiap.TechChallenge.Execution.Infrastructure.Database.Configurations;

public class TarefaProducaoConfiguration : IEntityTypeConfiguration<TarefaProducao>
{
    public void Configure(EntityTypeBuilder<TarefaProducao> builder)
    {
        builder.ToTable("tarefas_producao", t =>
        {
            t.HasCheckConstraint("ck_tarefas_tempo_estimado_positivo", "tempo_estimado_minutos > 0");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.OrdemProducaoId)
            .HasColumnName("ordem_producao_id")
            .IsRequired();

        builder.Property(t => t.IdServicoOrigem)
            .HasColumnName("id_servico_origem")
            .IsRequired();

        builder.Property(t => t.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.TempoEstimadoMinutos)
            .HasColumnName("tempo_estimado_minutos")
            .IsRequired();

        builder.Property(t => t.IdMecanicoResponsavel)
            .HasColumnName("id_mecanico_responsavel");

        builder.Property(t => t.NomeMecanicoResponsavel)
            .HasColumnName("nome_mecanico_responsavel")
            .HasMaxLength(150);

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.ToUpperSnake(),
                v => StatusTarefaProducaoExtensions.FromUpperSnake(v))
            .HasMaxLength(30)
            .HasDefaultValue(StatusTarefaProducao.Pendente)
            .HasSentinel(default)
            .IsRequired();

        builder.Property(t => t.DataHoraInicio)
            .HasColumnName("data_hora_inicio");

        builder.Property(t => t.DataHoraFim)
            .HasColumnName("data_hora_fim");

        builder.Property(t => t.ObservacoesTecnicas)
            .HasColumnName("observacoes_tecnicas")
            .HasColumnType("text");

        builder.Property(t => t.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.Property(t => t.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .IsRequired();

        // Índice parcial para métricas de produtividade por mecânico e período (Seção 5.3)
        builder.HasIndex(t => new { t.IdMecanicoResponsavel, t.DataHoraFim })
            .HasDatabaseName("idx_tarefas_mecanico_fim")
            .HasFilter("status = 'CONCLUIDA'");
    }
}
