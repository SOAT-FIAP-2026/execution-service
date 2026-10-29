using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiap.TechChallenge.Execution.Infrastructure.Database.Configurations;

public class OrdemProducaoConfiguration : IEntityTypeConfiguration<OrdemProducao>
{
    public void Configure(EntityTypeBuilder<OrdemProducao> builder)
    {
        builder.ToTable("ordens_producao");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(o => o.OrdemServicoId)
            .HasColumnName("ordem_servico_id")
            .IsRequired();

        builder.Property(o => o.FilialId)
            .HasColumnName("filial_id")
            .HasMaxLength(50);

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.ToUpperSnake(),
                v => StatusOrdemProducaoExtensions.FromUpperSnake(v))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.Versao)
            .HasColumnName("versao")
            .HasDefaultValue(1)
            .HasSentinel(0)
            .IsConcurrencyToken()
            .IsRequired();

        builder.ComplexProperty(o => o.Cliente, c =>
        {
            c.Property(p => p.IdCliente)
                .HasColumnName("cliente_id")
                .IsRequired();

            c.Property(p => p.Nome)
                .HasColumnName("cliente_nome")
                .HasMaxLength(150)
                .IsRequired();

            c.Property(p => p.Telefone)
                .HasColumnName("cliente_telefone")
                .HasMaxLength(20);
        });

        builder.ComplexProperty(o => o.Veiculo, v =>
        {
            v.Property(p => p.Placa)
                .HasColumnName("veiculo_placa")
                .HasMaxLength(10)
                .IsRequired();

            v.Property(p => p.Modelo)
                .HasColumnName("veiculo_modelo")
                .HasMaxLength(100)
                .IsRequired();

            v.Property(p => p.Ano)
                .HasColumnName("veiculo_ano")
                .IsRequired();
        });

        builder.Property(o => o.DataAbertura)
            .HasColumnName("data_abertura")
            .IsRequired();

        builder.Property(o => o.DataLiberacao)
            .HasColumnName("data_liberacao");

        builder.Property(o => o.DataConclusao)
            .HasColumnName("data_conclusao");

        builder.Property(o => o.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.Property(o => o.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .IsRequired();

        // Índices e constraints especializadas (Seção 5.3)
        builder.HasIndex(o => o.OrdemServicoId)
            .IsUnique()
            .HasDatabaseName("uk_ordens_producao_os");

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("idx_ordens_producao_status");

        builder.HasIndex(o => o.DataAbertura)
            .HasDatabaseName("idx_ordens_producao_fila_diag")
            .HasFilter("status = 'AGUARDANDO_DIAGNOSTICO'");

        builder.HasIndex(o => o.DataLiberacao)
            .HasDatabaseName("idx_ordens_producao_fila_prod")
            .HasFilter("status = 'NA_FILA'");

        // Relacionamento 1:1 com Diagnostico
        builder.HasOne(o => o.Diagnostico)
            .WithOne()
            .HasForeignKey<Diagnostico>(d => d.OrdemProducaoId)
            .HasConstraintName("fk_diagnostico_ordem_producao")
            .OnDelete(DeleteBehavior.Cascade);

        // Relacionamento 1:N com Tarefas de Produção
        builder.HasMany(o => o.Tarefas)
            .WithOne()
            .HasForeignKey(t => t.OrdemProducaoId)
            .HasConstraintName("fk_tarefas_ordem_producao")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Tarefas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Relacionamento 1:N com Histórico de Estados
        builder.HasMany(o => o.Historico)
            .WithOne()
            .HasForeignKey(h => h.OrdemProducaoId)
            .HasConstraintName("fk_historico_ordem_producao")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Historico)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
