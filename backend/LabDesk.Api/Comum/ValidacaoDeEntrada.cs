using Microsoft.AspNetCore.Mvc;

namespace LabDesk.Api.Comum;

/// <summary>
/// Traduz a validacao automatica do ASP.NET para uma mensagem que o operador entende.
///
/// O padrao responde "One or more validation errors occurred." com os nomes das
/// propriedades do DTO e o tipo .NET que falhou na conversao. Isso e diagnostico de
/// desenvolvedor, nao aviso de tela: quem esta na recepcao precisa saber qual campo
/// preencher, em portugues.
/// </summary>
public static class ValidacaoDeEntrada
{
    /// <summary>
    /// Nome do campo no JSON para o rotulo que aparece na tela. O que nao estiver aqui
    /// cai no proprio nome do campo, que e melhor do que nao dizer nada.
    /// </summary>
    private static readonly Dictionary<string, string> RotulosDosCampos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nomeCompleto"] = "Nome completo",
        ["dataNascimento"] = "Data de nascimento",
        ["documento"] = "Documento",
        ["contato"] = "Contato",
        ["pacienteId"] = "Paciente",
        ["exameIds"] = "Exames solicitados",
        ["prioridade"] = "Prioridade",
        ["jejumConfirmado"] = "Confirmação do jejum",
        ["observacoes"] = "Observações",
        ["observacao"] = "Observação",
        ["identificacaoConfirmada"] = "Confirmação da identificação",
        ["motivoRejeicaoId"] = "Motivo da rejeição",
        ["motivo"] = "Motivo do cancelamento",
        ["filtro"] = "Filtro da fila"
    };

    public static IActionResult Responder(ActionContext contexto)
    {
        // Quando o corpo inteiro falha em desserializar, o ASP.NET marca tambem o parametro
        // do metodo ("requisicao") como invalido. Esse nome nao existe para quem usa a tela.
        var parametrosDoMetodo = contexto.ActionDescriptor.Parameters
            .Select(parametro => parametro.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var campos = contexto.ModelState
            .Where(entrada => entrada.Value?.Errors.Count > 0)
            .Select(entrada => NomeDoCampo(entrada.Key))
            .Where(campo => campo.Length > 0 && !parametrosDoMetodo.Contains(campo))
            .Select(Rotular)
            .Distinct()
            .ToList();

        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dados inválidos",
            Detail = Descrever(campos)
        };

        return new BadRequestObjectResult(problema);
    }

    private static string Descrever(IReadOnlyList<string> campos) => campos.Count switch
    {
        // Sobra quando nem os campos foram identificados, por exemplo com o corpo malformado.
        0 => "Não foi possível ler os dados enviados. Confira o preenchimento e tente novamente.",
        1 => $"Preencha corretamente o campo {campos[0]}.",
        _ => $"Preencha corretamente os campos: {string.Join(", ", campos)}."
    };

    /// <summary>
    /// Erro de desserializacao do JSON chega com a chave no formato "$.dataNascimento";
    /// erro de validacao chega com o nome da propriedade. Os dois viram o mesmo campo.
    /// </summary>
    private static string NomeDoCampo(string chave) =>
        chave.TrimStart('$', '.').Split('.').Last();

    private static string Rotular(string campo) =>
        RotulosDosCampos.TryGetValue(campo, out var rotulo) ? rotulo : campo;
}
