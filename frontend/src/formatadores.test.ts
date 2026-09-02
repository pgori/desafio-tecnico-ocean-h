import { describe, expect, it } from 'vitest'
import {
  FILTROS_DA_FILA,
  classeDoStatus,
  dataParaIso,
  descreverEspera,
  formatarData,
  mascararData,
  nomearMotivoCancelamento,
  nomearStatus
} from './formatadores'

describe('descreverEspera', () => {
  it('mostra minutos abaixo de uma hora', () => {
    expect(descreverEspera(45)).toBe('45 min')
  })

  it('mostra horas e minutos acima de uma hora', () => {
    expect(descreverEspera(80)).toBe('1h20')
  })

  it('omite os minutos quando a espera e exata', () => {
    expect(descreverEspera(120)).toBe('2h')
  })

  it('trata a espera zerada', () => {
    expect(descreverEspera(0)).toBe('agora')
  })
})

describe('formatarData', () => {
  it('nao desloca a data de nascimento por causa do fuso', () => {
    // Montar o Date direto da string AAAA-MM-DD faz o navegador aplicar UTC
    // e mostrar o dia anterior, o que trocaria um identificador do paciente.
    expect(formatarData('1948-03-12')).toBe('12/03/1948')
  })
})

describe('status', () => {
  it('traduz o status do dominio para o texto da tela', () => {
    expect(nomearStatus('ComPendencia')).toBe('Pendente de recoleta')
    expect(nomearStatus('Coletada')).toBe('Aguardando conferência')
  })

  it('destaca pendencia e rejeicao como alerta', () => {
    expect(classeDoStatus('ComPendencia')).toContain('alerta')
    expect(classeDoStatus('Rejeitada')).toContain('alerta')
    expect(classeDoStatus('Concluido')).toContain('ok')
  })
})

describe('entrada de data em dd/mm/aaaa', () => {
  it('formata enquanto a pessoa digita', () => {
    expect(mascararData('1')).toBe('1')
    expect(mascararData('120')).toBe('12/0')
    expect(mascararData('12031948')).toBe('12/03/1948')
  })

  it('ignora o que nao for numero e nao passa de oito digitos', () => {
    expect(mascararData('12/03/1948999')).toBe('12/03/1948')
    expect(mascararData('ab12cd03ef1948')).toBe('12/03/1948')
  })

  it('converte para o formato da API lendo dia/mes/ano, nao mes/dia/ano', () => {
    // 03/12 e 3 de dezembro. Se fosse lido como mes/dia, viraria 12 de marco
    // e trocaria a data de nascimento, que e um identificador do paciente.
    expect(dataParaIso('03/12/1948')).toBe('1948-12-03')
  })

  it('faz a volta completa entre o calendario e o campo digitado', () => {
    // O calendario devolve AAAA-MM-DD e a tela mostra dia/mes/ano. Se as duas funcoes
    // discordassem, escolher no calendario apagaria ou trocaria a data digitada.
    const doCalendario = '1948-03-12'

    expect(formatarData(doCalendario)).toBe('12/03/1948')
    expect(dataParaIso(formatarData(doCalendario))).toBe(doCalendario)
  })

  it('recusa data incompleta ou inexistente', () => {
    expect(dataParaIso('12/03')).toBeNull()
    expect(dataParaIso('31/02/1990')).toBeNull()
    expect(dataParaIso('00/01/1990')).toBeNull()
    expect(dataParaIso('')).toBeNull()
  })
})

describe('cancelamento do atendimento', () => {
  it('traduz o motivo para o texto que o operador le', () => {
    expect(nomearMotivoCancelamento('DesistenciaDoPaciente')).toBe('Paciente desistiu e foi embora')
    expect(nomearMotivoCancelamento('PreparoNaoCumprido')).toContain('reagendado')
  })

  it('mostra traco quando o atendimento nao foi cancelado', () => {
    expect(nomearMotivoCancelamento(null)).toBe('-')
  })

  it('nomeia o status de atendimento cancelado e nao o deixa em vermelho', () => {
    // Cancelado nao e nao conformidade: e um pedido que foi encerrado antes da coleta.
    expect(nomearStatus('Cancelado')).toBe('Cancelado')
    expect(classeDoStatus('Cancelado')).toContain('neutra')
  })
})

describe('filtros da fila', () => {
  it('comeca por "a coletar", que e o trabalho pendente da sala de coleta', () => {
    expect(FILTROS_DA_FILA[0]).toEqual({ valor: 'AColetar', rotulo: 'A coletar' })
  })

  it('usa rotulo acentuado na tela e o valor do enum na chamada da API', () => {
    const pendencia = FILTROS_DA_FILA.find((f) => f.valor === 'ComPendencia')
    expect(pendencia?.rotulo).toBe('Com pendência')
  })
})
