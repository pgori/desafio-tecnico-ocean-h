namespace LabDesk.Domain.Amostras;

/// <summary>
/// Uma linha do historico da amostra: o que aconteceu, quando e quem fez.
/// E o que permite reconstruir a cadeia de custodia depois, sem depender de memoria de ninguem.
/// </summary>
public class EventoAmostra
{
    public int Id { get; private set; }

    public int AmostraId { get; private set; }

    public TipoEventoAmostra Tipo { get; private set; }

    public DateTime DataHora { get; private set; }

    /// <summary>Quem executou a acao. Hoje vem do seletor de responsavel; com login viria do usuario.</summary>
    public string Responsavel { get; private set; } = string.Empty;

    /// <summary>Detalhe livre do evento (ex.: o motivo da rejeicao).</summary>
    public string? Detalhe { get; private set; }

    private EventoAmostra()
    {
    }

    internal EventoAmostra(TipoEventoAmostra tipo, DateTime dataHora, string responsavel, string? detalhe)
    {
        Tipo = tipo;
        DataHora = dataHora;
        Responsavel = responsavel;
        Detalhe = detalhe;
    }
}
