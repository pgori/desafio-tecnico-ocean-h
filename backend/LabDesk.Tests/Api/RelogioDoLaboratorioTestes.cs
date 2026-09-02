using FluentAssertions;
using LabDesk.Api.Comum;
using Microsoft.Extensions.Configuration;

namespace LabDesk.Tests.Api;

/// <summary>
/// O painel do dia depende de saber onde o dia comeca.
/// Usar a meia-noite UTC faria os indicadores zerarem as 21h de Brasilia,
/// no meio do turno da tarde.
/// </summary>
public class RelogioDoLaboratorioTestes
{
    [Fact]
    public void O_dia_comeca_a_meia_noite_do_fuso_do_laboratorio()
    {
        var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var relogio = Criar("America/Sao_Paulo");

        var inicioLocal = TimeZoneInfo.ConvertTimeFromUtc(relogio.InicioDoDiaUtc, fuso);

        inicioLocal.TimeOfDay.Should().Be(TimeSpan.Zero);
        DateOnly.FromDateTime(inicioLocal).Should().Be(relogio.Hoje);
    }

    [Fact]
    public void Cai_para_UTC_quando_o_fuso_configurado_nao_existe_no_sistema()
    {
        // Windows e Linux usam bancos de fuso diferentes; a aplicacao nao pode
        // deixar de subir por causa disso.
        var relogio = Criar("Fuso/Inexistente");

        relogio.InicioDoDiaUtc.Should().Be(relogio.AgoraUtc.Date);
    }

    private static RelogioDoLaboratorio Criar(string fuso)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Laboratorio:FusoHorario"] = fuso })
            .Build();

        return new RelogioDoLaboratorio(configuracao);
    }
}
