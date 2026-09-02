using LabDesk.Domain.Atendimentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabDesk.Infrastructure.Persistencia.Configuracoes;

public class AtendimentoConfiguracao : IEntityTypeConfiguration<Atendimento>
{
    public void Configure(EntityTypeBuilder<Atendimento> builder)
    {
        builder.ToTable("atendimentos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Numero).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Observacoes).HasMaxLength(500);
        builder.HasIndex(a => a.Numero).IsUnique();
        builder.HasIndex(a => a.Status);

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Prioridade).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.MotivoCancelamento).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.CanceladoPor).HasMaxLength(80);

        // Propriedade calculada a partir dos itens: e regra de dominio, nao coluna.
        builder.Ignore(a => a.TemColetaPendente);

        builder.HasOne(a => a.Paciente)
            .WithMany()
            .HasForeignKey(a => a.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // As colecoes sao expostas como somente leitura, entao o EF precisa gravar
        // direto no campo interno em vez de tentar usar a propriedade.
        builder.Metadata.FindNavigation(nameof(Atendimento.Itens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Atendimento.Amostras))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.Itens)
            .WithOne()
            .HasForeignKey(i => i.AtendimentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Amostras)
            .WithOne()
            .HasForeignKey(a => a.AtendimentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ItemAtendimentoConfiguracao : IEntityTypeConfiguration<ItemAtendimento>
{
    public void Configure(EntityTypeBuilder<ItemAtendimento> builder)
    {
        builder.ToTable("itens_atendimento");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(i => i.Exame)
            .WithMany()
            .HasForeignKey(i => i.ExameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindSkipNavigation(nameof(ItemAtendimento.Amostras))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
