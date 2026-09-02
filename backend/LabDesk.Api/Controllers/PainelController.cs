using LabDesk.Api.Contratos;
using LabDesk.Api.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace LabDesk.Api.Controllers;

/// <summary>Indicadores da operação do dia.</summary>
[ApiController]
[Route("api/painel")]
public class PainelController : ControllerBase
{
    private readonly PainelServico _servico;

    public PainelController(PainelServico servico) => _servico = servico;

    /// <summary>Fila, taxa de rejeição, motivos mais frequentes e tempos médios.</summary>
    [HttpGet]
    public Task<PainelDto> Obter(CancellationToken ct) => _servico.ObterAsync(ct);
}
