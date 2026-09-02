// A API devolve tudo em UTC. Estas funcoes cuidam da traducao para o fuso do navegador
// e do vocabulario que aparece na tela.

export function formatarHora(iso: string | null): string {
  if (!iso) return '-'

  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

export function formatarDataHora(iso: string | null): string {
  if (!iso) return '-'

  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

export function formatarData(iso: string | null): string {
  if (!iso) return '-'

  // A data de nascimento vem como AAAA-MM-DD, sem hora. Montar o Date direto
  // da string faria o navegador aplicar fuso e mostrar o dia anterior.
  const [ano, mes, dia] = iso.split('-')
  return `${dia}/${mes}/${ano}`
}

/**
 * Vai formatando a data enquanto a pessoa digita, no formato brasileiro dd/mm/aaaa.
 *
 * O campo e de texto e nao <input type="date"> porque o navegador renderiza o campo
 * nativo no formato do idioma dele - num Windows em ingles a recepcao veria mm/dd/aaaa,
 * e trocar dia com mes na data de nascimento troca um identificador do paciente.
 */
export function mascararData(texto: string): string {
  const digitos = texto.replace(/\D/g, '').slice(0, 8)

  return [digitos.slice(0, 2), digitos.slice(2, 4), digitos.slice(4, 8)]
    .filter((parte) => parte.length > 0)
    .join('/')
}

/** Converte dd/mm/aaaa para o formato que a API espera. Devolve null se a data nao existir. */
export function dataParaIso(texto: string): string | null {
  const partes = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(texto.trim())
  if (!partes) return null

  const [, dia, mes, ano] = partes
  const data = new Date(Number(ano), Number(mes) - 1, Number(dia))

  // O Date aceita 31/02 e "corrige" sozinho para 03/03. Comparar de volta rejeita isso.
  const existe =
    data.getFullYear() === Number(ano) &&
    data.getMonth() === Number(mes) - 1 &&
    data.getDate() === Number(dia)

  return existe ? `${ano}-${mes}-${dia}` : null
}

/** Tempo de espera em texto curto, do jeito que a recepcao fala: "1h20", "45 min". */
export function descreverEspera(minutos: number): string {
  if (minutos < 1) return 'agora'
  if (minutos < 60) return `${minutos} min`

  const horas = Math.floor(minutos / 60)
  const resto = minutos % 60

  return resto === 0 ? `${horas}h` : `${horas}h${String(resto).padStart(2, '0')}`
}

const NOMES_DE_STATUS: Record<string, string> = {
  AguardandoColeta: 'Aguardando coleta',
  EmColeta: 'Em coleta',
  AguardandoTriagem: 'Aguardando triagem',
  ComPendencia: 'Pendente de recoleta',
  Concluido: 'Concluído',
  Coletado: 'Coletado',
  AguardandoRecoleta: 'Aguardando recoleta',
  EmAnalise: 'Em análise',
  Cancelado: 'Cancelado',
  Coletada: 'Aguardando conferência',
  Aceita: 'Aceita',
  Rejeitada: 'Rejeitada',
  Encaminhada: 'Encaminhada ao setor'
}

export function nomearStatus(status: string): string {
  return NOMES_DE_STATUS[status] ?? status
}

// As chaves sao o enum da API; os valores sao o que o operador le na tela.
const NOMES_DE_MOTIVO_CANCELAMENTO: Record<string, string> = {
  DesistenciaDoPaciente: 'Paciente desistiu e foi embora',
  PacienteNaoCompareceu: 'Paciente não compareceu',
  PreparoNaoCumprido: 'Preparo não cumprido, paciente reagendado',
  AberturaIncorreta: 'Pedido aberto por engano'
}

export function nomearMotivoCancelamento(motivo: string | null): string {
  if (!motivo) return '-'

  return NOMES_DE_MOTIVO_CANCELAMENTO[motivo] ?? motivo
}

/** Rotulo de cada recorte da fila. A chave e o valor que a API espera em ?filtro=. */
export const FILTROS_DA_FILA = [
  { valor: 'AColetar', rotulo: 'A coletar' },
  { valor: 'ComPendencia', rotulo: 'Com pendência' },
  { valor: 'EmTriagem', rotulo: 'Em triagem' },
  { valor: 'Concluidos', rotulo: 'Concluídos' },
  { valor: 'Cancelados', rotulo: 'Cancelados' },
  { valor: 'Todos', rotulo: 'Todos' }
] as const

/** Classe CSS da etiqueta de status: verde para o fluxo normal, vermelho para pendencia. */
export function classeDoStatus(status: string): string {
  if (status === 'ComPendencia' || status === 'Rejeitada' || status === 'AguardandoRecoleta') {
    return 'etiqueta etiqueta--alerta'
  }

  if (status === 'Concluido' || status === 'Encaminhada' || status === 'EmAnalise') {
    return 'etiqueta etiqueta--ok'
  }

  if (status === 'Cancelado') return 'etiqueta etiqueta--neutra'


  return 'etiqueta etiqueta--andamento'
}

/** Cor de fundo da bolinha que representa a tampa do tubo. */
export function corDaTampa(cor: string): string {
  const cores: Record<string, string> = {
    Azul: '#2f6fd0',
    Amarela: '#e0a800',
    Verde: '#2e9b57',
    Roxa: '#7a4bbd',
    Cinza: '#8a8f98'
  }

  return cores[cor] ?? '#8a8f98'
}
