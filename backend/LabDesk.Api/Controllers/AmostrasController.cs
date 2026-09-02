using LabDesk.Api.Contratos;
using LabDesk.Api.Servicos;
using LabDesk.Domain.Amostras;
using Microsoft.AspNetCore.Mvc;

namespace LabDesk.Api.Controllers;

/// <summary>Bancada de triagem: conferência dos tubos e encaminhamento aos setores.</summary>
[ApiController]
[Route("api/amostras")]
public class AmostrasController : ControllerBase
{
    private readonly TriagemServico _servico;

    public AmostrasController(TriagemServico servico) => _servico = servico;

    /// <summary>Amostras por situação. Sem filtro, traz as que ainda esperam conferência.</summary>
    [HttpGet]
    public Task<IReadOnlyList<AmostraDto>> Listar([FromQuery] StatusAmostra? status, CancellationToken ct) =>
        _servico.ListarAsync(status, ct);

    /// <summary>Aprova a amostra na conferência.</summary>
    [HttpPost("{id:int}/aceitar")]
    public Task<AmostraDto> Aceitar(int id, CancellationToken ct) =>
        _servico.AceitarAsync(id, ct);

    /// <summary>Recusa a amostra com um motivo padronizado. Gera recoleta quando o motivo exige.</summary>
    [HttpPost("{id:int}/rejeitar")]
    public Task<AmostraDto> Rejeitar(int id, RejeitarAmostraRequest requisicao, CancellationToken ct) =>
        _servico.RejeitarAsync(id, requisicao, ct);

    /// <summary>Entrega a amostra ao setor técnico. Encerra o fluxo pré-analítico.</summary>
    [HttpPost("{id:int}/encaminhar")]
    public Task<AmostraDto> Encaminhar(int id, CancellationToken ct) =>
        _servico.EncaminharAsync(id, ct);
}
