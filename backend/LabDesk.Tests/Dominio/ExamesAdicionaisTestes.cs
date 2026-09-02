using FluentAssertions;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Comum;

namespace LabDesk.Tests.Dominio;

/// <summary>
/// Paciente que ja esta no laboratorio e aparece com mais exames.
///
/// As duas regras andam juntas: a recepcao nao pode abrir um segundo atendimento para
/// quem ainda tem tubo a coletar, e por isso precisa conseguir acrescentar os exames ao
/// atendimento que ja existe. Travar sem oferecer a saida so empurraria o operador a
/// cadastrar o paciente de novo com o nome trocado.
/// </summary>
public class ExamesAdicionaisTestes
{
    private readonly CenarioDeLaboratorio _lab = new();
    private readonly DateTime _agora = new(2026, 3, 10, 7, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Exame_adicionado_antes_da_coleta_sai_no_mesmo_tubo()
    {
        var atendimento = Abrir(_lab.Hemograma);

        atendimento.AdicionarExames([_lab.Reticulocitos], jejumConfirmado: false, _agora);
        var amostras = atendimento.RegistrarColeta(true, "Bruno", _agora);

        // E este o motivo de a regra existir: em dois atendimentos separados sairiam
        // dois tubos roxos, e o paciente seria furado a mais por um problema de cadastro.
        amostras.Should().HaveCount(1);
        amostras[0].Itens.Should().HaveCount(2);
    }

    [Fact]
    public void Recusa_exame_que_ja_esta_no_atendimento()
    {
        var atendimento = Abrir(_lab.Hemograma);

        var adicionar = () => atendimento.AdicionarExames([_lab.Hemograma], false, _agora);

        adicionar.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*já estão no atendimento*");
    }

    [Fact]
    public void Confere_o_preparo_dos_exames_novos()
    {
        var atendimento = Abrir(_lab.Hemograma);

        var adicionar = () => atendimento.AdicionarExames([_lab.Glicemia], jejumConfirmado: false, _agora);

        // O exame novo passa pela mesma conferencia do check-in: descobrir que faltou
        // jejum depois da puncao custa o tubo e uma volta do paciente.
        adicionar.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*Glicemia de jejum (8h)*");
    }

    [Fact]
    public void Recusa_adicionar_exame_em_atendimento_cancelado()
    {
        var atendimento = Abrir(_lab.Hemograma);
        atendimento.Cancelar(MotivoCancelamento.DesistenciaDoPaciente, "Ana", _agora);

        var adicionar = () => atendimento.AdicionarExames([_lab.Creatinina], false, _agora);

        adicionar.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*já foi encerrado*");
    }

    [Fact]
    public void Devolve_o_paciente_para_a_fila_quando_a_coleta_anterior_ja_tinha_terminado()
    {
        var atendimento = Abrir(_lab.Hemograma);
        atendimento.ChamarParaColeta(_agora);
        atendimento.RegistrarColeta(true, "Bruno", _agora);

        atendimento.AdicionarExames([_lab.Creatinina], false, _agora.AddMinutes(20));

        // Ele ja saiu da sala de coleta: precisa ser chamado de novo, senao a fila
        // mostraria um paciente que nao esta mais la.
        atendimento.DataHoraChamada.Should().BeNull();
        atendimento.Status.Should().Be(StatusAtendimento.AguardandoColeta);
    }

    [Fact]
    public void Mantem_a_chamada_quando_o_paciente_ainda_esta_na_sala_de_coleta()
    {
        var atendimento = Abrir(_lab.Hemograma);
        atendimento.ChamarParaColeta(_agora);

        atendimento.AdicionarExames([_lab.Creatinina], false, _agora.AddMinutes(2));

        atendimento.DataHoraChamada.Should().Be(_agora);
        atendimento.Status.Should().Be(StatusAtendimento.EmColeta);
    }

    [Fact]
    public void Recusa_segundo_atendimento_para_quem_ainda_tem_tubo_a_coletar()
    {
        var aberto = Abrir(_lab.Hemograma);

        var abrirOutro = () => Atendimento.GarantirQueNaoHaColetaPendente([aberto]);

        // A mensagem precisa dizer o que fazer, e nao so que deu errado.
        abrirOutro.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*20260310-0001*")
            .WithMessage("*Adicione os exames novos a esse atendimento*");
    }

    [Fact]
    public void Libera_atendimento_novo_quando_tudo_ja_foi_coletado()
    {
        var aberto = Abrir(_lab.Hemograma);
        aberto.RegistrarColeta(true, "Bruno", _agora);

        var abrirOutro = () => Atendimento.GarantirQueNaoHaColetaPendente([aberto]);

        // O tubo ja saiu: um pedido novo exige puncao nova de qualquer jeito, entao
        // nao ha coleta para compartilhar e nao ha o que travar.
        abrirOutro.Should().NotThrow();
    }

    [Fact]
    public void Libera_atendimento_novo_depois_do_cancelamento()
    {
        var aberto = Abrir(_lab.Hemograma);
        aberto.Cancelar(MotivoCancelamento.DesistenciaDoPaciente, "Ana", _agora);

        var abrirOutro = () => Atendimento.GarantirQueNaoHaColetaPendente([aberto]);

        // Sem isso a trava viraria prisao: o atendimento abandonado impediria o
        // paciente de ser atendido para sempre.
        abrirOutro.Should().NotThrow();
    }

    private Atendimento Abrir(params Exame[] exames) =>
        new("20260310-0001", _lab.Paciente, exames, Prioridade.Normal, true, null, _agora);
}
