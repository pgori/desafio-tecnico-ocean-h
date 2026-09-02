using LabDesk.Api.Comum;
using LabDesk.Api.Contratos;
using LabDesk.Domain.Amostras;
using LabDesk.Domain.Atendimentos;
using LabDesk.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace LabDesk.Api.Servicos;

/// <summary>
/// Indicadores da operacao do dia.
///
/// A taxa de rejeicao e o tempo de espera sao os dois numeros que um laboratorio
/// acompanha de perto na fase pre-analitica: o primeiro mostra quanto retrabalho
/// esta sendo gerado, o segundo mostra se a fila esta andando.
/// </summary>
public class PainelServico
{
    private readonly LabDeskDbContext _db;
    private readonly RelogioDoLaboratorio _relogio;

    public PainelServico(LabDeskDbContext db, RelogioDoLaboratorio relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    public async Task<PainelDto> ObterAsync(CancellationToken ct)
    {
        // O corte do dia e a meia-noite local do laboratorio, nao a meia-noite UTC.
        var inicioDoDia = _relogio.InicioDoDiaUtc;

        var atendimentos = await _db.Atendimentos
            .AsNoTracking()
            .Include(a => a.Amostras).ThenInclude(am => am.MotivoRejeicao)
            .ToListAsync(ct);

        var doDia = atendimentos.Where(a => a.DataHoraChegada >= inicioDoDia).ToList();
        var amostrasDoDia = doDia.SelectMany(a => a.Amostras).ToList();

        var triadasHoje = amostrasDoDia
            .Where(a => a.DataHoraTriagem >= inicioDoDia)
            .ToList();

        var rejeitadasHoje = triadasHoje
            .Where(a => a.Status == StatusAmostra.Rejeitada)
            .ToList();

        var esperas = doDia
            .Where(a => a.DataHoraPrimeiraColeta is not null)
            .Select(a => (a.DataHoraPrimeiraColeta!.Value - a.DataHoraChegada).TotalMinutes)
            .ToList();

        var triagens = triadasHoje
            .Select(a => (a.DataHoraTriagem!.Value - a.DataHoraColeta).TotalMinutes)
            .ToList();

        var motivos = rejeitadasHoje
            .Where(a => a.MotivoRejeicao is not null)
            .GroupBy(a => a.MotivoRejeicao!.Descricao)
            .Select(g => new MotivoFrequenteDto(g.Key, g.Count()))
            .OrderByDescending(m => m.Quantidade)
            .ToList();

        return new PainelDto(
            AguardandoColeta: atendimentos.Count(a => a.Status == StatusAtendimento.AguardandoColeta),
            EmColeta: atendimentos.Count(a => a.Status == StatusAtendimento.EmColeta),
            AguardandoTriagem: atendimentos.Count(a => a.Status == StatusAtendimento.AguardandoTriagem),
            ComPendencia: atendimentos.Count(a => a.Status == StatusAtendimento.ComPendencia),
            ConcluidosHoje: doDia.Count(a => a.Status == StatusAtendimento.Concluido),
            AmostrasAguardandoTriagem: atendimentos
                .SelectMany(a => a.Amostras)
                .Count(a => a.Status == StatusAmostra.Coletada),
            AmostrasTriadasHoje: triadasHoje.Count,
            AmostrasRejeitadasHoje: rejeitadasHoje.Count,
            TaxaRejeicaoPercentual: triadasHoje.Count == 0
                ? 0
                : Math.Round(rejeitadasHoje.Count * 100.0 / triadasHoje.Count, 1),
            TempoMedioEsperaMinutos: esperas.Count == 0 ? null : (int)Math.Round(esperas.Average()),
            TempoMedioTriagemMinutos: triagens.Count == 0 ? null : (int)Math.Round(triagens.Average()),
            MotivosMaisFrequentes: motivos);
    }
}
