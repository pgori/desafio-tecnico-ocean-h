using LabDesk.Api.Contratos;
using LabDesk.Domain.Amostras;
using LabDesk.Domain.Atendimentos;
using LabDesk.Domain.Catalogo;
using LabDesk.Domain.Pacientes;

namespace LabDesk.Api.Comum;

/// <summary>
/// Conversao das entidades para os contratos da API, escrita a mao.
/// Sao poucos tipos e a traducao nao e um de-para direto (idade, tempo de espera,
/// lista de exames do tubo), entao um mapeador automatico atrapalharia mais que ajudaria.
/// </summary>
public static class Mapeamento
{
    public static PacienteDto ParaDto(this Paciente paciente, DateOnly hoje) =>
        new(paciente.Id,
            paciente.NomeCompleto,
            paciente.DataNascimento,
            paciente.IdadeEm(hoje),
            paciente.Documento,
            paciente.Contato);

    public static ExameDto ParaDto(this Exame exame) =>
        new(exame.Id,
            exame.Codigo,
            exame.Nome,
            exame.TipoTubo.Cor,
            exame.TipoTubo.Aditivo,
            exame.TipoTubo.OrdemColeta,
            exame.ExigeJejum,
            exame.HorasJejum,
            exame.SetorDestino);

    public static MotivoRejeicaoDto ParaDto(this MotivoRejeicao motivo) =>
        new(motivo.Id, motivo.Codigo, motivo.Descricao, motivo.ExigeRecoleta);

    public static ItemAtendimentoDto ParaDto(this ItemAtendimento item) =>
        new(item.Id,
            item.ExameId,
            item.Exame.Codigo,
            item.Exame.Nome,
            item.Exame.TipoTubo.Cor,
            item.Exame.SetorDestino,
            item.Exame.ExigeJejum,
            item.Exame.HorasJejum,
            item.Status);

    public static AmostraDto ParaDto(this Amostra amostra, Atendimento atendimento) =>
        new(amostra.Id,
            amostra.Codigo,
            atendimento.Id,
            atendimento.Numero,
            atendimento.Paciente.NomeCompleto,
            atendimento.Paciente.DataNascimento,
            amostra.TipoTubo.Cor,
            amostra.TipoTubo.Aditivo,
            amostra.TipoTubo.VolumeMinimoMl,
            amostra.Status,
            amostra.DataHoraColeta,
            amostra.ColetadoPor,
            amostra.DataHoraTriagem,
            amostra.MotivoRejeicao?.Descricao,
            amostra.SetorDestino,
            amostra.Itens.Select(i => $"{i.Exame.Codigo} - {i.Exame.Nome}").ToList(),
            amostra.Eventos
                .OrderBy(e => e.DataHora)
                .Select(e => new EventoAmostraDto(e.Tipo, e.DataHora, e.Responsavel, e.Detalhe))
                .ToList());

    public static AtendimentoDetalheDto ParaDetalheDto(this Atendimento atendimento, DateOnly hoje) =>
        new(atendimento.Id,
            atendimento.Numero,
            atendimento.Paciente.ParaDto(hoje),
            atendimento.Prioridade,
            atendimento.Status,
            atendimento.JejumConfirmado,
            atendimento.Observacoes,
            atendimento.DataHoraChegada,
            atendimento.DataHoraChamada,
            atendimento.DataHoraPrimeiraColeta,
            atendimento.DataHoraConclusao,
            atendimento.MotivoCancelamento,
            atendimento.DataHoraCancelamento,
            atendimento.CanceladoPor,
            atendimento.Itens.Select(i => i.ParaDto()).ToList(),
            atendimento.Amostras
                .OrderBy(a => a.DataHoraColeta)
                .ThenBy(a => a.Codigo)
                .Select(a => a.ParaDto(atendimento))
                .ToList());

    public static AtendimentoResumoDto ParaResumoDto(this Atendimento atendimento, DateTime agora, DateOnly hoje)
    {
        // O tempo de espera para de contar na primeira coleta; depois disso
        // o paciente ja foi atendido e o numero deixaria de significar espera.
        // Cancelamento tambem congela a contagem: quem foi embora nao esta esperando.
        var fim = atendimento.DataHoraPrimeiraColeta ?? atendimento.DataHoraCancelamento ?? agora;
        var espera = (int)Math.Max(0, (fim - atendimento.DataHoraChegada).TotalMinutes);

        return new AtendimentoResumoDto(
            atendimento.Id,
            atendimento.Numero,
            atendimento.PacienteId,
            atendimento.Paciente.NomeCompleto,
            atendimento.Paciente.IdadeEm(hoje),
            atendimento.Prioridade,
            atendimento.Status,
            atendimento.DataHoraChegada,
            espera,
            atendimento.Itens.Count,
            atendimento.Amostras.Count,
            atendimento.Itens.Count(i =>
                i.Status is StatusItemAtendimento.AguardandoColeta or StatusItemAtendimento.AguardandoRecoleta));
    }
}
