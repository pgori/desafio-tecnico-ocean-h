namespace LabDesk.Api.Comum;

/// <summary>
/// O relogio do laboratorio.
///
/// Os registros sao gravados em UTC, mas "hoje" para a operacao e o dia do fuso local.
/// Sem essa distincao o painel do dia zera no meio do turno da tarde, porque em Brasilia
/// a meia-noite UTC cai as 21h. O fuso e configuravel para o sistema nao ficar preso
/// a uma unica unidade.
/// </summary>
public class RelogioDoLaboratorio
{
    private readonly TimeZoneInfo _fuso;

    public RelogioDoLaboratorio(IConfiguration configuracao)
    {
        var id = configuracao["Laboratorio:FusoHorario"] ?? "America/Sao_Paulo";

        _fuso = BuscarFuso(id);
    }

    public DateTime AgoraUtc => DateTime.UtcNow;

    /// <summary>Data corrente no fuso do laboratorio. Usada para calcular idade e filtrar o dia.</summary>
    public DateOnly Hoje => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(AgoraUtc, _fuso));

    /// <summary>Instante em UTC que corresponde a meia-noite local. E o corte do painel do dia.</summary>
    public DateTime InicioDoDiaUtc
    {
        get
        {
            var meiaNoiteLocal = Hoje.ToDateTime(TimeOnly.MinValue);
            return TimeZoneInfo.ConvertTimeToUtc(meiaNoiteLocal, _fuso);
        }
    }

    /// <summary>
    /// Windows e Linux usam bancos de fuso diferentes. Em ambientes sem a base IANA
    /// o identificador nao e encontrado, e nesse caso o UTC evita derrubar a aplicacao.
    /// </summary>
    private static TimeZoneInfo BuscarFuso(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
