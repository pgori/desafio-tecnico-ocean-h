using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LabDesk.Tests.Integracao;

/// <summary>
/// Sobe a API inteira em memoria, com um SQLite proprio para cada execucao.
///
/// Nao ha dubles: o teste passa pelo controller, pelo servico, pelo dominio e pelo banco.
/// E o unico jeito de garantir que o mapeamento do EF e as regras do dominio combinam,
/// que foi justamente onde os erros apareceram durante o desenvolvimento.
/// </summary>
public class ApiDeTeste : WebApplicationFactory<Program>
{
    private readonly string _arquivoDoBanco = Path.Combine(Path.GetTempPath(), $"labdesk-teste-{Guid.NewGuid():N}.db");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Banco:Provedor"] = "Sqlite",
            ["ConnectionStrings:Padrao"] = $"Data Source={_arquivoDoBanco}"
        }));

        return base.CreateHost(builder);
    }

    /// <summary>
    /// Cliente ja identificado, como o front faz ao enviar o responsavel da acao.
    /// O nome vai percent-encoded porque cabecalho HTTP so trafega ASCII.
    /// </summary>
    public HttpClient CriarClienteComResponsavel(string responsavel = "Teste automatizado")
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Responsavel", Uri.EscapeDataString(responsavel));
        return cliente;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        // O SQLite mantem a conexao em pool depois que o host cai, e o arquivo
        // segue bloqueado. Limpar o pool antes libera o arquivo temporario.
        SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_arquivoDoBanco))
                File.Delete(_arquivoDoBanco);
        }
        catch (IOException)
        {
            // Arquivo temporario preso: o sistema operacional limpa depois.
        }
    }
}

public static class RespostaHttp
{
    public static async Task<T> LerAsync<T>(this HttpResponseMessage resposta)
    {
        var conteudo = await resposta.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(conteudo, ApiDeTeste.Json)
               ?? throw new InvalidOperationException($"Resposta vazia: {conteudo}");
    }

    public static Task<HttpResponseMessage> PostVazioAsync(this HttpClient cliente, string rota) =>
        cliente.PostAsJsonAsync(rota, new { });
}
