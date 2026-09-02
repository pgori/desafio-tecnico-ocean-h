using LabDesk.Api.Contratos;
using LabDesk.Api.Servicos;
using LabDesk.Domain.Atendimentos;
using Microsoft.AspNetCore.Mvc;

namespace LabDesk.Api.Controllers;

/// <summary>
/// Atendimento do paciente: check-in na recepção, fila de espera e registro da coleta.
/// </summary>
[ApiController]
[Route("api/atendimentos")]
public class AtendimentosController : ControllerBase
{
    private readonly AtendimentoServico _servico;

    public AtendimentosController(AtendimentoServico servico) => _servico = servico;

    /// <summary>
    /// Fila de atendimento do dia. Sem filtro, traz quem ainda tem tubo a coletar.
    /// O que ficou de dias anteriores só continua aparecendo se a coleta não aconteceu.
    /// </summary>
    [HttpGet]
    public Task<IReadOnlyList<AtendimentoResumoDto>> Listar(
        [FromQuery] FiltroDaFila filtro = FiltroDaFila.AColetar,
        CancellationToken ct = default) => _servico.ListarAsync(filtro, ct);

    /// <summary>Detalhe do atendimento: paciente, exames pedidos e amostras coletadas.</summary>
    [HttpGet("{id:int}")]
    public Task<AtendimentoDetalheDto> Obter(int id, CancellationToken ct) =>
        _servico.ObterAsync(id, ct);

    /// <summary>Check-in: abre o atendimento com os exames pedidos e confere o preparo do paciente.</summary>
    [HttpPost]
    public async Task<ActionResult<AtendimentoDetalheDto>> Abrir(AbrirAtendimentoRequest requisicao, CancellationToken ct)
    {
        var atendimento = await _servico.AbrirAsync(requisicao, ct);
        return CreatedAtAction(nameof(Obter), new { id = atendimento.Id }, atendimento);
    }

    /// <summary>
    /// Acrescenta exames a um atendimento já aberto, para o paciente que chegou com uma
    /// segunda requisição. Evita abrir um segundo pedido e furar o paciente duas vezes.
    /// </summary>
    [HttpPost("{id:int}/exames")]
    public Task<AtendimentoDetalheDto> AdicionarExames(int id, AdicionarExamesRequest requisicao, CancellationToken ct) =>
        _servico.AdicionarExamesAsync(id, requisicao, ct);

    /// <summary>
    /// Cancela os exames que ainda não foram coletados, com motivo padronizado.
    /// Amostras já coletadas continuam valendo e seguem para a triagem.
    /// </summary>
    [HttpPost("{id:int}/cancelar")]
    public Task<AtendimentoDetalheDto> Cancelar(int id, CancelarAtendimentoRequest requisicao, CancellationToken ct) =>
        _servico.CancelarAsync(id, requisicao, ct);

    /// <summary>Chama o paciente da fila para a sala de coleta.</summary>
    [HttpPost("{id:int}/chamar")]
    public Task<AtendimentoDetalheDto> Chamar(int id, CancellationToken ct) =>
        _servico.ChamarParaColetaAsync(id, ct);

    /// <summary>Tubos que devem ser coletados, já agrupados e na ordem de coleta.</summary>
    [HttpGet("{id:int}/tubos-previstos")]
    public Task<IReadOnlyList<TuboPrevistoDto>> PreverTubos(int id, CancellationToken ct) =>
        _servico.PreverTubosAsync(id, ct);

    /// <summary>Registra a coleta e gera as amostras etiquetadas.</summary>
    [HttpPost("{id:int}/coleta")]
    public Task<AtendimentoDetalheDto> RegistrarColeta(int id, RegistrarColetaRequest requisicao, CancellationToken ct) =>
        _servico.RegistrarColetaAsync(id, requisicao, ct);
}
