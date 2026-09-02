namespace LabDesk.Domain.Catalogo;

/// <summary>
/// Exame que o laboratorio oferece. E o item que o medico pede na requisicao.
/// Atencao: exame nao e a mesma coisa que amostra. Varios exames podem ser
/// atendidos por um unico tubo, e e isso que a coleta resolve.
/// </summary>
public class Exame
{
    public int Id { get; private set; }

    /// <summary>Sigla usada no dia a dia do laboratorio (ex.: "HEMOG").</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    /// <summary>Tubo em que este exame precisa ser coletado.</summary>
    public int TipoTuboId { get; private set; }
    public TipoTubo TipoTubo { get; private set; } = null!;

    /// <summary>Se o paciente precisa estar em jejum. Conferido no check-in da recepcao.</summary>
    public bool ExigeJejum { get; private set; }

    /// <summary>Horas de jejum exigidas. Zero quando o exame nao exige jejum.</summary>
    public int HorasJejum { get; private set; }

    /// <summary>Setor tecnico que vai analisar a amostra (ex.: "Hematologia").</summary>
    public string SetorDestino { get; private set; } = string.Empty;

    private Exame()
    {
    }

    public Exame(string codigo, string nome, int tipoTuboId, bool exigeJejum, int horasJejum, string setorDestino)
    {
        Codigo = codigo;
        Nome = nome;
        TipoTuboId = tipoTuboId;
        ExigeJejum = exigeJejum;
        HorasJejum = exigeJejum ? horasJejum : 0;
        SetorDestino = setorDestino;
    }

    /// <summary>
    /// Corrige os dados do exame. O codigo identifica o exame e nao muda.
    /// Trocar o tubo de um exame e revisao de catalogo normal e afeta so as coletas futuras:
    /// as amostras ja coletadas guardam o tubo que foi usado de fato.
    /// </summary>
    public void Atualizar(string nome, int tipoTuboId, bool exigeJejum, int horasJejum, string setorDestino)
    {
        Nome = nome;
        TipoTuboId = tipoTuboId;
        ExigeJejum = exigeJejum;
        HorasJejum = exigeJejum ? horasJejum : 0;
        SetorDestino = setorDestino;
    }
}
