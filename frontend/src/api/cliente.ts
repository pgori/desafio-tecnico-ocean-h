import type {
  Amostra,
  AtendimentoDetalhe,
  AtendimentoResumo,
  Exame,
  FiltroDaFila,
  MotivoCancelamento,
  MotivoRejeicao,
  Paciente,
  Painel,
  Prioridade,
  StatusAmostra,
  TuboPrevisto
} from './tipos'

// Em desenvolvimento o Vite faz proxy de /api para o backend.
// No deploy, VITE_API_URL aponta para a API publicada.
const BASE = import.meta.env.VITE_API_URL ?? '/api'

/** Erro vindo da API com a mensagem que o backend escreveu para o operador ler. */
export class ErroDaApi extends Error {}

/** Nome de quem esta operando. Vai em todas as chamadas para registrar quem fez o que. */
let responsavel = ''

export function definirResponsavel(nome: string) {
  responsavel = nome
}

async function chamar<T>(rota: string, opcoes: RequestInit = {}): Promise<T> {
  const resposta = await fetch(`${BASE}${rota}`, {
    ...opcoes,
    headers: {
      'Content-Type': 'application/json',
      // Cabecalho HTTP so aceita ASCII e nome de pessoa tem acento: sem codificar,
      // um responsavel chamado "Joao" faz o servidor recusar a requisicao inteira.
      'X-Responsavel': encodeURIComponent(responsavel),
      ...(opcoes.headers ?? {})
    }
  })

  if (!resposta.ok) {
    // O backend devolve ProblemDetails; o campo "detail" traz a mensagem de negocio.
    const corpo = await resposta.json().catch(() => null)
    throw new ErroDaApi(corpo?.detail ?? corpo?.title ?? `Falha na requisição (${resposta.status}).`)
  }

  if (resposta.status === 204) return undefined as T

  return resposta.json() as Promise<T>
}

const post = <T>(rota: string, corpo: unknown = {}) =>
  chamar<T>(rota, { method: 'POST', body: JSON.stringify(corpo) })

export const api = {
  exames: () => chamar<Exame[]>('/exames'),

  motivosRejeicao: () => chamar<MotivoRejeicao[]>('/motivos-rejeicao'),

  buscarPacientes: (busca: string) =>
    chamar<Paciente[]>(`/pacientes?busca=${encodeURIComponent(busca)}`),

  cadastrarPaciente: (dados: {
    nomeCompleto: string
    dataNascimento: string
    documento: string
    contato: string | null
  }) => post<Paciente>('/pacientes', dados),

  fila: (filtro: FiltroDaFila = 'AColetar') =>
    chamar<AtendimentoResumo[]>(`/atendimentos?filtro=${filtro}`),

  atendimento: (id: number) => chamar<AtendimentoDetalhe>(`/atendimentos/${id}`),

  abrirAtendimento: (dados: {
    pacienteId: number
    exameIds: number[]
    prioridade: Prioridade
    jejumConfirmado: boolean
    observacoes: string | null
  }) => post<AtendimentoDetalhe>('/atendimentos', dados),

  adicionarExames: (id: number, exameIds: number[], jejumConfirmado: boolean) =>
    post<AtendimentoDetalhe>(`/atendimentos/${id}/exames`, { exameIds, jejumConfirmado }),

  cancelarAtendimento: (id: number, motivo: MotivoCancelamento) =>
    post<AtendimentoDetalhe>(`/atendimentos/${id}/cancelar`, { motivo }),

  chamarParaColeta: (id: number) => post<AtendimentoDetalhe>(`/atendimentos/${id}/chamar`),

  tubosPrevistos: (id: number) => chamar<TuboPrevisto[]>(`/atendimentos/${id}/tubos-previstos`),

  registrarColeta: (id: number, identificacaoConfirmada: boolean) =>
    post<AtendimentoDetalhe>(`/atendimentos/${id}/coleta`, { identificacaoConfirmada }),

  amostras: (status?: StatusAmostra) =>
    chamar<Amostra[]>(`/amostras${status ? `?status=${status}` : ''}`),

  aceitarAmostra: (id: number) => post<Amostra>(`/amostras/${id}/aceitar`),

  rejeitarAmostra: (id: number, motivoRejeicaoId: number, observacao: string | null) =>
    post<Amostra>(`/amostras/${id}/rejeitar`, { motivoRejeicaoId, observacao }),

  encaminharAmostra: (id: number) => post<Amostra>(`/amostras/${id}/encaminhar`),

  painel: () => chamar<Painel>('/painel')
}
