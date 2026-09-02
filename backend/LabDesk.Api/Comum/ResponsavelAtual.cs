using LabDesk.Domain.Comum;

namespace LabDesk.Api.Comum;

/// <summary>
/// Quem esta operando o sistema agora. Vem do cabecalho <c>X-Responsavel</c>,
/// preenchido pelo seletor de responsavel da tela.
///
/// Este e o ponto onde entraria a autenticacao de verdade: o nome passaria a vir
/// do usuario logado e nada mais no codigo mudaria. Login ficou fora do escopo,
/// mas registrar quem fez cada acao nao pode ficar, porque rastreabilidade
/// e requisito do laboratorio, nao de seguranca.
/// </summary>
public class ResponsavelAtual
{
    public const string NomeDoCabecalho = "X-Responsavel";

    private readonly IHttpContextAccessor _contexto;

    public ResponsavelAtual(IHttpContextAccessor contexto) => _contexto = contexto;

    public string Nome
    {
        get
        {
            var valor = _contexto.HttpContext?.Request.Headers[NomeDoCabecalho].ToString();

            if (string.IsNullOrWhiteSpace(valor))
                throw new RegraDeNegocioException(
                    $"Informe quem está executando a ação no cabeçalho {NomeDoCabecalho}. " +
                    "Toda etapa do fluxo precisa ficar registrada com um responsável.");

            // Cabecalho HTTP so trafega ASCII, e nome de pessoa tem acento ("Joao", "Conceicao").
            // O cliente envia percent-encoded; sem isso o servidor recusa a requisicao inteira
            // antes de ela chegar aqui. Valor ja em ASCII passa intacto pela decodificacao.
            return Uri.UnescapeDataString(valor).Trim();
        }
    }
}
