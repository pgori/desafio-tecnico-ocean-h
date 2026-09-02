using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LabDesk.Api.Contratos;

namespace LabDesk.Tests.Integracao;

/// <summary>
/// Percorre o fluxo inteiro pela API, da chegada do paciente ate a amostra sair para o setor,
/// incluindo o caminho torto: rejeicao na triagem e recoleta.
/// </summary>
public class FluxoPreAnaliticoTestes : IClassFixture<ApiDeTeste>
{
    private readonly ApiDeTeste _api;

    public FluxoPreAnaliticoTestes(ApiDeTeste api) => _api = api;

    // Os testes compartilham o mesmo banco, e um paciente so pode ter um atendimento com
    // coleta pendente por vez. Por isso cada teste usa um dos pacientes de exemplo.

    [Fact]
    public async Task Da_chegada_do_paciente_ate_a_amostra_seguir_para_o_setor()
    {
        var cliente = _api.CriarClienteComResponsavel("Ana - recepcao");
        var exames = await BuscarExamesAsync(cliente);

        var hemograma = exames.First(e => e.Codigo == "HEMOG");
        var glicemia = exames.First(e => e.Codigo == "GLI");
        var glicada = exames.First(e => e.Codigo == "HBA1C");

        // 1. Check-in: o pedido tem glicemia, que exige jejum.
        var atendimento = await AbrirAtendimentoAsync(cliente, pacienteId: 1, [hemograma.Id, glicemia.Id, glicada.Id]);
        atendimento.Status.Should().Be(LabDesk.Domain.Atendimentos.StatusAtendimento.AguardandoColeta);
        atendimento.Itens.Should().HaveCount(3);

        // 2. O atendimento aparece na fila.
        var fila = await (await cliente.GetAsync("/api/atendimentos"))
            .LerAsync<List<AtendimentoResumoDto>>();
        fila.Should().Contain(a => a.Numero == atendimento.Numero);

        // 3. Previa da coleta: hemograma e glicada usam EDTA mas vao para setores
        //    diferentes, entao saem em tubos separados. Sao tres tubos, nao tres exames.
        var tubos = await (await cliente.GetAsync($"/api/atendimentos/{atendimento.Id}/tubos-previstos"))
            .LerAsync<List<TuboPrevistoDto>>();
        tubos.Should().HaveCount(3);
        tubos.Select(t => t.TuboCor).Should().ContainInOrder("Roxa", "Roxa", "Cinza");

        // 4. Coleta.
        await cliente.PostVazioAsync($"/api/atendimentos/{atendimento.Id}/chamar");
        var coletado = await RegistrarColetaAsync(cliente, atendimento.Id);
        coletado.Amostras.Should().HaveCount(3);
        coletado.Status.Should().Be(LabDesk.Domain.Atendimentos.StatusAtendimento.AguardandoTriagem);

        // 5. Triagem: uma amostra e rejeitada por hemolise.
        var motivos = await (await cliente.GetAsync("/api/motivos-rejeicao"))
            .LerAsync<List<MotivoRejeicaoDto>>();
        var hemolise = motivos.First(m => m.Codigo == "HEMOLISE");

        var rejeitada = coletado.Amostras.First(a => a.Exames.Any(e => e.StartsWith("HEMOG")));
        var resposta = await cliente.PostAsJsonAsync(
            $"/api/amostras/{rejeitada.Id}/rejeitar",
            new RejeitarAmostraRequest(hemolise.Id, "Soro avermelhado"));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. As demais sao aceitas e encaminhadas.
        foreach (var amostra in coletado.Amostras.Where(a => a.Id != rejeitada.Id))
        {
            await cliente.PostVazioAsync($"/api/amostras/{amostra.Id}/aceitar");
            await cliente.PostVazioAsync($"/api/amostras/{amostra.Id}/encaminhar");
        }

        // 7. O atendimento fica pendente por causa da recoleta, nao concluido.
        var comPendencia = await BuscarAtendimentoAsync(cliente, atendimento.Id);
        comPendencia.Status.Should().Be(LabDesk.Domain.Atendimentos.StatusAtendimento.ComPendencia);
        comPendencia.Itens.Should().ContainSingle(i =>
            i.Status == LabDesk.Domain.Atendimentos.StatusItemAtendimento.AguardandoRecoleta);

        // 8. Recoleta: sai um tubo novo so para o exame pendente.
        var recoletado = await RegistrarColetaAsync(cliente, atendimento.Id);
        recoletado.Amostras.Should().HaveCount(4);

        var nova = recoletado.Amostras.Single(a =>
            a.Status == LabDesk.Domain.Amostras.StatusAmostra.Coletada);
        await cliente.PostVazioAsync($"/api/amostras/{nova.Id}/aceitar");
        await cliente.PostVazioAsync($"/api/amostras/{nova.Id}/encaminhar");

        // 9. Fim do fluxo pre-analitico.
        var final = await BuscarAtendimentoAsync(cliente, atendimento.Id);
        final.Status.Should().Be(LabDesk.Domain.Atendimentos.StatusAtendimento.Concluido);
        final.DataHoraConclusao.Should().NotBeNull();
        final.Amostras.Should().HaveCount(4);
    }

    [Fact]
    public async Task Recusa_check_in_de_exame_com_jejum_sem_confirmacao_do_preparo()
    {
        var cliente = _api.CriarClienteComResponsavel();
        var exames = await BuscarExamesAsync(cliente);
        var glicemia = exames.First(e => e.Codigo == "GLI");

        var resposta = await cliente.PostAsJsonAsync("/api/atendimentos", new AbrirAtendimentoRequest(
            PacienteId: 2,
            ExameIds: [glicemia.Id],
            Prioridade: LabDesk.Domain.Atendimentos.Prioridade.Normal,
            JejumConfirmado: false,
            Observacoes: null), ApiDeTeste.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("jejum");
    }

    [Fact]
    public async Task Recusa_registrar_coleta_sem_informar_o_responsavel()
    {
        var cliente = _api.CriarClienteComResponsavel();
        var exames = await BuscarExamesAsync(cliente);
        var atendimento = await AbrirAtendimentoAsync(cliente, pacienteId: 3, [exames.First(e => e.Codigo == "HEMOG").Id]);

        // Cliente sem o cabecalho: nenhuma acao pode ficar sem responsavel registrado.
        var anonimo = _api.CreateClient();
        var resposta = await anonimo.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/coleta",
            new RegistrarColetaRequest(true), ApiDeTeste.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("X-Responsavel");
    }

    [Fact]
    public async Task Registra_responsavel_com_acento_no_historico_da_amostra()
    {
        // Nome de pessoa tem acento e cabecalho HTTP so trafega ASCII. Sem codificar o valor,
        // o servidor recusa a requisicao inteira antes de ela chegar na aplicacao, e o
        // laboratorio fica sem conseguir registrar quem coletou o tubo.
        var cliente = _api.CriarClienteComResponsavel("João da Conceição");
        var exames = await BuscarExamesAsync(cliente);
        var atendimento = await AbrirAtendimentoAsync(cliente, pacienteId: 4, [exames.First(e => e.Codigo == "HEMOG").Id]);

        var coletado = await RegistrarColetaAsync(cliente, atendimento.Id);

        coletado.Amostras.Should().ContainSingle()
            .Which.ColetadoPor.Should().Be("João da Conceição");
    }

    private static async Task<List<ExameDto>> BuscarExamesAsync(HttpClient cliente) =>
        await (await cliente.GetAsync("/api/exames")).LerAsync<List<ExameDto>>();

    private static async Task<AtendimentoDetalheDto> BuscarAtendimentoAsync(HttpClient cliente, int id) =>
        await (await cliente.GetAsync($"/api/atendimentos/{id}")).LerAsync<AtendimentoDetalheDto>();

    private static async Task<AtendimentoDetalheDto> AbrirAtendimentoAsync(HttpClient cliente, int pacienteId, int[] exameIds)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/atendimentos", new AbrirAtendimentoRequest(
            PacienteId: pacienteId,
            ExameIds: exameIds,
            Prioridade: LabDesk.Domain.Atendimentos.Prioridade.Normal,
            JejumConfirmado: true,
            Observacoes: null), ApiDeTeste.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.LerAsync<AtendimentoDetalheDto>();
    }

    private static async Task<AtendimentoDetalheDto> RegistrarColetaAsync(HttpClient cliente, int atendimentoId)
    {
        var resposta = await cliente.PostAsJsonAsync(
            $"/api/atendimentos/{atendimentoId}/coleta",
            new RegistrarColetaRequest(true), ApiDeTeste.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.LerAsync<AtendimentoDetalheDto>();
    }
}
