using LabDesk.Domain.Comum;

namespace LabDesk.Domain.Pacientes;

/// <summary>
/// Paciente atendido pelo laboratorio.
/// Nome completo e data de nascimento sao os dois identificadores usados para conferir
/// quem esta na cadeira antes de etiquetar o tubo.
/// </summary>
public class Paciente
{
    public int Id { get; private set; }

    public string NomeCompleto { get; private set; } = string.Empty;

    public DateOnly DataNascimento { get; private set; }

    public string Documento { get; private set; } = string.Empty;

    public string? Contato { get; private set; }

    private Paciente()
    {
    }

    public Paciente(string nomeCompleto, DateOnly dataNascimento, string documento, string? contato)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto))
            throw new RegraDeNegocioException("O nome completo do paciente é obrigatório.");

        if (string.IsNullOrWhiteSpace(documento))
            throw new RegraDeNegocioException("O documento do paciente é obrigatório.");

        if (dataNascimento > DateOnly.FromDateTime(DateTime.Today))
            throw new RegraDeNegocioException("A data de nascimento não pode estar no futuro.");

        NomeCompleto = nomeCompleto.Trim();
        DataNascimento = dataNascimento;
        Documento = documento.Trim();
        Contato = string.IsNullOrWhiteSpace(contato) ? null : contato.Trim();
    }

    /// <summary>Idade em anos completos, usada para sugerir atendimento preferencial.</summary>
    public int IdadeEm(DateOnly referencia)
    {
        var idade = referencia.Year - DataNascimento.Year;
        if (referencia < DataNascimento.AddYears(idade))
            idade--;

        return idade;
    }
}
