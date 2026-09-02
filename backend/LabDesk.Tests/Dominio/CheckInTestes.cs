using FluentAssertions;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Comum;

namespace LabDesk.Tests.Dominio;

/// <summary>
/// Check-in na recepcao. O ponto critico aqui e o preparo do paciente:
/// deixar passar um exame de jejum significa perder a amostra depois da coleta.
/// </summary>
public class CheckInTestes
{
    private readonly CenarioDeLaboratorio _lab = new();
    private readonly DateTime _agora = new(2026, 3, 10, 7, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Abre_o_atendimento_com_um_item_por_exame_pedido()
    {
        var atendimento = Abrir([_lab.Hemograma, _lab.Creatinina], jejumConfirmado: false);

        atendimento.Itens.Should().HaveCount(2);
        atendimento.Itens.Should().OnlyContain(i => i.Status == StatusItemAtendimento.AguardandoColeta);
        atendimento.Status.Should().Be(StatusAtendimento.AguardandoColeta);
        atendimento.DataHoraChegada.Should().Be(_agora);
    }

    [Fact]
    public void Recusa_atendimento_sem_nenhum_exame()
    {
        var abrir = () => Abrir([], jejumConfirmado: false);

        abrir.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*ao menos um exame*");
    }

    [Fact]
    public void Recusa_exame_de_jejum_quando_o_preparo_nao_foi_confirmado()
    {
        var abrir = () => Abrir([_lab.Hemograma, _lab.Glicemia], jejumConfirmado: false);

        // A mensagem precisa dizer qual exame travou e quantas horas ele exige,
        // senao a recepcao nao consegue orientar o paciente.
        abrir.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*Glicemia de jejum (8h)*");
    }

    [Fact]
    public void Aceita_exame_de_jejum_quando_o_preparo_foi_confirmado()
    {
        var atendimento = Abrir([_lab.Glicemia], jejumConfirmado: true);

        atendimento.JejumConfirmado.Should().BeTrue();
        atendimento.Itens.Should().HaveCount(1);
    }

    [Fact]
    public void Ignora_exame_repetido_no_mesmo_pedido()
    {
        // Duplicar exame no pedido geraria tubo e cobranca a mais sem necessidade.
        var atendimento = Abrir([_lab.Hemograma, _lab.Hemograma], jejumConfirmado: false);

        atendimento.Itens.Should().HaveCount(1);
    }

    private Atendimento Abrir(IEnumerable<Exame> exames, bool jejumConfirmado) =>
        new("20260310-0001", _lab.Paciente, exames, Prioridade.Normal, jejumConfirmado, null, _agora);
}
