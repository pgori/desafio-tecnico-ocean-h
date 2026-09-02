using LabDesk.Domain.Pacientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabDesk.Infrastructure.Persistencia.Configuracoes;

public class PacienteConfiguracao : IEntityTypeConfiguration<Paciente>
{
    public void Configure(EntityTypeBuilder<Paciente> builder)
    {
        builder.ToTable("pacientes");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.NomeCompleto).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Documento).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Contato).HasMaxLength(60);

        // Documento unico evita cadastro duplicado do mesmo paciente na recepcao,
        // que e uma das causas de troca de amostra.
        builder.HasIndex(p => p.Documento).IsUnique();
        builder.HasIndex(p => p.NomeCompleto);
    }
}
