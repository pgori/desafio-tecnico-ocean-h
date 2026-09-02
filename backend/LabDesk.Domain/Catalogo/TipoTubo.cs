namespace LabDesk.Domain.Catalogo;

/// <summary>
/// Tipo de tubo de coleta, identificado pela cor da tampa e pelo aditivo.
/// Cada exame so pode ser feito no tubo correto, porque o aditivo interfere no resultado.
/// </summary>
public class TipoTubo
{
    public int Id { get; private set; }

    /// <summary>Cor da tampa, que e como o coletor identifica o tubo na bancada (ex.: "Roxa").</summary>
    public string Cor { get; private set; } = string.Empty;

    /// <summary>Aditivo presente no tubo (ex.: "EDTA", "Citrato de sodio").</summary>
    public string Aditivo { get; private set; } = string.Empty;

    /// <summary>
    /// Posicao do tubo na sequencia de coleta (order of draw).
    /// Coletar fora de ordem carrega aditivo de um tubo para o outro e altera resultados,
    /// entao a tela de coleta sempre lista os tubos ordenados por este campo.
    /// </summary>
    public int OrdemColeta { get; private set; }

    /// <summary>Volume minimo aceitavel. Abaixo disso a amostra e rejeitada por volume insuficiente.</summary>
    public decimal VolumeMinimoMl { get; private set; }

    private TipoTubo()
    {
    }

    public TipoTubo(string cor, string aditivo, int ordemColeta, decimal volumeMinimoMl)
    {
        Cor = cor;
        Aditivo = aditivo;
        OrdemColeta = ordemColeta;
        VolumeMinimoMl = volumeMinimoMl;
    }

    /// <summary>
    /// Corrige os dados do tubo. A cor identifica o tubo e nao muda; o resto e informacao
    /// de catalogo, que o laboratorio revisa ao longo do tempo.
    /// </summary>
    public void Atualizar(string aditivo, int ordemColeta, decimal volumeMinimoMl)
    {
        Aditivo = aditivo;
        OrdemColeta = ordemColeta;
        VolumeMinimoMl = volumeMinimoMl;
    }
}
