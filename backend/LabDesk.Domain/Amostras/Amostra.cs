using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Comum;

namespace LabDesk.Domain.Amostras;

/// <summary>
/// Um tubo fisico coletado do paciente. Carrega um ou mais exames que usam o mesmo aditivo.
/// As transicoes de estado sao internal de proposito: quem comanda e o Atendimento,
/// que e o unico ponto capaz de manter os itens e o proprio atendimento coerentes.
/// </summary>
public class Amostra
{
    public int Id { get; private set; }

    /// <summary>Codigo unico impresso na etiqueta. E por ele que a amostra circula no laboratorio.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public int AtendimentoId { get; private set; }

    public int TipoTuboId { get; private set; }
    public TipoTubo TipoTubo { get; private set; } = null!;

    public DateTime DataHoraColeta { get; private set; }

    public string ColetadoPor { get; private set; } = string.Empty;

    public StatusAmostra Status { get; private set; } = StatusAmostra.Coletada;

    public DateTime? DataHoraTriagem { get; private set; }

    public int? MotivoRejeicaoId { get; private set; }
    public MotivoRejeicao? MotivoRejeicao { get; private set; }

    /// <summary>Setor tecnico para onde o tubo foi encaminhado depois de aceito.</summary>
    public string? SetorDestino { get; private set; }

    private readonly List<ItemAtendimento> _itens = new();
    public IReadOnlyCollection<ItemAtendimento> Itens => _itens.AsReadOnly();

    private readonly List<EventoAmostra> _eventos = new();
    public IReadOnlyCollection<EventoAmostra> Eventos => _eventos.AsReadOnly();

    private Amostra()
    {
    }

    internal Amostra(string codigo, TipoTubo tipoTubo, IEnumerable<ItemAtendimento> itens, DateTime dataHoraColeta, string coletadoPor)
    {
        Codigo = codigo;
        TipoTubo = tipoTubo;
        TipoTuboId = tipoTubo.Id;
        DataHoraColeta = dataHoraColeta;
        ColetadoPor = coletadoPor;

        foreach (var item in itens)
        {
            _itens.Add(item);
            item.VincularA(this);
        }

        _eventos.Add(new EventoAmostra(TipoEventoAmostra.Coletada, dataHoraColeta, coletadoPor,
            $"Tubo {tipoTubo.Cor.ToLowerInvariant()} ({tipoTubo.Aditivo})"));
    }

    /// <summary>Aprova a amostra na conferencia. So faz sentido para tubo recem-coletado.</summary>
    internal void Aceitar(DateTime agora, string responsavel)
    {
        GarantirQueEstaEmTriagem("aceitar");

        Status = StatusAmostra.Aceita;
        DataHoraTriagem = agora;
        _eventos.Add(new EventoAmostra(TipoEventoAmostra.Aceita, agora, responsavel, null));
    }

    /// <summary>
    /// Recusa a amostra na conferencia. O tubo nao e apagado: fica registrado como rejeitado,
    /// porque essa informacao e o indicador de nao conformidade da fase pre-analitica.
    /// </summary>
    internal void Rejeitar(MotivoRejeicao motivo, DateTime agora, string responsavel, string? observacao)
    {
        GarantirQueEstaEmTriagem("rejeitar");

        Status = StatusAmostra.Rejeitada;
        DataHoraTriagem = agora;
        MotivoRejeicao = motivo;
        MotivoRejeicaoId = motivo.Id;

        var detalhe = string.IsNullOrWhiteSpace(observacao)
            ? motivo.Descricao
            : $"{motivo.Descricao} - {observacao.Trim()}";

        _eventos.Add(new EventoAmostra(TipoEventoAmostra.Rejeitada, agora, responsavel, detalhe));

        // O destino do exame depende do motivo: hemolise pede sangue novo,
        // tubo coletado a mais e so descartado.
        var novoStatus = motivo.ExigeRecoleta
            ? StatusItemAtendimento.AguardandoRecoleta
            : StatusItemAtendimento.Cancelado;

        foreach (var item in _itens)
            item.MarcarComo(novoStatus);
    }

    /// <summary>Entrega a amostra ao setor tecnico. Encerra o fluxo pre-analitico deste tubo.</summary>
    internal void Encaminhar(DateTime agora, string responsavel)
    {
        if (Status != StatusAmostra.Aceita)
            throw new RegraDeNegocioException(
                $"A amostra {Codigo} precisa ser aceita na triagem antes de ser encaminhada ao setor.");

        // Todos os exames do tubo vao para o mesmo setor porque o agrupamento
        // da coleta ja considera o setor de destino alem do tipo de tubo.
        SetorDestino = _itens.First().Exame.SetorDestino;
        Status = StatusAmostra.Encaminhada;
        _eventos.Add(new EventoAmostra(TipoEventoAmostra.Encaminhada, agora, responsavel, SetorDestino));

        foreach (var item in _itens)
            item.MarcarComo(StatusItemAtendimento.EmAnalise);
    }

    private void GarantirQueEstaEmTriagem(string acao)
    {
        if (Status != StatusAmostra.Coletada)
            throw new RegraDeNegocioException(
                $"Não é possível {acao} a amostra {Codigo}: ela já foi conferida na triagem (situação atual: {Status}).");
    }
}
