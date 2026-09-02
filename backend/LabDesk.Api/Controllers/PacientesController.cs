using LabDesk.Api.Contratos;
using LabDesk.Api.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace LabDesk.Api.Controllers;

/// <summary>Cadastro de pacientes usado pela recepção no check-in.</summary>
[ApiController]
[Route("api/pacientes")]
public class PacientesController : ControllerBase
{
    private readonly CadastroServico _servico;

    public PacientesController(CadastroServico servico) => _servico = servico;

    /// <summary>Busca paciente por nome ou documento, para a recepção não duplicar cadastro.</summary>
    [HttpGet]
    public Task<IReadOnlyList<PacienteDto>> Buscar([FromQuery] string? busca, CancellationToken ct) =>
        _servico.BuscarPacientesAsync(busca, ct);

    /// <summary>Cadastra um paciente novo. O documento não pode repetir.</summary>
    [HttpPost]
    public async Task<ActionResult<PacienteDto>> Cadastrar(CriarPacienteRequest requisicao, CancellationToken ct)
    {
        var paciente = await _servico.CadastrarPacienteAsync(requisicao, ct);
        return CreatedAtAction(nameof(Buscar), new { busca = paciente.Documento }, paciente);
    }
}
