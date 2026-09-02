using FluentAssertions;
using LabDesk.Domain.Amostras;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Comum;

namespace LabDesk.Tests.Dominio;

/// <summary>
/// Conferencia das amostras. O que importa aqui e a consequencia da rejeicao:
/// o exame precisa voltar para a fila de coleta, e nao sumir.
/// </summary>
public class TriagemTestes
{
    private readonly CenarioDeLaboratorio _lab = new();
    private readonly DateTime _coleta = new(2026, 3, 10, 7, 30, 0, DateTimeKind.Utc);
    private readonly DateTime _triagem = new(2026, 3, 10, 8, 5, 0, DateTimeKind.Utc);

    [Fact]
    public void Rejeicao_com_motivo_que_exige_recoleta_devolve_o_exame_para_a_fila()
    {
        var (atendimento, amostra) = ColetarUmTubo(_lab.Hemograma);

        atendimento.RejeitarAmostra(amostra.Id, _lab.Hemolise, "Carla", "Soro avermelhado", _triagem);

        amostra.Status.Should().Be(StatusAmostra.Rejeitada);
        amostra.MotivoRejeicao.Should().Be(_lab.Hemolise);
        atendimento.Itens.Single().Status.Should().Be(StatusItemAtendimento.AguardandoRecoleta);
        atendimento.Status.Should().Be(StatusAtendimento.ComPendencia);
    }

    [Fact]
    public void Rejeicao_com_motivo_que_nao_exige_recoleta_apenas_cancela_o_exame()
    {
        var (atendimento, amostra) = ColetarUmTubo(_lab.Hemograma);

        atendimento.RejeitarAmostra(amostra.Id, _lab.TuboExtra, "Carla", null, _triagem);

        // Tubo coletado a mais e descartado: nao faz sentido furar o paciente de novo.
        atendimento.Itens.Single().Status.Should().Be(StatusItemAtendimento.Cancelado);
        atendimento.Status.Should().Be(StatusAtendimento.Concluido);
    }

    [Fact]
    public void A_amostra_rejeitada_continua_registrada_com_o_motivo()
    {
        var (atendimento, amostra) = ColetarUmTubo(_lab.Hemograma);

        atendimento.RejeitarAmostra(amostra.Id, _lab.Hemolise, "Carla", "Soro avermelhado", _triagem);

        // A amostra rejeitada e o dado que alimenta o indicador de nao conformidade
        // da fase pre-analitica. Apagar o registro apagaria o indicador.
        atendimento.Amostras.Should().Contain(amostra);
        amostra.Eventos.Should().Contain(e =>
            e.Tipo == TipoEventoAmostra.Rejeitada && e.Detalhe!.Contains("Soro avermelhado"));
    }

    [Fact]
    public void A_recoleta_gera_um_tubo_novo_apenas_para_o_exame_pendente()
    {
        var atendimento = Abrir(_lab.Hemograma, _lab.Coagulograma);
        var amostras = atendimento.RegistrarColeta(true, "Bruno", _coleta);
        CenarioDeLaboratorio.NumerarAmostras(amostras);

        var roxa = amostras.Single(a => a.TipoTubo == _lab.Roxa);
        atendimento.RejeitarAmostra(roxa.Id, _lab.Hemolise, "Carla", null, _triagem);

        var recoleta = atendimento.RegistrarColeta(true, "Bruno", _triagem);

        recoleta.Should().ContainSingle();
        recoleta[0].TipoTubo.Should().Be(_lab.Roxa);
        recoleta[0].Codigo.Should().Be("20260310-0001-03");
        atendimento.Amostras.Should().HaveCount(3);
    }

    [Fact]
    public void O_exame_recoletado_continua_ligado_ao_tubo_rejeitado()
    {
        var (atendimento, rejeitada) = ColetarUmTubo(_lab.Hemograma);
        atendimento.RejeitarAmostra(rejeitada.Id, _lab.Hemolise, "Carla", null, _triagem);

        var recoleta = atendimento.RegistrarColeta(true, "Bruno", _triagem).Single();

        // Manter os dois vinculos e o que permite reconstruir depois por que
        // aquele exame demorou mais que os outros.
        var item = atendimento.Itens.Single();
        item.Amostras.Should().BeEquivalentTo([rejeitada, recoleta]);
    }

    [Fact]
    public void Nao_permite_conferir_a_mesma_amostra_duas_vezes()
    {
        var (atendimento, amostra) = ColetarUmTubo(_lab.Hemograma);
        atendimento.AceitarAmostra(amostra.Id, "Carla", _triagem);

        var aceitarDeNovo = () => atendimento.AceitarAmostra(amostra.Id, "Carla", _triagem);

        aceitarDeNovo.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*já foi conferida*");
    }

    [Fact]
    public void Nao_permite_encaminhar_amostra_que_nao_passou_pela_triagem()
    {
        var (atendimento, amostra) = ColetarUmTubo(_lab.Hemograma);

        var encaminhar = () => atendimento.EncaminharAmostra(amostra.Id, "Carla", _triagem);

        encaminhar.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*precisa ser aceita na triagem*");
    }

    [Fact]
    public void Encaminhar_a_ultima_amostra_conclui_o_atendimento()
    {
        var (atendimento, amostra) = ColetarUmTubo(_lab.Hemograma);
        atendimento.AceitarAmostra(amostra.Id, "Carla", _triagem);

        atendimento.EncaminharAmostra(amostra.Id, "Carla", _triagem);

        amostra.SetorDestino.Should().Be("Hematologia");
        atendimento.Status.Should().Be(StatusAtendimento.Concluido);
        atendimento.DataHoraConclusao.Should().Be(_triagem);
        atendimento.Itens.Single().Status.Should().Be(StatusItemAtendimento.EmAnalise);
    }

    private (Atendimento, Amostra) ColetarUmTubo(Exame exame)
    {
        var atendimento = Abrir(exame);
        var amostras = atendimento.RegistrarColeta(true, "Bruno", _coleta);
        CenarioDeLaboratorio.NumerarAmostras(amostras);

        return (atendimento, amostras.Single());
    }

    private Atendimento Abrir(params Exame[] exames) =>
        new("20260310-0001", _lab.Paciente, exames, Prioridade.Normal, true, null, _coleta);
}
