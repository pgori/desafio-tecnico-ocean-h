using LabDesk.Api.Comum;
using LabDesk.Api.Contratos;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Comum;
using LabDesk.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace LabDesk.Api.Servicos;

/// <summary>
/// Casos de uso da recepcao e da sala de coleta.
/// O servico so orquestra: busca o que precisa, chama o metodo do dominio e salva.
/// Nenhuma regra de laboratorio mora aqui.
/// </summary>
public class AtendimentoServico
{
    private readonly LabDeskDbContext _db;
    private readonly ResponsavelAtual _responsavel;
    private readonly RelogioDoLaboratorio _relogio;

    public AtendimentoServico(LabDeskDbContext db, ResponsavelAtual responsavel, RelogioDoLaboratorio relogio)
    {
        _db = db;
        _responsavel = responsavel;
        _relogio = relogio;
    }

    public async Task<IReadOnlyList<AtendimentoResumoDto>> ListarAsync(FiltroDaFila filtro, CancellationToken ct)
    {
        var agora = _relogio.AgoraUtc;

        var consulta = _db.Atendimentos
            .AsNoTracking()
            .Include(a => a.Paciente)
            .Include(a => a.Itens)
            .Include(a => a.Amostras)
            .AsQueryable();

        // A fila e a lista de trabalho do dia, e nao o historico do laboratorio: sem esse
        // corte ela so cresceria e a consulta carregaria todos os atendimentos ja feitos.
        // O que ficou de ontem continua aparecendo enquanto tiver tubo a coletar, senao um
        // atendimento abandonado sumiria da tela sem que ninguem pudesse cancela-lo.
        consulta = consulta.Where(a =>
            a.DataHoraChegada >= _relogio.InicioDoDiaUtc ||
            a.Itens.Any(i => i.Status == StatusItemAtendimento.AguardandoColeta
                          || i.Status == StatusItemAtendimento.AguardandoRecoleta));

        consulta = filtro switch
        {
            FiltroDaFila.AColetar => consulta.Where(a =>
                a.Itens.Any(i => i.Status == StatusItemAtendimento.AguardandoColeta
                              || i.Status == StatusItemAtendimento.AguardandoRecoleta)),
            FiltroDaFila.ComPendencia => consulta.Where(a => a.Status == StatusAtendimento.ComPendencia),
            FiltroDaFila.EmTriagem => consulta.Where(a => a.Status == StatusAtendimento.AguardandoTriagem),
            FiltroDaFila.Concluidos => consulta.Where(a => a.Status == StatusAtendimento.Concluido),
            FiltroDaFila.Cancelados => consulta.Where(a => a.Status == StatusAtendimento.Cancelado),
            _ => consulta
        };

        var atendimentos = await consulta.ToListAsync(ct);

        // A ordenacao da fila e a regra de atendimento da recepcao:
        // pendencia de recoleta primeiro (o paciente ja esta esperando ha mais tempo),
        // depois prioridade legal, depois ordem de chegada.
        return atendimentos
            .OrderByDescending(a => a.Status == StatusAtendimento.ComPendencia)
            .ThenByDescending(a => a.Prioridade)
            .ThenBy(a => a.DataHoraChegada)
            .Select(a => a.ParaResumoDto(agora, _relogio.Hoje))
            .ToList();
    }

    public async Task<AtendimentoDetalheDto> ObterAsync(int id, CancellationToken ct)
    {
        var atendimento = await CarregarAsync(id, ct);
        return atendimento.ParaDetalheDto(_relogio.Hoje);
    }

    public async Task<AtendimentoDetalheDto> AbrirAsync(AbrirAtendimentoRequest requisicao, CancellationToken ct)
    {
        var agora = _relogio.AgoraUtc;

        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == requisicao.PacienteId, ct)
                       ?? throw new RegraDeNegocioException("Paciente não encontrado.");

        // Dois atendimentos abertos para a mesma pessoa agrupam os tubos separadamente,
        // e o paciente sai com dois tubos do mesmo aditivo onde um resolveria.
        var comColetaPendente = await _db.Atendimentos
            .AsNoTracking()
            .Include(a => a.Itens)
            .Where(a => a.PacienteId == requisicao.PacienteId)
            .Where(a => a.Itens.Any(i => i.Status == StatusItemAtendimento.AguardandoColeta
                                      || i.Status == StatusItemAtendimento.AguardandoRecoleta))
            .ToListAsync(ct);

        Atendimento.GarantirQueNaoHaColetaPendente(comColetaPendente);

        var exames = await BuscarExamesAsync(requisicao.ExameIds, ct);

        var numero = await GerarNumeroAsync(agora, ct);

        var atendimento = new Atendimento(
            numero,
            paciente,
            exames,
            requisicao.Prioridade,
            requisicao.JejumConfirmado,
            requisicao.Observacoes,
            agora);

        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync(ct);

        return atendimento.ParaDetalheDto(_relogio.Hoje);
    }

    /// <summary>
    /// Paciente que chega com uma segunda requisicao depois do check-in. Os exames entram
    /// no atendimento que ja existe para sairem nos mesmos tubos da coleta que vem.
    /// </summary>
    public async Task<AtendimentoDetalheDto> AdicionarExamesAsync(int id, AdicionarExamesRequest requisicao, CancellationToken ct)
    {
        var atendimento = await CarregarAsync(id, ct);
        var exames = await BuscarExamesAsync(requisicao.ExameIds, ct);

        atendimento.AdicionarExames(exames, requisicao.JejumConfirmado, _relogio.AgoraUtc);
        await _db.SaveChangesAsync(ct);

        return atendimento.ParaDetalheDto(_relogio.Hoje);
    }

    public async Task<AtendimentoDetalheDto> CancelarAsync(int id, CancelarAtendimentoRequest requisicao, CancellationToken ct)
    {
        var atendimento = await CarregarAsync(id, ct);

        atendimento.Cancelar(requisicao.Motivo, _responsavel.Nome, _relogio.AgoraUtc);
        await _db.SaveChangesAsync(ct);

        return atendimento.ParaDetalheDto(_relogio.Hoje);
    }

    public async Task<AtendimentoDetalheDto> ChamarParaColetaAsync(int id, CancellationToken ct)
    {
        var atendimento = await CarregarAsync(id, ct);

        atendimento.ChamarParaColeta(_relogio.AgoraUtc);
        await _db.SaveChangesAsync(ct);

        return atendimento.ParaDetalheDto(_relogio.Hoje);
    }

    /// <summary>
    /// Previa dos tubos antes da puncao. Usa o mesmo agrupamento da coleta real,
    /// porque o coletor precisa ver exatamente o que vai sair.
    /// </summary>
    public async Task<IReadOnlyList<TuboPrevistoDto>> PreverTubosAsync(int id, CancellationToken ct)
    {
        var atendimento = await CarregarAsync(id, ct);

        return atendimento.Itens
            .Where(i => i.Status is StatusItemAtendimento.AguardandoColeta or StatusItemAtendimento.AguardandoRecoleta)
            .GroupBy(i => new { i.Exame.TipoTuboId, i.Exame.SetorDestino })
            .OrderBy(g => g.First().Exame.TipoTubo.OrdemColeta)
            .ThenBy(g => g.Key.SetorDestino)
            .Select(g => new TuboPrevistoDto(
                g.First().Exame.TipoTubo.Cor,
                g.First().Exame.TipoTubo.Aditivo,
                g.First().Exame.TipoTubo.OrdemColeta,
                g.First().Exame.TipoTubo.VolumeMinimoMl,
                g.Key.SetorDestino,
                g.Select(i => $"{i.Exame.Codigo} - {i.Exame.Nome}").ToList()))
            .ToList();
    }

    public async Task<AtendimentoDetalheDto> RegistrarColetaAsync(int id, RegistrarColetaRequest requisicao, CancellationToken ct)
    {
        var atendimento = await CarregarAsync(id, ct);

        atendimento.RegistrarColeta(requisicao.IdentificacaoConfirmada, _responsavel.Nome, _relogio.AgoraUtc);
        await _db.SaveChangesAsync(ct);

        return atendimento.ParaDetalheDto(_relogio.Hoje);
    }

    private async Task<List<Exame>> BuscarExamesAsync(IReadOnlyList<int> exameIds, CancellationToken ct)
    {
        var exames = await _db.Exames
            .Include(e => e.TipoTubo)
            .Where(e => exameIds.Contains(e.Id))
            .ToListAsync(ct);

        if (exames.Count != exameIds.Distinct().Count())
            throw new RegraDeNegocioException("Um ou mais exames do pedido não existem no catálogo.");

        return exames;
    }

    /// <summary>
    /// Numero visivel do atendimento, reiniciado a cada dia (AAAAMMDD-0001).
    /// E o formato que o balcao usa para chamar o paciente e localizar o pedido.
    /// </summary>
    private async Task<string> GerarNumeroAsync(DateTime agora, CancellationToken ct)
    {
        // O numero segue o dia local: e o que a recepcao le em voz alta para chamar o paciente.
        var prefixo = _relogio.Hoje.ToString("yyyyMMdd");

        var emitidosHoje = await _db.Atendimentos
            .CountAsync(a => a.Numero.StartsWith(prefixo), ct);

        return $"{prefixo}-{emitidosHoje + 1:D4}";
    }

    /// <summary>
    /// Carrega o atendimento inteiro. As operacoes de coleta e triagem precisam do grafo
    /// completo porque uma acao em um tubo altera os exames e o proprio atendimento.
    /// </summary>
    private async Task<Atendimento> CarregarAsync(int id, CancellationToken ct)
    {
        var atendimento = await _db.Atendimentos
            .ComGrafoCompleto()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return atendimento ?? throw new RegraDeNegocioException($"Atendimento {id} não encontrado.");
    }
}

public static class ConsultasAtendimento
{
    public static IQueryable<Atendimento> ComGrafoCompleto(this IQueryable<Atendimento> consulta) =>
        consulta
            .Include(a => a.Paciente)
            .Include(a => a.Itens).ThenInclude(i => i.Exame).ThenInclude(e => e.TipoTubo)
            .Include(a => a.Amostras).ThenInclude(am => am.TipoTubo)
            .Include(a => a.Amostras).ThenInclude(am => am.MotivoRejeicao)
            .Include(a => a.Amostras).ThenInclude(am => am.Eventos)
            .Include(a => a.Amostras).ThenInclude(am => am.Itens).ThenInclude(i => i.Exame);
}
