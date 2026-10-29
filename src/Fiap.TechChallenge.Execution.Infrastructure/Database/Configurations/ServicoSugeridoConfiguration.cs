using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiap.TechChallenge.Execution.Infrastructure.Database.Configurations;

public class ServicoSugeridoConfiguration : IEntityTypeConfiguration<ServicoSugerido>
{
    public void Configure(EntityTypeBuilder<ServicoSugerido> builder)
    {
        builder.ToTable("diagnostico_servicos_sugeridos");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.DiagnosticoId)
            .HasColumnName("diagnostico_id")
            .IsRequired();

        builder.Property(s => s.IdServicoCatalogo)
            .HasColumnName("id_servico_catalogo")
            .IsRequired();

        builder.Property(s => s.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(200);

        builder.Property(s => s.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.Property(s => s.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .IsRequired();
    }
}

