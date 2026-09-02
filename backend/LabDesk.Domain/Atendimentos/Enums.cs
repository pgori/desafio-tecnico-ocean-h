namespace LabDesk.Domain.Atendimentos;

/// <summary>Prioridade na fila de coleta. Preferencial cobre idoso, gestante e PCD.</summary>
public enum Prioridade
{
    Normal = 0,
    Preferencial = 1,
    Urgente = 2
}

/// <summary>
/// Situacao do atendimento como um todo. E sempre calculada a partir dos itens,
/// nunca alterada solta, para nao ficar inconsistente com os exames.
/// </summary>
public enum StatusAtendimento
{
    /// <summary>Paciente na fila, ainda nao chamado para a coleta.</summary>
    AguardandoColeta = 0,

    /// <summary>Paciente chamado, na sala de coleta.</summary>
    EmColeta = 1,

    /// <summary>Tudo coletado, esperando a conferencia das amostras na triagem.</summary>
    AguardandoTriagem = 2,

    /// <summary>Alguma amostra foi rejeitada e o paciente precisa ser recoletado.</summary>
    ComPendencia = 3,

    /// <summary>Todas as amostras seguiram para os setores tecnicos. Fim do fluxo pre-analitico.</summary>
    Concluido = 4,

    /// <summary>Nenhum exame chegou a ser coletado: o pedido foi encerrado antes da puncao.</summary>
    Cancelado = 5
}

/// <summary>
/// Por que um atendimento foi encerrado sem coleta.
///
/// E lista fechada pelo mesmo motivo dos motivos de rejeicao: texto livre nao vira
/// indicador, e evasao de fila e um numero que o laboratorio acompanha.
/// </summary>
public enum MotivoCancelamento
{
    /// <summary>O paciente estava na fila e foi embora antes de ser coletado.</summary>
    DesistenciaDoPaciente = 0,

    /// <summary>O pedido foi aberto mas o paciente nunca apareceu para a coleta.</summary>
    PacienteNaoCompareceu = 1,

    /// <summary>Jejum ou outro preparo nao foi cumprido e o paciente foi reagendado.</summary>
    PreparoNaoCumprido = 2,

    /// <summary>Erro da recepcao: paciente trocado, exames errados ou pedido duplicado.</summary>
    AberturaIncorreta = 3
}

/// <summary>Situacao de um exame especifico dentro do atendimento.</summary>
public enum StatusItemAtendimento
{
    AguardandoColeta = 0,

    /// <summary>Ja tem amostra coletada, aguardando a triagem aprovar.</summary>
    Coletado = 1,

    /// <summary>A amostra foi rejeitada na triagem e o exame precisa de nova coleta.</summary>
    AguardandoRecoleta = 2,

    /// <summary>Amostra aceita e encaminhada ao setor. Sai do escopo pre-analitico.</summary>
    EmAnalise = 3,

    /// <summary>Exame descartado sem recoleta (ex.: pedido cancelado depois da coleta).</summary>
    Cancelado = 4
}
