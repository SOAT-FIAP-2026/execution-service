using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiap.TechChallenge.Execution.Infrastructure.Database.Configurations;

public class PecaSugeridaConfiguration : IEntityTypeConfiguration<PecaSugerida>
{
    public void Configure(EntityTypeBuilder<PecaSugerida> builder)
    {
        builder.ToTable("diagnostico_pecas_sugeridas", t =>
        {
            t.HasCheckConstraint("ck_diag_pecas_quantidade_positiva", "quantidade > 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.DiagnosticoId)
            .HasColumnName("diagnostico_id")
            .IsRequired();

        builder.Property(p => p.IdPecaCatalogo)
            .HasColumnName("id_peca_catalogo")
            .IsRequired();

        builder.Property(p => p.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(200);

        builder.Property(p => p.Quantidade)
            .HasColumnName("quantidade")
            .IsRequired();

        builder.Property(p => p.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.Property(p => p.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .IsRequired();
    }
}

