using LabDesk.Domain.Amostras;
using LabDesk.Domain.Atendimentos;

namespace LabDesk.Api.Contratos;

// Os contratos ficam separados das entidades de proposito: a tela precisa de dados
// prontos para exibir (idade, tempo de espera, cor do tubo) que nao sao estado do dominio.

// ---------- Pacientes ----------

public record PacienteDto(
    int Id,
    string NomeCompleto,
    DateOnly DataNascimento,
    int Idade,
    string Documento,
    string? Contato);

public record CriarPacienteRequest(
    string NomeCompleto,
    DateOnly DataNascimento,
    string Documento,
    string? Contato);

// ---------- Catalogo ----------

public record ExameDto(
    int Id,
    string Codigo,
    string Nome,
    string TuboCor,
    string TuboAditivo,
    int OrdemColeta,
    bool ExigeJejum,
    int HorasJejum,
    string SetorDestino);

public record MotivoRejeicaoDto(
    int Id,
    string Codigo,
    string Descricao,
    bool ExigeRecoleta);

// ---------- Atendimento ----------

/// <summary>
/// Recorte da fila pedido pela tela. Nao e o status do atendimento: "a coletar" agrupa
/// tres situacoes diferentes, e e assim que a sala de coleta enxerga a fila.
/// </summary>
public enum FiltroDaFila
{
    /// <summary>Padrao: quem ainda tem tubo a tirar, incluindo recoleta.</summary>
    AColetar = 0,

    /// <summary>Só quem teve amostra rejeitada e precisa ser recoletado.</summary>
    ComPendencia = 1,

    /// <summary>Já coletado, esperando a conferência das amostras.</summary>
    EmTriagem = 2,

    /// <summary>Encerrados hoje com todas as amostras encaminhadas.</summary>
    Concluidos = 3,

    /// <summary>Encerrados hoje sem coleta.</summary>
    Cancelados = 4,

    /// <summary>Tudo que está na fila do dia, sem recorte.</summary>
    Todos = 5
}

public record AbrirAtendimentoRequest(
    int PacienteId,
    IReadOnlyList<int> ExameIds,
    Prioridade Prioridade,
    bool JejumConfirmado,
    string? Observacoes);

/// <summary>Linha da fila de espera. Traz so o que a recepcao precisa ver de relance.</summary>
public record AtendimentoResumoDto(
    int Id,
    string Numero,
    int PacienteId,
    string PacienteNome,
    int PacienteIdade,
    Prioridade Prioridade,
    StatusAtendimento Status,
    DateTime DataHoraChegada,
    int MinutosDeEspera,
    int QuantidadeExames,
    int QuantidadeAmostras,
    int ExamesPendentesDeColeta);

public record ItemAtendimentoDto(
    int Id,
    int ExameId,
    string ExameCodigo,
    string ExameNome,
    string TuboCor,
    string SetorDestino,
    bool ExigeJejum,
    int HorasJejum,
    StatusItemAtendimento Status);

public record AtendimentoDetalheDto(
    int Id,
    string Numero,
    PacienteDto Paciente,
    Prioridade Prioridade,
    StatusAtendimento Status,
    bool JejumConfirmado,
    string? Observacoes,
    DateTime DataHoraChegada,
    DateTime? DataHoraChamada,
    DateTime? DataHoraPrimeiraColeta,
    DateTime? DataHoraConclusao,
    MotivoCancelamento? MotivoCancelamento,
    DateTime? DataHoraCancelamento,
    string? CanceladoPor,
    IReadOnlyList<ItemAtendimentoDto> Itens,
    IReadOnlyList<AmostraDto> Amostras);

/// <summary>Exames que o paciente trouxe depois de o atendimento já estar aberto.</summary>
public record AdicionarExamesRequest(
    IReadOnlyList<int> ExameIds,
    bool JejumConfirmado);

/// <summary>Encerramento do atendimento antes da coleta, com motivo padronizado.</summary>
public record CancelarAtendimentoRequest(MotivoCancelamento Motivo);

public record RegistrarColetaRequest(bool IdentificacaoConfirmada);

/// <summary>
/// O que o coletor precisa ter na mao antes de puncionar: quais tubos tirar,
/// em que ordem e com quais exames em cada um.
/// </summary>
public record TuboPrevistoDto(
    string TuboCor,
    string TuboAditivo,
    int OrdemColeta,
    decimal VolumeMinimoMl,
    string SetorDestino,
    IReadOnlyList<string> Exames);

// ---------- Amostras ----------

public record AmostraDto(
    int Id,
    string Codigo,
    int AtendimentoId,
    string AtendimentoNumero,
    string PacienteNome,
    DateOnly PacienteDataNascimento,
    string TuboCor,
    string TuboAditivo,
    decimal VolumeMinimoMl,
    StatusAmostra Status,
    DateTime DataHoraColeta,
    string ColetadoPor,
    DateTime? DataHoraTriagem,
    string? MotivoRejeicao,
    string? SetorDestino,
    IReadOnlyList<string> Exames,
    IReadOnlyList<EventoAmostraDto> Eventos);

public record EventoAmostraDto(
    TipoEventoAmostra Tipo,
    DateTime DataHora,
    string Responsavel,
    string? Detalhe);

public record RejeitarAmostraRequest(int MotivoRejeicaoId, string? Observacao);

// ---------- Painel ----------

public record PainelDto(
    int AguardandoColeta,
    int EmColeta,
    int AguardandoTriagem,
    int ComPendencia,
    int ConcluidosHoje,
    int AmostrasAguardandoTriagem,
    int AmostrasTriadasHoje,
    int AmostrasRejeitadasHoje,
    double TaxaRejeicaoPercentual,
    int? TempoMedioEsperaMinutos,
    int? TempoMedioTriagemMinutos,
    IReadOnlyList<MotivoFrequenteDto> MotivosMaisFrequentes);

public record MotivoFrequenteDto(string Motivo, int Quantidade);
