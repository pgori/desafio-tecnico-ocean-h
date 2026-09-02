using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LabDesk.Infrastructure.Persistencia;

/// <summary>
/// Garante que toda data e hora entre e saia do banco em UTC.
///
/// Sem isso, o SQLite devolve a data sem fuso e o navegador exibe o horario errado,
/// o que num laboratorio e grave: os horarios de coleta e de triagem sao o registro
/// de rastreabilidade da amostra. O front converte para o fuso local na hora de mostrar.
/// </summary>
public class ConversorDataHoraUtc : ValueConverter<DateTime, DateTime>
{
    public ConversorDataHoraUtc()
        : base(
            valor => valor.ToUniversalTime(),
            valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc))
    {
    }
}
