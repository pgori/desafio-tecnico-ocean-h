namespace LabDesk.Domain.Amostras;

/// <summary>Situacao de um tubo especifico, da coleta ate a entrega no setor tecnico.</summary>
public enum StatusAmostra
{
    /// <summary>Tubo coletado e etiquetado, esperando a conferencia da triagem.</summary>
    Coletada = 0,

    /// <summary>Aprovada na triagem, pronta para ir ao setor.</summary>
    Aceita = 1,

    /// <summary>Recusada na triagem por um motivo padronizado.</summary>
    Rejeitada = 2,

    /// <summary>Entregue ao setor tecnico. Fim do fluxo pre-analitico.</summary>
    Encaminhada = 3
}

/// <summary>Tipo de evento registrado no historico da amostra, para rastreabilidade.</summary>
public enum TipoEventoAmostra
{
    Coletada = 0,
    Aceita = 1,
    Rejeitada = 2,
    Encaminhada = 3
}
