using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LabDesk.Api.Contratos;
using LabDesk.Domain.Atendimentos;

namespace LabDesk.Tests.Integracao;

/// <summary>
/// O que acontece com o atendimento fora do caminho feliz: o paciente que aparece com
/// uma segunda requisicao e o que vai embora antes de coletar. Sao os dois casos que
/// deixavam a fila crescer sem ter como limpar.
/// </summary>
public class FilaEEncerramentoTestes : IClassFixture<ApiDeTeste>
{
    private readonly ApiDeTeste _api;

    public FilaEEncerramentoTestes(ApiDeTeste api) => _api = api;

    [Fact]
    public async Task Recusa_segundo_atendimento_e_aceita_os_exames_no_que_ja_esta_aberto()
    {
        var cliente = _api.CriarClienteComResponsavel("Ana - recepcao");
        var exames = await BuscarExamesAsync(cliente);
        var paciente = await CadastrarPacienteAsync(cliente, "Paciente da Segunda Requisição");

        var hemograma = exames.First(e => e.Codigo == "HEMOG");
        var vhs = exames.First(e => e.Codigo == "VHS");

        var atendimento = await AbrirAtendimentoAsync(cliente, paciente.Id, [hemograma.Id]);

        // 1. O segundo pedido e recusado, e a mensagem diz onde os exames devem entrar.
        var segundo = await cliente.PostAsJsonAsync("/api/atendimentos", new AbrirAtendimentoRequest(
            paciente.Id, [vhs.Id], Prioridade.Normal, true, null), ApiDeTeste.Json);

        segundo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var mensagem = await segundo.Content.ReadAsStringAsync();
        mensagem.Should().Contain(atendimento.Numero);
        mensagem.Should().Contain("Adicione os exames novos");

        // 2. Pelo caminho certo, os exames entram no atendimento que ja existe.
        var comOsDois = await (await cliente.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/exames",
            new AdicionarExamesRequest([vhs.Id], true), ApiDeTeste.Json))
            .LerAsync<AtendimentoDetalheDto>();

        comOsDois.Itens.Should().HaveCount(2);

        // 3. E o ganho aparece na previa: hemograma e VHS sao EDTA e Hematologia,
        //    entao um tubo so atende os dois. Em atendimentos separados sairiam dois.
        var tubos = await (await cliente.GetAsync($"/api/atendimentos/{atendimento.Id}/tubos-previstos"))
            .LerAsync<List<TuboPrevistoDto>>();

        tubos.Should().ContainSingle();
        tubos[0].Exames.Should().HaveCount(2);
    }

    [Fact]
    public async Task Cancelar_libera_o_paciente_para_um_atendimento_novo()
    {
        var cliente = _api.CriarClienteComResponsavel("Ana - recepcao");
        var exames = await BuscarExamesAsync(cliente);
        var paciente = await CadastrarPacienteAsync(cliente, "Paciente que Desistiu da Fila");
        var hemograma = exames.First(e => e.Codigo == "HEMOG");

        var atendimento = await AbrirAtendimentoAsync(cliente, paciente.Id, [hemograma.Id]);

        var cancelado = await (await cliente.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/cancelar",
            new CancelarAtendimentoRequest(MotivoCancelamento.DesistenciaDoPaciente), ApiDeTeste.Json))
            .LerAsync<AtendimentoDetalheDto>();

        cancelado.Status.Should().Be(StatusAtendimento.Cancelado);
        cancelado.CanceladoPor.Should().Be("Ana - recepcao");
        cancelado.MotivoCancelamento.Should().Be(MotivoCancelamento.DesistenciaDoPaciente);

        // Sem o cancelamento, o atendimento abandonado bloquearia esse paciente
        // para sempre: a trava do check-in viraria uma prisao.
        var novo = await AbrirAtendimentoAsync(cliente, paciente.Id, [hemograma.Id]);
        novo.Id.Should().NotBe(atendimento.Id);
    }

    [Fact]
    public async Task A_fila_padrao_mostra_so_quem_ainda_tem_tubo_a_coletar()
    {
        var cliente = _api.CriarClienteComResponsavel("Bruno - coleta");
        var exames = await BuscarExamesAsync(cliente);
        var paciente = await CadastrarPacienteAsync(cliente, "Paciente Já Coletado");

        var atendimento = await AbrirAtendimentoAsync(
            cliente, paciente.Id, [exames.First(e => e.Codigo == "HEMOG").Id]);

        (await BuscarFilaAsync(cliente, FiltroDaFila.AColetar))
            .Should().Contain(a => a.Id == atendimento.Id);

        await cliente.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/coleta",
            new RegistrarColetaRequest(true), ApiDeTeste.Json);

        // Depois de coletado nao ha mais nada a fazer na sala de coleta: a linha vira
        // ruido na fila e o trabalho passa a ser da triagem.
        (await BuscarFilaAsync(cliente, FiltroDaFila.AColetar))
            .Should().NotContain(a => a.Id == atendimento.Id);

        (await BuscarFilaAsync(cliente, FiltroDaFila.EmTriagem))
            .Should().Contain(a => a.Id == atendimento.Id);
    }

    [Fact]
    public async Task Recusa_cancelamento_sem_informar_o_responsavel()
    {
        var cliente = _api.CriarClienteComResponsavel("Ana - recepcao");
        var exames = await BuscarExamesAsync(cliente);
        var paciente = await CadastrarPacienteAsync(cliente, "Paciente do Cancelamento Anônimo");

        var atendimento = await AbrirAtendimentoAsync(
            cliente, paciente.Id, [exames.First(e => e.Codigo == "HEMOG").Id]);

        // Cancelar e uma decisao que alguem tomou, e o laboratorio precisa saber quem.
        var anonimo = _api.CreateClient();
        var resposta = await anonimo.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/cancelar",
            new CancelarAtendimentoRequest(MotivoCancelamento.AberturaIncorreta), ApiDeTeste.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("X-Responsavel");
    }

    private static async Task<List<ExameDto>> BuscarExamesAsync(HttpClient cliente) =>
        await (await cliente.GetAsync("/api/exames")).LerAsync<List<ExameDto>>();

    private static async Task<List<AtendimentoResumoDto>> BuscarFilaAsync(HttpClient cliente, FiltroDaFila filtro) =>
        await (await cliente.GetAsync($"/api/atendimentos?filtro={filtro}"))
            .LerAsync<List<AtendimentoResumoDto>>();

    private static async Task<PacienteDto> CadastrarPacienteAsync(HttpClient cliente, string nome)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/pacientes", new CriarPacienteRequest(
            nome, new DateOnly(1990, 6, 15), Guid.NewGuid().ToString("N")[..11], null), ApiDeTeste.Json);

        return await resposta.LerAsync<PacienteDto>();
    }

    private static async Task<AtendimentoDetalheDto> AbrirAtendimentoAsync(
        HttpClient cliente, int pacienteId, int[] exameIds)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/atendimentos", new AbrirAtendimentoRequest(
            pacienteId, exameIds, Prioridade.Normal, true, null), ApiDeTeste.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.LerAsync<AtendimentoDetalheDto>();
    }
}
