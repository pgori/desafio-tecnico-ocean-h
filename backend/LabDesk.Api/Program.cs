using System.Text.Json.Serialization;
using LabDesk.Api.Comum;
using LabDesk.Api.Servicos;
using LabDesk.Infrastructure;
using LabDesk.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

ConfiguracaoDeHospedagem.Aplicar(builder);

builder.Services.AddControllers()
    .AddJsonOptions(opcoes =>
    {
        // Enums viajam como texto ("AguardandoColeta") em vez de numero.
        // O front fica legivel e a API nao quebra se a ordem do enum mudar.
        opcoes.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AdicionarInfraestrutura(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<RelogioDoLaboratorio>();
builder.Services.AddScoped<ResponsavelAtual>();
builder.Services.AddScoped<CadastroServico>();
builder.Services.AddScoped<AtendimentoServico>();
builder.Services.AddScoped<TriagemServico>();
builder.Services.AddScoped<PainelServico>();

// A validacao automatica do ASP.NET responde em ingles e com nome de propriedade do DTO.
// Quem le a mensagem esta na recepcao, entao ela e reescrita em portugues.
builder.Services.Configure<ApiBehaviorOptions>(opcoes =>
    opcoes.InvalidModelStateResponseFactory = ValidacaoDeEntrada.Responder);

builder.Services.AddExceptionHandler<TratamentoDeErros>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opcoes =>
{
    opcoes.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LabDesk — fluxo pré-analítico",
        Version = "v1",
        Description = "Da chegada do paciente até a conferência das amostras."
    });

    // Nao ha login, mas toda acao precisa de um responsavel registrado.
    // Declarar o cabecalho aqui faz o Swagger oferecer um campo para preenche-lo.
    opcoes.AddSecurityDefinition("Responsavel", new OpenApiSecurityScheme
    {
        Name = ResponsavelAtual.NomeDoCabecalho,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Nome de quem está operando o sistema, percent-encoded (ex.: Ana%20-%20coleta)."
    });

    opcoes.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Responsavel", documento)] = new List<string>()
    });

    var xml = Path.Combine(AppContext.BaseDirectory, "LabDesk.Api.xml");
    if (File.Exists(xml))
        opcoes.IncludeXmlComments(xml);
});

const string politicaCors = "front";
builder.Services.AddCors(opcoes => opcoes.AddPolicy(politicaCors, politica =>
{
    var origens = builder.Configuration.GetSection("Cors:Origens").Get<string[]>()
                  ?? ["http://localhost:5173"];

    politica.WithOrigins(origens)
        .AllowAnyHeader()
        .AllowAnyMethod();
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors(politicaCors);

app.UseSwagger();
app.UseSwaggerUI(opcoes => opcoes.SwaggerEndpoint("/swagger/v1/swagger.json", "LabDesk v1"));

app.MapControllers();
// Conveniencia para quem abre a URL da API no navegador: a raiz leva ao Swagger.
// Fica fora da documentacao porque e atalho de navegacao, nao faz parte do contrato
// da API - listado no Swagger, so apareceria como um grupo solto e sem proposito.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await PrepararBancoAsync(app);

app.Run();

// Cria o banco e carrega o catalogo na subida.
//
// O schema e criado com EnsureCreated em vez de migrations: o banco deste recorte e
// descartavel e o projeto roda em dois provedores (SQLite e Postgres), que exigiriam
// dois conjuntos de migrations. Num sistema com dados reais, migrations seriam obrigatorias.
static async Task PrepararBancoAsync(WebApplication app)
{
    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<LabDeskDbContext>();

    await db.Database.EnsureCreatedAsync();
    await SeedInicial.ExecutarAsync(db);

    // Atendimentos de exemplo so na instancia publica, para a tela nao abrir vazia.
    if (app.Configuration.GetValue<bool>("Banco:DadosDeDemonstracao"))
        await SeedDemonstracao.ExecutarAsync(db);
}

/// <summary>Ponto de entrada exposto para os testes de integracao usarem a WebApplicationFactory.</summary>
public partial class Program;
