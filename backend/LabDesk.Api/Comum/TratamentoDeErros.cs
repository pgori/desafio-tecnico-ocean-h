using LabDesk.Domain.Comum;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LabDesk.Api.Comum;

/// <summary>
/// Converte excecao de regra de negocio em HTTP 400 com a mensagem original.
///
/// As mensagens do dominio foram escritas para o operador ler na tela
/// ("confirme a identificacao antes de registrar a coleta"), entao vale a pena
/// repassa-las direto em vez de trocar por um texto generico.
/// </summary>
public class TratamentoDeErros : IExceptionHandler
{
    private readonly ILogger<TratamentoDeErros> _log;

    public TratamentoDeErros(ILogger<TratamentoDeErros> log) => _log = log;

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken ct)
    {
        if (excecao is not RegraDeNegocioException regra)
        {
            _log.LogError(excecao, "Falha nao tratada em {Rota}", contexto.Request.Path);
            return false;
        }

        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Operação não permitida",
            Detail = regra.Message
        };

        contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
        await contexto.Response.WriteAsJsonAsync(problema, ct);

        return true;
    }
}
