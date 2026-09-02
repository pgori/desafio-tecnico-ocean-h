using LabDesk.Domain.Amostras;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Pacientes;
using LabDesk.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;

namespace LabDesk.Infrastructure.Persistencia;

public class LabDeskDbContext : DbContext
{
    public LabDeskDbContext(DbContextOptions<LabDeskDbContext> options) : base(options)
    {
    }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<TipoTubo> TiposTubo => Set<TipoTubo>();
    public DbSet<Exame> Exames => Set<Exame>();
    public DbSet<MotivoRejeicao> MotivosRejeicao => Set<MotivoRejeicao>();
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();
    public DbSet<ItemAtendimento> ItensAtendimento => Set<ItemAtendimento>();
    public DbSet<Amostra> Amostras => Set<Amostra>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<ConversorDataHoraUtc>();
        builder.Properties<DateTime?>().HaveConversion<ConversorDataHoraUtc>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CatalogoConfiguracao.TipoTuboConfiguracao());
        modelBuilder.ApplyConfiguration(new CatalogoConfiguracao.ExameConfiguracao());
        modelBuilder.ApplyConfiguration(new CatalogoConfiguracao.MotivoRejeicaoConfiguracao());
        modelBuilder.ApplyConfiguration(new PacienteConfiguracao());
        modelBuilder.ApplyConfiguration(new AtendimentoConfiguracao());

        // A configuracao da Amostra vem antes da do item porque e ela que declara
        // o muitos-para-muitos entre os dois. So depois disso a navegacao existe no modelo.
        modelBuilder.ApplyConfiguration(new AmostraConfiguracao());
        modelBuilder.ApplyConfiguration(new ItemAtendimentoConfiguracao());
        modelBuilder.ApplyConfiguration(new EventoAmostraConfiguracao());
    }
}
