using FluentAssertions;
using LabDesk.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LabDesk.Tests.Infraestrutura;

/// <summary>
/// A carga do catalogo roda a cada subida da aplicacao, e nao so em banco vazio.
///
/// Isso existe porque catalogo e dado de referencia: corrigir o nome de um exame ou a
/// redacao de um motivo de rejeicao nao pode exigir apagar o banco. Foi exatamente o que
/// aconteceu ao acentuar o catalogo - o codigo estava certo e a tela continuava errada,
/// porque o banco ja existia.
/// </summary>
public class SeedInicialTestes : IDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");

    public SeedInicialTestes() => _conexao.Open();

    private LabDeskDbContext CriarContexto()
    {
        var opcoes = new DbContextOptionsBuilder<LabDeskDbContext>()
            .UseSqlite(_conexao)
            .Options;

        var db = new LabDeskDbContext(opcoes);
        db.Database.EnsureCreated();

        return db;
    }

    [Fact]
    public async Task Corrige_dado_de_catalogo_que_ficou_desatualizado_no_banco()
    {
        await using var db = CriarContexto();
        await SeedInicial.ExecutarAsync(db);

        // Simula um banco criado antes de o catalogo ser corrigido.
        var exame = await db.Exames.FirstAsync(e => e.Codigo == "AMON");
        exame.Atualizar("Amonia", exame.TipoTuboId, exame.ExigeJejum, exame.HorasJejum, "Bioquimica");
        await db.SaveChangesAsync();

        await SeedInicial.ExecutarAsync(db);

        var corrigido = await db.Exames.FirstAsync(e => e.Codigo == "AMON");
        corrigido.Nome.Should().Be("Amônia");
        corrigido.SetorDestino.Should().Be("Bioquímica");
    }

    [Fact]
    public async Task Nao_duplica_o_catalogo_quando_roda_de_novo()
    {
        await using var db = CriarContexto();

        await SeedInicial.ExecutarAsync(db);
        var exames = await db.Exames.CountAsync();
        var motivos = await db.MotivosRejeicao.CountAsync();
        var tubos = await db.TiposTubo.CountAsync();

        await SeedInicial.ExecutarAsync(db);

        (await db.Exames.CountAsync()).Should().Be(exames);
        (await db.MotivosRejeicao.CountAsync()).Should().Be(motivos);
        (await db.TiposTubo.CountAsync()).Should().Be(tubos);
    }

    [Fact]
    public async Task Mantem_o_mesmo_id_do_item_de_catalogo_entre_execucoes()
    {
        await using var db = CriarContexto();
        await SeedInicial.ExecutarAsync(db);
        var idOriginal = (await db.Exames.FirstAsync(e => e.Codigo == "HEMOG")).Id;

        await SeedInicial.ExecutarAsync(db);

        // Recriar o item trocaria o Id e quebraria o vinculo das amostras ja coletadas.
        (await db.Exames.FirstAsync(e => e.Codigo == "HEMOG")).Id.Should().Be(idOriginal);
    }

    [Fact]
    public async Task Nao_sobrescreve_os_pacientes_ja_cadastrados()
    {
        await using var db = CriarContexto();
        await SeedInicial.ExecutarAsync(db);

        var paciente = await db.Pacientes.FirstAsync();
        db.Pacientes.Remove(paciente);
        await db.SaveChangesAsync();
        var restantes = await db.Pacientes.CountAsync();

        await SeedInicial.ExecutarAsync(db);

        // Paciente e dado da operacao, nao catalogo: a carga nao recria o que a recepcao mexeu.
        (await db.Pacientes.CountAsync()).Should().Be(restantes);
    }

    public void Dispose() => _conexao.Dispose();
}
