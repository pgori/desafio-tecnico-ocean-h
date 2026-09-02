using LabDesk.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabDesk.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento das tabelas de catalogo: tubos, exames e motivos de rejeicao.</summary>
public static class CatalogoConfiguracao
{
    public class TipoTuboConfiguracao : IEntityTypeConfiguration<TipoTubo>
    {
        public void Configure(EntityTypeBuilder<TipoTubo> builder)
        {
            builder.ToTable("tipos_tubo");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Cor).HasMaxLength(30).IsRequired();
            builder.Property(t => t.Aditivo).HasMaxLength(60).IsRequired();
            builder.Property(t => t.VolumeMinimoMl).HasPrecision(4, 1);
            builder.HasIndex(t => t.OrdemColeta).IsUnique();
        }
    }

    public class ExameConfiguracao : IEntityTypeConfiguration<Exame>
    {
        public void Configure(EntityTypeBuilder<Exame> builder)
        {
            builder.ToTable("exames");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Codigo).HasMaxLength(20).IsRequired();
            builder.Property(e => e.Nome).HasMaxLength(120).IsRequired();
            builder.Property(e => e.SetorDestino).HasMaxLength(60).IsRequired();
            builder.HasIndex(e => e.Codigo).IsUnique();

            builder.HasOne(e => e.TipoTubo)
                .WithMany()
                .HasForeignKey(e => e.TipoTuboId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class MotivoRejeicaoConfiguracao : IEntityTypeConfiguration<MotivoRejeicao>
    {
        public void Configure(EntityTypeBuilder<MotivoRejeicao> builder)
        {
            builder.ToTable("motivos_rejeicao");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Codigo).HasMaxLength(20).IsRequired();
            builder.Property(m => m.Descricao).HasMaxLength(200).IsRequired();
            builder.HasIndex(m => m.Codigo).IsUnique();
        }
    }
}
