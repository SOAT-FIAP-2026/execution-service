using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiap.TechChallenge.Execution.Infrastructure.Database.Configurations;

public class DiagnosticoConfiguration : IEntityTypeConfiguration<Diagnostico>
{
    public void Configure(EntityTypeBuilder<Diagnostico> builder)
    {
        builder.ToTable("diagnosticos");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.OrdemProducaoId)
            .HasColumnName("ordem_producao_id")
            .IsRequired();

        builder.HasIndex(d => d.OrdemProducaoId)
            .IsUnique()
            .HasDatabaseName("uk_diagnostico_ordem_producao");

        builder.Property(d => d.IdMecanico)
            .HasColumnName("id_mecanico")
            .IsRequired();

        builder.Property(d => d.NomeMecanico)
            .HasColumnName("nome_mecanico")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(d => d.DataHoraInicio)
            .HasColumnName("data_hora_inicio")
            .IsRequired();

        builder.Property(d => d.DataHoraFim)
            .HasColumnName("data_hora_fim");

        builder.Property(d => d.LaudoTecnico)
            .HasColumnName("laudo_tecnico")
            .HasColumnType("text");

        builder.Property(d => d.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.Property(d => d.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .IsRequired();

        builder.HasMany(d => d.ServicosSugeridos)
            .WithOne()
            .HasForeignKey(s => s.DiagnosticoId)
            .HasConstraintName("fk_diag_servicos_diagnostico")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.ServicosSugeridos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(d => d.PecasSugeridas)
            .WithOne()
            .HasForeignKey(p => p.DiagnosticoId)
            .HasConstraintName("fk_diag_pecas_diagnostico")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.PecasSugeridas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

