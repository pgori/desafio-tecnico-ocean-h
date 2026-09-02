namespace LabDesk.Domain.Catalogo;

/// <summary>
/// Motivo padronizado para recusar uma amostra na triagem.
/// A lista e fechada de proposito: motivo em texto livre impede medir as nao conformidades
/// da fase pre-analitica, que e justamente onde o laboratorio mais erra.
/// </summary>
public class MotivoRejeicao
{
    public int Id { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public string Descricao { get; private set; } = string.Empty;

    /// <summary>
    /// Se este motivo obriga a coletar o paciente de novo.
    /// Nem todo motivo obriga: um tubo coletado a mais, por exemplo, e so descartado.
    /// </summary>
    public bool ExigeRecoleta { get; private set; }

    private MotivoRejeicao()
    {
    }

    public MotivoRejeicao(string codigo, string descricao, bool exigeRecoleta)
    {
        Codigo = codigo;
        Descricao = descricao;
        ExigeRecoleta = exigeRecoleta;
    }

    /// <summary>
    /// Corrige o motivo. O codigo identifica o motivo e nao muda, para as rejeicoes ja
    /// registradas continuarem contando no mesmo indicador.
    /// </summary>
    public void Atualizar(string descricao, bool exigeRecoleta)
    {
        Descricao = descricao;
        ExigeRecoleta = exigeRecoleta;
    }
}
