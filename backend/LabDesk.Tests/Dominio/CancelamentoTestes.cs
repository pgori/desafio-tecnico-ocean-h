using FluentAssertions;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Comum;

namespace LabDesk.Tests.Dominio;

/// <summary>
/// Cancelamento do atendimento.
///
/// Sem ele, o paciente que desiste da fila deixa um atendimento parado para sempre:
/// ele continua contando tempo de espera no painel e impede um pedido novo para a
/// mesma pessoa. O limite e a coleta: tubo que ja saiu existe na bancada e nao some
/// porque alguem cancelou o pedido na tela.
/// </summary>
public class CancelamentoTestes
{
    private readonly CenarioDeLaboratorio _lab = new();
    private readonly DateTime _agora = new(2026, 3, 10, 7, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Cancela_o_atendimento_que_ainda_nao_teve_coleta()
    {
        var atendimento = Abrir(_lab.Hemograma, _lab.Creatinina);

        atendimento.Cancelar(MotivoCancelamento.DesistenciaDoPaciente, "Ana", _agora);

        atendimento.Status.Should().Be(StatusAtendimento.Cancelado);
        atendimento.Itens.Should().OnlyContain(i => i.Status == StatusItemAtendimento.Cancelado);
        atendimento.MotivoCancelamento.Should().Be(MotivoCancelamento.DesistenciaDoPaciente);
        atendimento.CanceladoPor.Should().Be("Ana");
        atendimento.DataHoraCancelamento.Should().Be(_agora);
    }

    [Fact]
    public void Atendimento_cancelado_sai_da_lista_de_quem_tem_tubo_a_coletar()
    {
        var atendimento = Abrir(_lab.Hemograma);

        atendimento.Cancelar(MotivoCancelamento.PacienteNaoCompareceu, "Ana", _agora);

        // E o que libera o paciente para um atendimento novo depois.
        atendimento.TemColetaPendente.Should().BeFalse();
    }

    [Fact]
    public void Cancela_so_a_recoleta_e_preserva_a_amostra_que_ja_foi_aceita()
    {
        var atendimento = Abrir(_lab.Hemograma, _lab.Creatinina);
        var amostras = atendimento.RegistrarColeta(true, "Bruno", _agora);
        CenarioDeLaboratorio.NumerarAmostras(amostras);

        var doHemograma = amostras.Single(a => a.TipoTubo == _lab.Roxa);
        atendimento.RejeitarAmostra(doHemograma.Id, _lab.Hemolise, "Carla", null, _agora);
        atendimento.AceitarAmostra(amostras.Single(a => a.Id != doHemograma.Id).Id, "Carla", _agora);

        // O paciente foi embora antes de voltar para a recoleta.
        atendimento.Cancelar(MotivoCancelamento.DesistenciaDoPaciente, "Ana", _agora);

        var hemograma = atendimento.Itens.Single(i => i.ExameId == _lab.Hemograma.Id);
        var creatinina = atendimento.Itens.Single(i => i.ExameId == _lab.Creatinina.Id);

        hemograma.Status.Should().Be(StatusItemAtendimento.Cancelado);
        creatinina.Status.Should().Be(StatusItemAtendimento.Coletado);

        // O tubo aceito continua na bancada esperando ser encaminhado: cancelar o
        // pedido na tela nao faz o tubo desaparecer da triagem.
        atendimento.Status.Should().Be(StatusAtendimento.AguardandoTriagem);
        atendimento.Amostras.Should().HaveCount(2);
    }

    [Fact]
    public void Recusa_cancelar_quando_todos_os_tubos_ja_foram_coletados()
    {
        var atendimento = Abrir(_lab.Hemograma);
        atendimento.RegistrarColeta(true, "Bruno", _agora);

        var cancelar = () => atendimento.Cancelar(MotivoCancelamento.AberturaIncorreta, "Ana", _agora);

        // O caminho certo passa a ser a triagem, que registra o motivo da recusa do tubo.
        cancelar.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*já foram coletados*");
    }

    [Fact]
    public void Recusa_cancelar_duas_vezes()
    {
        var atendimento = Abrir(_lab.Hemograma);
        atendimento.Cancelar(MotivoCancelamento.DesistenciaDoPaciente, "Ana", _agora);

        var denovo = () => atendimento.Cancelar(MotivoCancelamento.AberturaIncorreta, "Ana", _agora);

        denovo.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*já está cancelado*");
    }

    [Fact]
    public void Exame_descartado_na_triagem_nao_transforma_o_atendimento_em_cancelado()
    {
        var atendimento = Abrir(_lab.Hemograma);
        var amostras = atendimento.RegistrarColeta(true, "Bruno", _agora);
        CenarioDeLaboratorio.NumerarAmostras(amostras);

        atendimento.RejeitarAmostra(amostras[0].Id, _lab.TuboExtra, "Carla", null, _agora);

        // O item fica cancelado nos dois casos, mas aqui o paciente foi furado e o
        // fluxo aconteceu. Cancelado e reservado para quem nao chegou a coletar.
        atendimento.Itens.Single().Status.Should().Be(StatusItemAtendimento.Cancelado);
        atendimento.Status.Should().Be(StatusAtendimento.Concluido);
    }

    private Atendimento Abrir(params Exame[] exames) =>
        new("20260310-0001", _lab.Paciente, exames, Prioridade.Normal, true, null, _agora);
}
