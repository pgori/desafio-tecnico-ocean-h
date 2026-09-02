using LabDesk.Domain.Amostras;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Comum;
using LabDesk.Domain.Pacientes;

namespace LabDesk.Domain.Atendimentos;

/// <summary>
/// A ordem de servico do laboratorio: um paciente que chegou, os exames que ele veio fazer
/// e os tubos gerados para eles.
///
/// Toda mudanca de estado do fluxo passa por aqui. As amostras nao sao alteradas por fora
/// porque aceitar ou rejeitar um tubo muda tambem a situacao dos exames e do atendimento,
/// e esses tres precisam andar juntos.
/// </summary>
public class Atendimento
{
    public int Id { get; private set; }

    /// <summary>Numero visivel para o operador, no formato AAAAMMDD-0001.</summary>
    public string Numero { get; private set; } = string.Empty;

    public int PacienteId { get; private set; }
    public Paciente Paciente { get; private set; } = null!;

    public Prioridade Prioridade { get; private set; }

    public StatusAtendimento Status { get; private set; } = StatusAtendimento.AguardandoColeta;

    /// <summary>Momento em que o paciente chegou na recepcao. Inicio da contagem do tempo de espera.</summary>
    public DateTime DataHoraChegada { get; private set; }

    /// <summary>Momento em que o paciente foi chamado para a sala de coleta.</summary>
    public DateTime? DataHoraChamada { get; private set; }

    /// <summary>Momento da primeira coleta. Usado para medir o tempo de espera real.</summary>
    public DateTime? DataHoraPrimeiraColeta { get; private set; }

    /// <summary>Momento em que a ultima amostra foi encaminhada ao setor.</summary>
    public DateTime? DataHoraConclusao { get; private set; }

    /// <summary>Se o paciente declarou jejum no check-in. Obrigatorio quando ha exame que exige.</summary>
    public bool JejumConfirmado { get; private set; }

    public string? Observacoes { get; private set; }

    /// <summary>Por que o atendimento foi encerrado sem coleta. Nulo enquanto ele estiver valendo.</summary>
    public MotivoCancelamento? MotivoCancelamento { get; private set; }

    /// <summary>Momento do cancelamento.</summary>
    public DateTime? DataHoraCancelamento { get; private set; }

    /// <summary>Quem cancelou. Faz parte da rastreabilidade, como o resto do fluxo.</summary>
    public string? CanceladoPor { get; private set; }

    private readonly List<ItemAtendimento> _itens = new();
    public IReadOnlyCollection<ItemAtendimento> Itens => _itens.AsReadOnly();

    private readonly List<Amostra> _amostras = new();
    public IReadOnlyCollection<Amostra> Amostras => _amostras.AsReadOnly();

    /// <summary>
    /// Ainda ha tubo a tirar deste paciente: primeira coleta ou recoleta apos rejeicao.
    /// E o conceito que define quem aparece na fila e quem impede um segundo pedido.
    /// </summary>
    public bool TemColetaPendente => _itens.Any(i => i.PrecisaDeColeta);

    private Atendimento()
    {
    }

    /// <summary>
    /// Check-in na recepcao. Ja valida o preparo do paciente, porque descobrir que faltou
    /// jejum depois da picada significa perder o tubo e chamar o paciente de volta outro dia.
    /// </summary>
    public Atendimento(
        string numero,
        Paciente paciente,
        IEnumerable<Exame> exames,
        Prioridade prioridade,
        bool jejumConfirmado,
        string? observacoes,
        DateTime agora)
    {
        var listaExames = exames.DistinctBy(e => e.Id).ToList();

        if (listaExames.Count == 0)
            throw new RegraDeNegocioException("Selecione ao menos um exame para abrir o atendimento.");

        GarantirPreparoConfirmado(listaExames, jejumConfirmado);

        Numero = numero;
        Paciente = paciente;
        PacienteId = paciente.Id;
        Prioridade = prioridade;
        JejumConfirmado = jejumConfirmado;
        Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
        DataHoraChegada = agora;

        foreach (var exame in listaExames)
            _itens.Add(new ItemAtendimento(exame));
    }

    /// <summary>
    /// Impede abrir um segundo atendimento para quem ainda tem tubo a coletar.
    ///
    /// O agrupamento de exames por tubo acontece dentro de um atendimento. Dois atendimentos
    /// abertos agrupam separado, e o paciente sai com dois tubos do mesmo aditivo onde um
    /// resolveria: a regra central do sistema furada por fora dela.
    /// </summary>
    public static void GarantirQueNaoHaColetaPendente(IEnumerable<Atendimento> atendimentosDoPaciente)
    {
        var aberto = atendimentosDoPaciente.FirstOrDefault(a => a.TemColetaPendente);

        if (aberto is null)
            return;

        throw new RegraDeNegocioException(
            $"Este paciente já tem o atendimento {aberto.Numero} em aberto, com exame aguardando coleta. " +
            "Adicione os exames novos a esse atendimento, para que saiam nos mesmos tubos, " +
            "ou cancele o atendimento se o paciente não vai coletar.");
    }

    /// <summary>
    /// Acrescenta exames a um atendimento que ja esta aberto.
    ///
    /// E a saida para o paciente que chega com uma segunda requisicao. Sem isto a recepcao
    /// so teria como abrir outro atendimento, e os exames novos sairiam em tubos proprios
    /// em vez de pegar carona na coleta que ainda vai acontecer.
    /// </summary>
    public IReadOnlyList<ItemAtendimento> AdicionarExames(IEnumerable<Exame> exames, bool jejumConfirmado, DateTime agora)
    {
        if (Status is StatusAtendimento.Cancelado or StatusAtendimento.Concluido)
            throw new RegraDeNegocioException(
                $"O atendimento {Numero} já foi encerrado. Abra um atendimento novo para o paciente.");

        var novos = exames.DistinctBy(e => e.Id).ToList();

        if (novos.Count == 0)
            throw new RegraDeNegocioException("Selecione ao menos um exame para adicionar ao atendimento.");

        var repetidos = novos
            .Where(e => _itens.Any(i => i.ExameId == e.Id && i.Status != StatusItemAtendimento.Cancelado))
            .ToList();

        if (repetidos.Count > 0)
            throw new RegraDeNegocioException(
                $"Estes exames já estão no atendimento {Numero}: {string.Join(", ", repetidos.Select(e => e.Nome))}.");

        GarantirPreparoConfirmado(novos, jejumConfirmado);

        var faltavaColetar = TemColetaPendente;

        if (jejumConfirmado)
            JejumConfirmado = true;

        var itens = novos.Select(e => new ItemAtendimento(e)).ToList();
        _itens.AddRange(itens);

        // Se a coleta anterior ja tinha terminado, o paciente precisa ser chamado de novo.
        // Manter a chamada antiga faria a fila mostrar que ele ainda esta na sala de coleta.
        if (!faltavaColetar)
            DataHoraChamada = null;

        AtualizarStatus();

        return itens;
    }

    /// <summary>
    /// Encerra o que ainda nao foi coletado.
    ///
    /// Paciente que desiste da fila ou que nao pode coletar deixaria o atendimento parado
    /// para sempre, contando tempo de espera no painel e bloqueando um pedido novo para a
    /// mesma pessoa. As amostras ja coletadas nao sao tocadas: sao tubos que existem na
    /// bancada e ainda precisam passar pela triagem.
    /// </summary>
    public void Cancelar(MotivoCancelamento motivo, string responsavel, DateTime agora)
    {
        if (Status == StatusAtendimento.Cancelado)
            throw new RegraDeNegocioException($"O atendimento {Numero} já está cancelado.");

        if (Status == StatusAtendimento.Concluido)
            throw new RegraDeNegocioException(
                $"O atendimento {Numero} já foi concluído e não pode ser cancelado.");

        var pendentes = _itens.Where(i => i.PrecisaDeColeta).ToList();

        if (pendentes.Count == 0)
            throw new RegraDeNegocioException(
                $"Os exames do atendimento {Numero} já foram coletados e os tubos estão na triagem. " +
                "Recuse os tubos na conferência, com o motivo de rejeição correspondente.");

        foreach (var item in pendentes)
            item.MarcarComo(StatusItemAtendimento.Cancelado);

        MotivoCancelamento = motivo;
        CanceladoPor = responsavel;
        DataHoraCancelamento = agora;

        AtualizarStatus();
    }

    /// <summary>Chama o paciente para a sala de coleta. Serve para o painel saber quem ja foi chamado.</summary>
    public void ChamarParaColeta(DateTime agora)
    {
        if (!_itens.Any(i => i.PrecisaDeColeta))
            throw new RegraDeNegocioException(
                $"O atendimento {Numero} não tem nenhum exame pendente de coleta.");

        DataHoraChamada = agora;
        AtualizarStatus();
    }

    /// <summary>
    /// Registra a coleta e gera os tubos.
    ///
    /// Esta e a regra central do sistema: o coletor nao tira um tubo por exame.
    /// Os exames pendentes sao agrupados pelo tubo que cada um exige (e pelo setor de destino,
    /// ja que nao ha aliquotagem neste recorte), e cada grupo vira uma amostra unica.
    /// Os tubos sao devolvidos na ordem de coleta para evitar contaminacao por aditivo.
    /// </summary>
    public IReadOnlyList<Amostra> RegistrarColeta(bool identificacaoConfirmada, string responsavel, DateTime agora)
    {
        if (!identificacaoConfirmada)
            throw new RegraDeNegocioException(
                "Confirme a identificação do paciente (nome completo e data de nascimento) antes de registrar a coleta. " +
                "A etiqueta só pode ser gerada com o paciente presente.");

        var pendentes = _itens.Where(i => i.PrecisaDeColeta).ToList();
        if (pendentes.Count == 0)
            throw new RegraDeNegocioException(
                $"O atendimento {Numero} não tem nenhum exame pendente de coleta.");

        var grupos = pendentes
            .GroupBy(i => new { i.Exame.TipoTuboId, i.Exame.SetorDestino })
            .OrderBy(g => g.First().Exame.TipoTubo.OrdemColeta)
            .ThenBy(g => g.Key.SetorDestino)
            .ToList();

        var novas = new List<Amostra>();
        var sequencia = _amostras.Count;

        foreach (var grupo in grupos)
        {
            sequencia++;
            var codigo = $"{Numero}-{sequencia:D2}";
            var amostra = new Amostra(codigo, grupo.First().Exame.TipoTubo, grupo, agora, responsavel);

            _amostras.Add(amostra);
            novas.Add(amostra);

            foreach (var item in grupo)
                item.MarcarComo(StatusItemAtendimento.Coletado);
        }

        DataHoraPrimeiraColeta ??= agora;
        AtualizarStatus();

        return novas;
    }

    public void AceitarAmostra(int amostraId, string responsavel, DateTime agora)
    {
        Buscar(amostraId).Aceitar(agora, responsavel);
        AtualizarStatus();
    }

    public void RejeitarAmostra(int amostraId, MotivoRejeicao motivo, string responsavel, string? observacao, DateTime agora)
    {
        Buscar(amostraId).Rejeitar(motivo, agora, responsavel, observacao);
        AtualizarStatus();
    }

    public void EncaminharAmostra(int amostraId, string responsavel, DateTime agora)
    {
        Buscar(amostraId).Encaminhar(agora, responsavel);
        AtualizarStatus();

        if (Status == StatusAtendimento.Concluido)
            DataHoraConclusao = agora;
    }

    private Amostra Buscar(int amostraId)
    {
        return _amostras.FirstOrDefault(a => a.Id == amostraId)
               ?? throw new RegraDeNegocioException($"Amostra {amostraId} não pertence ao atendimento {Numero}.");
    }

    /// <summary>
    /// O status do atendimento e sempre derivado dos itens, nunca definido na mao.
    /// Assim ele nao tem como divergir do que realmente aconteceu com os tubos.
    /// </summary>
    private void AtualizarStatus()
    {
        // Vem antes de concluido: um atendimento em que nada chegou a ser coletado nao foi
        // concluido, e chamar isso de "concluido" mentiria para quem le a fila.
        //
        // A condicao e ter sido cancelado E nao ter tubo nenhum. Exame descartado na triagem
        // tambem deixa o item cancelado, mas ali o paciente foi furado e o fluxo aconteceu:
        // esse atendimento termina como concluido.
        if (MotivoCancelamento is not null && _amostras.Count == 0)
        {
            Status = StatusAtendimento.Cancelado;
            return;
        }

        if (_itens.All(i => i.Status is StatusItemAtendimento.EmAnalise or StatusItemAtendimento.Cancelado))
        {
            Status = StatusAtendimento.Concluido;
            return;
        }

        // Pendencia vem antes da fila: e o caso que o operador precisa ver primeiro.
        if (_itens.Any(i => i.Status == StatusItemAtendimento.AguardandoRecoleta))
        {
            Status = StatusAtendimento.ComPendencia;
            return;
        }

        if (_itens.Any(i => i.Status == StatusItemAtendimento.AguardandoColeta))
        {
            Status = DataHoraChamada is null ? StatusAtendimento.AguardandoColeta : StatusAtendimento.EmColeta;
            return;
        }

        Status = StatusAtendimento.AguardandoTriagem;
    }

    /// <summary>
    /// Descobrir que faltou jejum depois da picada significa perder o tubo e chamar o
    /// paciente de volta outro dia, entao a conferencia acontece antes, na recepcao.
    /// Vale tanto no check-in quanto ao acrescentar exames a um pedido ja aberto.
    /// </summary>
    private static void GarantirPreparoConfirmado(IReadOnlyCollection<Exame> exames, bool jejumConfirmado)
    {
        var comJejum = exames.Where(e => e.ExigeJejum).ToList();

        if (comJejum.Count == 0 || jejumConfirmado)
            return;

        var nomes = string.Join(", ", comJejum.Select(e => $"{e.Nome} ({e.HorasJejum}h)"));

        throw new RegraDeNegocioException(
            $"Estes exames exigem jejum e o paciente não confirmou o preparo: {nomes}. " +
            "Oriente o paciente e reagende, ou remova os exames do pedido.");
    }
}
