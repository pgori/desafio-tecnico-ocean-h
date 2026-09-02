using FluentAssertions;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Comum;

namespace LabDesk.Tests.Dominio;

/// <summary>
/// Coleta. E aqui que mora a regra que diferencia o sistema de um cadastro generico:
/// o coletor nao tira um tubo por exame, ele tira um tubo por aditivo.
/// </summary>
public class ColetaTestes
{
    private readonly CenarioDeLaboratorio _lab = new();
    private readonly DateTime _agora = new(2026, 3, 10, 7, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Agrupa_exames_do_mesmo_tubo_e_do_mesmo_setor_em_uma_unica_amostra()
    {
        var atendimento = Abrir(_lab.Hemograma, _lab.Reticulocitos);

        var amostras = atendimento.RegistrarColeta(true, "Bruno", _agora);

        // Os dois exames sao EDTA e vao para Hematologia: um tubo resolve os dois.
        amostras.Should().HaveCount(1);
        amostras[0].Itens.Should().HaveCount(2);
        amostras[0].TipoTubo.Should().Be(_lab.Roxa);
    }

    [Fact]
    public void Separa_o_mesmo_tubo_em_amostras_diferentes_quando_os_setores_sao_diferentes()
    {
        var atendimento = Abrir(_lab.Hemograma, _lab.HemoglobinaGlicada);

        var amostras = atendimento.RegistrarColeta(true, "Bruno", _agora);

        // Hemograma e hemoglobina glicada usam EDTA, mas vao para bancadas diferentes.
        // Sem aliquotagem, o jeito de o tubo chegar inteiro no setor e coletar dois.
        amostras.Should().HaveCount(2);
        amostras.Should().OnlyContain(a => a.TipoTubo == _lab.Roxa);
        amostras.Select(a => a.Itens.Single().Exame.SetorDestino)
            .Should().BeEquivalentTo(["Hematologia", "Bioquímica"]);
    }

    [Fact]
    public void Devolve_os_tubos_na_ordem_de_coleta()
    {
        var atendimento = Abrir(_lab.Glicemia, _lab.Hemograma, _lab.Coagulograma, _lab.Creatinina);

        var amostras = atendimento.RegistrarColeta(true, "Bruno", _agora);

        // Coletar fora de ordem carrega aditivo de um tubo para o outro.
        amostras.Select(a => a.TipoTubo.Cor)
            .Should().ContainInOrder("Azul", "Amarela", "Roxa", "Cinza");
    }

    [Fact]
    public void Numera_as_amostras_a_partir_do_numero_do_atendimento()
    {
        var atendimento = Abrir(_lab.Hemograma, _lab.Coagulograma);

        var amostras = atendimento.RegistrarColeta(true, "Bruno", _agora);

        amostras.Select(a => a.Codigo).Should().Equal("20260310-0001-01", "20260310-0001-02");
    }

    [Fact]
    public void Recusa_a_coleta_sem_confirmacao_da_identificacao_do_paciente()
    {
        var atendimento = Abrir(_lab.Hemograma);

        var coletar = () => atendimento.RegistrarColeta(false, "Bruno", _agora);

        // Etiquetar tubo sem conferir quem esta na cadeira e a principal causa
        // de troca de amostra em laboratorio.
        coletar.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*identificação do paciente*");
        atendimento.Amostras.Should().BeEmpty();
    }

    [Fact]
    public void Recusa_a_segunda_coleta_quando_nao_ha_exame_pendente()
    {
        var atendimento = Abrir(_lab.Hemograma);
        atendimento.RegistrarColeta(true, "Bruno", _agora);

        var recoletar = () => atendimento.RegistrarColeta(true, "Bruno", _agora);

        recoletar.Should().Throw<RegraDeNegocioException>()
            .WithMessage("*nenhum exame pendente*");
    }

    [Fact]
    public void Marca_o_atendimento_como_aguardando_triagem_depois_de_coletar_tudo()
    {
        var atendimento = Abrir(_lab.Hemograma, _lab.Coagulograma);

        atendimento.RegistrarColeta(true, "Bruno", _agora);

        atendimento.Status.Should().Be(StatusAtendimento.AguardandoTriagem);
        atendimento.DataHoraPrimeiraColeta.Should().Be(_agora);
        atendimento.Itens.Should().OnlyContain(i => i.Status == StatusItemAtendimento.Coletado);
    }

    [Fact]
    public void Registra_o_evento_de_coleta_no_historico_da_amostra()
    {
        var atendimento = Abrir(_lab.Hemograma);

        var amostra = atendimento.RegistrarColeta(true, "Bruno", _agora).Single();

        amostra.Eventos.Should().ContainSingle()
            .Which.Responsavel.Should().Be("Bruno");
    }

    private Atendimento Abrir(params Exame[] exames) =>
        new("20260310-0001", _lab.Paciente, exames, Prioridade.Normal, true, null, _agora);
}
