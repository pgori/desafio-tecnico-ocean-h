namespace LabDesk.Api.Comum;

/// <summary>
/// Ajustes para a aplicacao subir tanto na maquina do desenvolvedor quanto no Railway,
/// que entrega porta e banco por variavel de ambiente em formatos proprios.
///
/// Sao poucas linhas e ficam isoladas aqui de proposito: e configuracao de hospedagem,
/// nao regra do laboratorio.
/// </summary>
public static class ConfiguracaoDeHospedagem
{
    public static void Aplicar(WebApplicationBuilder builder)
    {
        // O Railway sorteia a porta e informa em PORT. Sem isso o container sobe e nao responde.
        var porta = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(porta))
            builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");

        // DATABASE_URL vem no formato de URL do Postgres, que o Npgsql nao aceita direto.
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            builder.Configuration["Banco:Provedor"] = "Postgres";
            builder.Configuration["ConnectionStrings:Padrao"] = ConverterUrlDoPostgres(url);
        }
    }

    /// <summary>Traduz postgresql://usuario:senha@host:porta/banco para a connection string do Npgsql.</summary>
    private static string ConverterUrlDoPostgres(string url)
    {
        var uri = new Uri(url);
        var credenciais = uri.UserInfo.Split(':', 2);

        return string.Join(';',
            $"Host={uri.Host}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Database={uri.AbsolutePath.TrimStart('/')}",
            $"Username={Uri.UnescapeDataString(credenciais[0])}",
            $"Password={Uri.UnescapeDataString(credenciais.Length > 1 ? credenciais[1] : string.Empty)}",
            "SSL Mode=Require",
            "Trust Server Certificate=true");
    }
}
