using LabDesk.Domain.Amostras;
using LabDesk.Domain.Atendimentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabDesk.Infrastructure.Persistencia.Configuracoes;

public class AmostraConfiguracao : IEntityTypeConfiguration<Amostra>
{
    public void Configure(EntityTypeBuilder<Amostra> builder)
    {
        builder.ToTable("amostras");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(a => a.ColetadoPor).HasMaxLength(80).IsRequired();
        builder.Property(a => a.SetorDestino).HasMaxLength(60);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);

        // O codigo da etiqueta e o identificador que circula pelo laboratorio.
        // Duplicar codigo significaria dois tubos diferentes com a mesma identidade.
        builder.HasIndex(a => a.Codigo).IsUnique();
        builder.HasIndex(a => a.Status);

        builder.HasOne(a => a.TipoTubo)
            .WithMany()
            .HasForeignKey(a => a.TipoTuboId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.MotivoRejeicao)
            .WithMany()
            .HasForeignKey(a => a.MotivoRejeicaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Muitos-para-muitos: um tubo carrega varios exames, e um exame rejeitado
        // aparece tambem no tubo da recoleta. Manter os dois vinculos preserva o historico.
        builder.HasMany(a => a.Itens)
            .WithMany(i => i.Amostras)
            .UsingEntity(j => j.ToTable("amostras_itens"));

        builder.HasMany(a => a.Eventos)
            .WithOne()
            .HasForeignKey(e => e.AmostraId)
            .OnDelete(DeleteBehavior.Cascade);

        // As colecoes sao expostas como somente leitura, entao o EF precisa gravar direto
        // no campo interno. Isso so pode ser feito depois de declarar as relacoes acima,
        // que sao o que cria as navegacoes no modelo.
        // Itens e uma skip navigation (muitos-para-muitos), por isso a busca e diferente.
        builder.Metadata.FindSkipNavigation(nameof(Amostra.Itens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Amostra.Eventos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class EventoAmostraConfiguracao : IEntityTypeConfiguration<EventoAmostra>
{
    public void Configure(EntityTypeBuilder<EventoAmostra> builder)
    {
        builder.ToTable("eventos_amostra");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Responsavel).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Detalhe).HasMaxLength(300);
        builder.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(30);
    }
}
