using LabDesk.Api.Contratos;
using LabDesk.Api.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace LabDesk.Api.Controllers;

/// <summary>Catálogo do laboratório: exames disponíveis e motivos de rejeição da triagem.</summary>
[ApiController]
[Route("api")]
public class CatalogoController : ControllerBase
{
    private readonly CadastroServico _servico;

    public CatalogoController(CadastroServico servico) => _servico = servico;

    /// <summary>Exames que o laboratório oferece, com o tubo e o preparo de cada um.</summary>
    [HttpGet("exames")]
    public Task<IReadOnlyList<ExameDto>> ListarExames(CancellationToken ct) =>
        _servico.ListarExamesAsync(ct);

    /// <summary>Motivos padronizados para recusar uma amostra na conferência.</summary>
    [HttpGet("motivos-rejeicao")]
    public Task<IReadOnlyList<MotivoRejeicaoDto>> ListarMotivosRejeicao(CancellationToken ct) =>
        _servico.ListarMotivosRejeicaoAsync(ct);
}
