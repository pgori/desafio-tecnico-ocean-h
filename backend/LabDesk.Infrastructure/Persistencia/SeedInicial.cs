using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Pacientes;
using Microsoft.EntityFrameworkCore;

namespace LabDesk.Infrastructure.Persistencia;

/// <summary>
/// Carga do catalogo do laboratorio.
///
/// O catalogo e pre-requisito do fluxo: sem saber em que tubo cada exame vai,
/// nao da para agrupar a coleta.
///
/// A carga e sincronizada a cada subida, nao aplicada so em banco vazio. Catalogo e dado
/// de referencia: o laboratorio corrige o nome de um exame, revisa o volume minimo de um
/// tubo, troca a redacao de um motivo de rejeicao. Se a carga so rodasse uma vez, corrigir
/// qualquer um desses dados exigiria apagar o banco inteiro.
///
/// A identidade e o codigo (ou a cor, no caso do tubo), nunca o Id: assim as amostras e
/// rejeicoes ja registradas continuam apontando para o mesmo item de catalogo.
/// </summary>
public static class SeedInicial
{
    public static async Task ExecutarAsync(LabDeskDbContext db, CancellationToken ct = default)
    {
        await SincronizarTubosAsync(db, ct);
        await SincronizarExamesAsync(db, ct);
        await SincronizarMotivosAsync(db, ct);
        await CriarPacientesDeExemploAsync(db, ct);
    }

    private static async Task SincronizarTubosAsync(LabDeskDbContext db, CancellationToken ct)
    {
        var existentes = await db.TiposTubo.ToDictionaryAsync(t => t.Cor, ct);

        foreach (var novo in CriarTubos())
        {
            if (existentes.TryGetValue(novo.Cor, out var atual))
                atual.Atualizar(novo.Aditivo, novo.OrdemColeta, novo.VolumeMinimoMl);
            else
                db.TiposTubo.Add(novo);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SincronizarExamesAsync(LabDeskDbContext db, CancellationToken ct)
    {
        var tubo = await db.TiposTubo.ToDictionaryAsync(t => t.Cor, t => t.Id, ct);
        var existentes = await db.Exames.ToDictionaryAsync(e => e.Codigo, ct);

        foreach (var novo in CriarExames(tubo))
        {
            if (existentes.TryGetValue(novo.Codigo, out var atual))
                atual.Atualizar(novo.Nome, novo.TipoTuboId, novo.ExigeJejum, novo.HorasJejum, novo.SetorDestino);
            else
                db.Exames.Add(novo);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SincronizarMotivosAsync(LabDeskDbContext db, CancellationToken ct)
    {
        var existentes = await db.MotivosRejeicao.ToDictionaryAsync(m => m.Codigo, ct);

        foreach (var novo in CriarMotivosRejeicao())
        {
            if (existentes.TryGetValue(novo.Codigo, out var atual))
                atual.Atualizar(novo.Descricao, novo.ExigeRecoleta);
            else
                db.MotivosRejeicao.Add(novo);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Pacientes de exemplo entram so para o sistema abrir com dados na tela e poder ser
    /// testado sem cadastro manual. Diferente do catalogo, nao sao sincronizados: paciente
    /// e dado da operacao, e sobrescrever o que a recepcao cadastrou seria destrutivo.
    /// </summary>
    private static async Task CriarPacientesDeExemploAsync(LabDeskDbContext db, CancellationToken ct)
    {
        if (await db.Pacientes.AnyAsync(ct))
            return;

        db.Pacientes.AddRange(CriarPacientes());
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Tubos na sequencia recomendada de coleta. A ordem existe para o aditivo de um tubo
    /// nao contaminar o proximo: citrato primeiro, tubos com anticoagulante forte por ultimo.
    /// </summary>
    private static List<TipoTubo> CriarTubos() =>
    [
        new("Azul", "Citrato de sódio 3,2%", 1, 2.7m),
        new("Amarela", "Ativador de coágulo com gel separador", 2, 5.0m),
        new("Verde", "Heparina de lítio", 3, 4.0m),
        new("Roxa", "EDTA K3", 4, 4.0m),
        new("Cinza", "Fluoreto de sódio com EDTA", 5, 2.0m)
    ];

    /// <summary>
    /// Catalogo enxuto, mas escolhido para cobrir os casos que o fluxo precisa saber tratar:
    /// exames diferentes no mesmo tubo, exames com jejum e o mesmo tubo servindo setores distintos
    /// (hemograma e hemoglobina glicada usam EDTA, mas vao para bancadas diferentes).
    /// </summary>
    private static List<Exame> CriarExames(IReadOnlyDictionary<string, int> tubo) =>
    [
        new("HEMOG", "Hemograma completo", tubo["Roxa"], false, 0, "Hematologia"),
        new("VHS", "Velocidade de hemossedimentação", tubo["Roxa"], false, 0, "Hematologia"),
        new("RETIC", "Contagem de reticulócitos", tubo["Roxa"], false, 0, "Hematologia"),
        new("HBA1C", "Hemoglobina glicada", tubo["Roxa"], false, 0, "Bioquímica"),

        new("GLI", "Glicemia de jejum", tubo["Cinza"], true, 8, "Bioquímica"),

        new("COLT", "Colesterol total e frações", tubo["Amarela"], true, 12, "Bioquímica"),
        new("TRIG", "Triglicerídeos", tubo["Amarela"], true, 12, "Bioquímica"),
        new("FERRO", "Ferro sérico", tubo["Amarela"], true, 8, "Bioquímica"),
        new("CREA", "Creatinina", tubo["Amarela"], false, 0, "Bioquímica"),
        new("URE", "Ureia", tubo["Amarela"], false, 0, "Bioquímica"),
        new("TGP", "TGP / ALT", tubo["Amarela"], false, 0, "Bioquímica"),
        new("ACURI", "Ácido úrico", tubo["Amarela"], false, 0, "Bioquímica"),

        new("TSH", "TSH - hormônio tireoestimulante", tubo["Amarela"], false, 0, "Hormônios"),
        new("T4L", "T4 livre", tubo["Amarela"], false, 0, "Hormônios"),

        new("ANTIHIV", "Anti-HIV 1 e 2", tubo["Amarela"], false, 0, "Sorologia"),
        new("HBSAG", "HBsAg - antígeno de superfície da hepatite B", tubo["Amarela"], false, 0, "Sorologia"),

        new("TP", "Tempo de protrombina (TP/INR)", tubo["Azul"], false, 0, "Hemostasia"),
        new("TTPA", "Tempo de tromboplastina parcial ativada", tubo["Azul"], false, 0, "Hemostasia"),

        new("AMON", "Amônia", tubo["Verde"], true, 8, "Bioquímica")
    ];

    /// <summary>
    /// Motivos de recusa da triagem. A maioria obriga recoleta, mas nem todos:
    /// tubo coletado a mais ou exame cancelado sao apenas descartados.
    /// </summary>
    private static List<MotivoRejeicao> CriarMotivosRejeicao() =>
    [
        new("HEMOLISE", "Amostra hemolisada", true),
        new("QNS", "Volume insuficiente para o exame (QNS)", true),
        new("COAGULO", "Amostra coagulada em tubo com anticoagulante", true),
        new("TUBO", "Tubo incorreto para o exame solicitado", true),
        new("IDENT", "Identificação ausente ou divergente do paciente", true),
        new("LIPEMIA", "Amostra lipêmica", true),
        new("ESTABILIDADE", "Fora do prazo de estabilidade ou temperatura de transporte", true),
        new("PREPARO", "Preparo do paciente não cumprido", true),
        new("EXTRA", "Tubo coletado a mais, sem exame vinculado", false),
        new("CANCELADO", "Exame cancelado após a coleta", false)
    ];

    private static List<Paciente> CriarPacientes() =>
    [
        new("Maria Aparecida de Souza", new DateOnly(1948, 3, 12), "123.456.789-00", "(11) 98800-1122"),
        new("João Pedro Ribeiro", new DateOnly(1991, 11, 2), "987.654.321-00", "(11) 97711-3344"),
        new("Ana Carolina Lima", new DateOnly(1985, 7, 25), "456.789.123-00", "(11) 96622-5566"),
        new("Carlos Eduardo Nogueira", new DateOnly(1972, 1, 9), "321.654.987-00", "(11) 95533-7788"),
        new("Beatriz Nunes Alves", new DateOnly(2016, 5, 30), "789.123.456-00", "(11) 94444-9900")
    ];
}
