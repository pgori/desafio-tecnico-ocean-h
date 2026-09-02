using LabDesk.Domain.Amostras;
using LabDesk.Domain.Catalogo;

namespace LabDesk.Domain.Atendimentos;

/// <summary>
/// Um exame pedido dentro de um atendimento.
/// O mesmo item pode aparecer em mais de uma amostra ao longo do tempo:
/// se a primeira coleta for rejeitada na triagem, a recoleta gera um tubo novo
/// e o item passa a estar ligado aos dois, preservando o historico.
/// </summary>
public class ItemAtendimento
{
    public int Id { get; private set; }

    public int AtendimentoId { get; private set; }

    public int ExameId { get; private set; }
    public Exame Exame { get; private set; } = null!;

    public StatusItemAtendimento Status { get; private set; } = StatusItemAtendimento.AguardandoColeta;

    private readonly List<Amostra> _amostras = new();

    /// <summary>Todas as amostras em que este exame ja foi coletado, inclusive as rejeitadas.</summary>
    public IReadOnlyCollection<Amostra> Amostras => _amostras.AsReadOnly();

    private ItemAtendimento()
    {
    }

    internal ItemAtendimento(Exame exame)
    {
        Exame = exame;
        ExameId = exame.Id;
    }

    /// <summary>Item que ainda precisa de tubo: primeira coleta ou recoleta apos rejeicao.</summary>
    internal bool PrecisaDeColeta =>
        Status is StatusItemAtendimento.AguardandoColeta or StatusItemAtendimento.AguardandoRecoleta;

    internal void MarcarComo(StatusItemAtendimento status) => Status = status;

    internal void VincularA(Amostra amostra) => _amostras.Add(amostra);
}
