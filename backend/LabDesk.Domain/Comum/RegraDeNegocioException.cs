namespace LabDesk.Domain.Comum;

/// <summary>
/// Erro de regra de negocio do laboratorio (ex.: tentar triar uma amostra que ainda nao foi coletada).
/// A API traduz essa excecao em HTTP 400 com a mensagem para o operador.
/// </summary>
public class RegraDeNegocioException : Exception
{
    public RegraDeNegocioException(string mensagem) : base(mensagem)
    {
    }
}
