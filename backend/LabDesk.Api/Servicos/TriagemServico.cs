using LabDesk.Api.Comum;
using LabDesk.Api.Contratos;
using LabDesk.Domain.Amostras;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Comum;
using LabDesk.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace LabDesk.Api.Servicos;

/// <summary>
/// Casos de uso da bancada de triagem: conferir o tubo que chegou, aceitar ou recusar,
/// e encaminhar ao setor tecnico.
///
/// As acoes carregam o atendimento inteiro em vez da amostra isolada, porque rejeitar
/// um tubo muda a situacao dos exames e pode colocar o atendimento em pendencia de recoleta.
/// </summary>
public class TriagemServico
{
    private readonly LabDeskDbContext _db;
    private readonly ResponsavelAtual _responsavel;
    private readonly RelogioDoLaboratorio _relogio;

    public TriagemServico(LabDeskDbContext db, ResponsavelAtual responsavel, RelogioDoLaboratorio relogio)
    {
        _db = db;
        _responsavel = responsavel;
        _relogio = relogio;
    }

    public async Task<IReadOnlyList<AmostraDto>> ListarAsync(StatusAmostra? status, CancellationToken ct)
    {
        // Sem filtro, a bancada ve o que interessa a ela: os tubos ainda nao conferidos.
        var alvo = status ?? StatusAmostra.Coletada;

        var atendimentos = await _db.Atendimentos
            .AsNoTracking()
            .ComGrafoCompleto()
            .Where(a => a.Amostras.Any(am => am.Status == alvo))
            .ToListAsync(ct);

        return atendimentos
            .SelectMany(a => a.Amostras.Where(am => am.Status == alvo).Select(am => am.ParaDto(a)))
            .OrderBy(a => a.DataHoraColeta)
            .ToList();
    }

    public Task<AmostraDto> AceitarAsync(int amostraId, CancellationToken ct) =>
        ExecutarAsync(amostraId, (atendimento, agora) =>
            atendimento.AceitarAmostra(amostraId, _responsavel.Nome, agora), ct);

    public async Task<AmostraDto> RejeitarAsync(int amostraId, RejeitarAmostraRequest requisicao, CancellationToken ct)
    {
        var motivo = await _db.MotivosRejeicao.FirstOrDefaultAsync(m => m.Id == requisicao.MotivoRejeicaoId, ct)
                     ?? throw new RegraDeNegocioException("Motivo de rejeição não encontrado.");

        return await ExecutarAsync(amostraId, (atendimento, agora) =>
            atendimento.RejeitarAmostra(amostraId, motivo, _responsavel.Nome, requisicao.Observacao, agora), ct);
    }

    public Task<AmostraDto> EncaminharAsync(int amostraId, CancellationToken ct) =>
        ExecutarAsync(amostraId, (atendimento, agora) =>
            atendimento.EncaminharAmostra(amostraId, _responsavel.Nome, agora), ct);

    private async Task<AmostraDto> ExecutarAsync(int amostraId, Action<Atendimento, DateTime> acao, CancellationToken ct)
    {
        var atendimento = await _db.Atendimentos
            .ComGrafoCompleto()
            .FirstOrDefaultAsync(a => a.Amostras.Any(am => am.Id == amostraId), ct)
            ?? throw new RegraDeNegocioException($"Amostra {amostraId} não encontrada.");

        acao(atendimento, _relogio.AgoraUtc);
        await _db.SaveChangesAsync(ct);

        return atendimento.Amostras.First(a => a.Id == amostraId).ParaDto(atendimento);
    }
}
