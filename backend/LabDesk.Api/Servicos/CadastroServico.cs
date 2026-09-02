using LabDesk.Api.Comum;
using LabDesk.Api.Contratos;
using LabDesk.Domain.Comum;
using LabDesk.Domain.Pacientes;
using LabDesk.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace LabDesk.Api.Servicos;

/// <summary>Consultas de catalogo e cadastro de paciente usadas pela recepcao.</summary>
public class CadastroServico
{
    private readonly LabDeskDbContext _db;
    private readonly RelogioDoLaboratorio _relogio;

    public CadastroServico(LabDeskDbContext db, RelogioDoLaboratorio relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    public async Task<IReadOnlyList<ExameDto>> ListarExamesAsync(CancellationToken ct)
    {
        var exames = await _db.Exames
            .AsNoTracking()
            .Include(e => e.TipoTubo)
            .OrderBy(e => e.SetorDestino)
            .ThenBy(e => e.Nome)
            .ToListAsync(ct);

        return exames.Select(e => e.ParaDto()).ToList();
    }

    public async Task<IReadOnlyList<MotivoRejeicaoDto>> ListarMotivosRejeicaoAsync(CancellationToken ct)
    {
        var motivos = await _db.MotivosRejeicao
            .AsNoTracking()
            .OrderBy(m => m.Descricao)
            .ToListAsync(ct);

        return motivos.Select(m => m.ParaDto()).ToList();
    }

    public async Task<IReadOnlyList<PacienteDto>> BuscarPacientesAsync(string? busca, CancellationToken ct)
    {
        var consulta = _db.Pacientes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            consulta = consulta.Where(p =>
                EF.Functions.Like(p.NomeCompleto, $"%{termo}%") ||
                EF.Functions.Like(p.Documento, $"%{termo}%"));
        }

        var pacientes = await consulta
            .OrderBy(p => p.NomeCompleto)
            .Take(30)
            .ToListAsync(ct);

        return pacientes.Select(p => p.ParaDto(_relogio.Hoje)).ToList();
    }

    public async Task<PacienteDto> CadastrarPacienteAsync(CriarPacienteRequest requisicao, CancellationToken ct)
    {
        var documento = requisicao.Documento?.Trim() ?? string.Empty;

        // Documento repetido quase sempre significa que a recepcao nao achou o cadastro
        // que ja existia. Duplicar paciente e o comeco de uma troca de amostra.
        if (await _db.Pacientes.AnyAsync(p => p.Documento == documento, ct))
            throw new RegraDeNegocioException("Já existe um paciente cadastrado com este documento.");

        var paciente = new Paciente(
            requisicao.NomeCompleto,
            requisicao.DataNascimento,
            documento,
            requisicao.Contato);

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(ct);

        return paciente.ParaDto(_relogio.Hoje);
    }
}
