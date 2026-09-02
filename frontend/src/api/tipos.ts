// Espelho dos contratos da API. Escrito a mao porque sao poucos tipos:
// gerar cliente a partir do OpenAPI so compensaria com mais endpoints.

export type Prioridade = 'Normal' | 'Preferencial' | 'Urgente'

export type StatusAtendimento =
  | 'AguardandoColeta'
  | 'EmColeta'
  | 'AguardandoTriagem'
  | 'ComPendencia'
  | 'Concluido'
  | 'Cancelado'

export type MotivoCancelamento =
  | 'DesistenciaDoPaciente'
  | 'PacienteNaoCompareceu'
  | 'PreparoNaoCumprido'
  | 'AberturaIncorreta'

/** Recorte da fila. Nao e o status: 'AColetar' agrupa tres situacoes diferentes. */
export type FiltroDaFila =
  | 'AColetar'
  | 'ComPendencia'
  | 'EmTriagem'
  | 'Concluidos'
  | 'Cancelados'
  | 'Todos'

export type StatusItemAtendimento =
  | 'AguardandoColeta'
  | 'Coletado'
  | 'AguardandoRecoleta'
  | 'EmAnalise'
  | 'Cancelado'

export type StatusAmostra = 'Coletada' | 'Aceita' | 'Rejeitada' | 'Encaminhada'

export type TipoEventoAmostra = 'Coletada' | 'Aceita' | 'Rejeitada' | 'Encaminhada'

export interface Paciente {
  id: number
  nomeCompleto: string
  dataNascimento: string
  idade: number
  documento: string
  contato: string | null
}

export interface Exame {
  id: number
  codigo: string
  nome: string
  tuboCor: string
  tuboAditivo: string
  ordemColeta: number
  exigeJejum: boolean
  horasJejum: number
  setorDestino: string
}

export interface MotivoRejeicao {
  id: number
  codigo: string
  descricao: string
  exigeRecoleta: boolean
}

export interface AtendimentoResumo {
  id: number
  numero: string
  pacienteId: number
  pacienteNome: string
  pacienteIdade: number
  prioridade: Prioridade
  status: StatusAtendimento
  dataHoraChegada: string
  minutosDeEspera: number
  quantidadeExames: number
  quantidadeAmostras: number
  examesPendentesDeColeta: number
}

export interface ItemAtendimento {
  id: number
  exameId: number
  exameCodigo: string
  exameNome: string
  tuboCor: string
  setorDestino: string
  exigeJejum: boolean
  horasJejum: number
  status: StatusItemAtendimento
}

export interface EventoAmostra {
  tipo: TipoEventoAmostra
  dataHora: string
  responsavel: string
  detalhe: string | null
}

export interface Amostra {
  id: number
  codigo: string
  atendimentoId: number
  atendimentoNumero: string
  pacienteNome: string
  pacienteDataNascimento: string
  tuboCor: string
  tuboAditivo: string
  volumeMinimoMl: number
  status: StatusAmostra
  dataHoraColeta: string
  coletadoPor: string
  dataHoraTriagem: string | null
  motivoRejeicao: string | null
  setorDestino: string | null
  exames: string[]
  eventos: EventoAmostra[]
}

export interface AtendimentoDetalhe {
  id: number
  numero: string
  paciente: Paciente
  prioridade: Prioridade
  status: StatusAtendimento
  jejumConfirmado: boolean
  observacoes: string | null
  dataHoraChegada: string
  dataHoraChamada: string | null
  dataHoraPrimeiraColeta: string | null
  dataHoraConclusao: string | null
  motivoCancelamento: MotivoCancelamento | null
  dataHoraCancelamento: string | null
  canceladoPor: string | null
  itens: ItemAtendimento[]
  amostras: Amostra[]
}

export interface TuboPrevisto {
  tuboCor: string
  tuboAditivo: string
  ordemColeta: number
  volumeMinimoMl: number
  setorDestino: string
  exames: string[]
}

export interface MotivoFrequente {
  motivo: string
  quantidade: number
}

export interface Painel {
  aguardandoColeta: number
  emColeta: number
  aguardandoTriagem: number
  comPendencia: number
  concluidosHoje: number
  amostrasAguardandoTriagem: number
  amostrasTriadasHoje: number
  amostrasRejeitadasHoje: number
  taxaRejeicaoPercentual: number
  tempoMedioEsperaMinutos: number | null
  tempoMedioTriagemMinutos: number | null
  motivosMaisFrequentes: MotivoFrequente[]
}
