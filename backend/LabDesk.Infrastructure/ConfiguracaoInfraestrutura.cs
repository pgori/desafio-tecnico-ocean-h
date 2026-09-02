using LabDesk.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LabDesk.Infrastructure;

public static class ConfiguracaoInfraestrutura
{
    /// <summary>
    /// Registra o banco de dados.
    ///
    /// SQLite e o padrao para quem so quer clonar o repositorio e rodar, sem subir container.
    /// Postgres e usado quando existe uma connection string de verdade (Docker Compose e deploy).
    /// O provedor e escolhido por configuracao para o codigo de dominio nao saber a diferenca.
    /// </summary>
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, IConfiguration config)
    {
        var provedor = config["Banco:Provedor"] ?? "Sqlite";
        var conexao = config.GetConnectionString("Padrao") ?? "Data Source=labdesk.db";

        services.AddDbContext<LabDeskDbContext>(options =>
        {
            if (provedor.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
                options.UseNpgsql(conexao);
            else
                options.UseSqlite(conexao);
        });

        return services;
    }
}
