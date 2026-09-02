<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api/cliente'
import type { AtendimentoResumo, FiltroDaFila, MotivoCancelamento } from '../api/tipos'
import { FILTROS_DA_FILA, descreverEspera, formatarHora } from '../formatadores'
import { usarResponsavel } from '../estado/responsavel'
import EtiquetaStatus from '../componentes/EtiquetaStatus.vue'

const router = useRouter()

const responsavel = usarResponsavel()

// Nenhuma acao roda sem responsavel: o laboratorio precisa saber quem fez cada etapa.
const semResponsavel = computed(() => responsavel.nome.trim().length === 0)
const avisoDeResponsavel = 'Informe o responsável no topo da tela antes de executar ações.'
const fila = ref<AtendimentoResumo[]>([])
const filtro = ref<FiltroDaFila>('AColetar')
const erro = ref('')
let temporizador: number | undefined

// Cancelamento pede motivo, entao a linha abre um painel em vez de agir no clique.
const cancelando = ref<AtendimentoResumo | null>(null)
const motivo = ref<MotivoCancelamento>('DesistenciaDoPaciente')

async function carregar() {
  try {
    fila.value = await api.fila(filtro.value)
    erro.value = ''
  } catch (e) {
    erro.value = (e as Error).message
  }
}

function trocarFiltro(novo: FiltroDaFila) {
  filtro.value = novo
  cancelando.value = null
  carregar()
}

onMounted(() => {
  carregar()
  // A fila e um painel de parede: precisa se atualizar sozinha.
  temporizador = window.setInterval(carregar, 15000)
})

onUnmounted(() => window.clearInterval(temporizador))

const pendentes = computed(() => fila.value.filter((a) => a.status === 'ComPendencia').length)

async function chamar(atendimento: AtendimentoResumo) {
  try {
    await api.chamarParaColeta(atendimento.id)
    router.push({ name: 'coleta', params: { id: atendimento.id } })
  } catch (e) {
    erro.value = (e as Error).message
  }
}

async function confirmarCancelamento() {
  if (!cancelando.value) return

  try {
    await api.cancelarAtendimento(cancelando.value.id, motivo.value)
    cancelando.value = null
    motivo.value = 'DesistenciaDoPaciente'
    await carregar()
  } catch (e) {
    erro.value = (e as Error).message
  }
}
</script>

<template>
  <div class="cabecalho-pagina">
    <h1>Fila de atendimento</h1>
    <button class="secundario pequeno" @click="carregar">Atualizar</button>
  </div>
  <p class="subtitulo">
    Ordenada por pendência de recoleta, depois prioridade e depois ordem de chegada.
  </p>

  <div class="filtros">
    <button
      v-for="opcao in FILTROS_DA_FILA"
      :key="opcao.valor"
      class="filtro"
      :class="{ 'filtro--ativo': filtro === opcao.valor }"
      @click="trocarFiltro(opcao.valor)"
    >
      {{ opcao.rotulo }}
    </button>
  </div>

  <div v-if="erro" class="aviso aviso--erro">{{ erro }}</div>

  <div v-if="pendentes" class="aviso aviso--atencao">
    {{ pendentes }} atendimento(s) aguardando recoleta. Esses pacientes já passaram pela
    coleta uma vez e devem ser chamados primeiro.
  </div>

  <div v-if="cancelando" class="cartao">
    <h2>Cancelar o atendimento {{ cancelando.numero }}</h2>
    <p class="dica" style="margin-bottom: 12px">
      Os exames que ainda não foram coletados serão encerrados.
      {{ cancelando.pacienteNome }} sai da fila e pode ser atendido em um pedido novo.
      Amostras já coletadas continuam valendo e seguem para a triagem.
    </p>

    <label class="campo">
      <span>Motivo <span class="obrigatorio">*</span></span>
      <select v-model="motivo">
        <option value="DesistenciaDoPaciente">Paciente desistiu e foi embora</option>
        <option value="PacienteNaoCompareceu">Paciente não compareceu</option>
        <option value="PreparoNaoCumprido">Preparo não cumprido, paciente reagendado</option>
        <option value="AberturaIncorreta">Pedido aberto por engano</option>
      </select>
    </label>

    <div class="acoes">
      <button
        :disabled="semResponsavel"
        :title="semResponsavel ? avisoDeResponsavel : undefined"
        @click="confirmarCancelamento"
      >
        Confirmar cancelamento
      </button>
      <button class="secundario" @click="cancelando = null">Voltar</button>
    </div>
  </div>

  <div class="cartao">
    <table v-if="fila.length">
      <thead>
        <tr>
          <th>Atendimento</th>
          <th>Paciente</th>
          <th>Prioridade</th>
          <th>Situação</th>
          <th>Chegada</th>
          <th>Espera</th>
          <th>Exames</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="atendimento in fila"
          :key="atendimento.id"
          :class="{ 'linha-prioritaria': atendimento.prioridade !== 'Normal' }"
        >
          <td class="mono">{{ atendimento.numero }}</td>
          <td>
            {{ atendimento.pacienteNome }}
            <div class="fraco">{{ atendimento.pacienteIdade }} anos</div>
          </td>
          <td>{{ atendimento.prioridade }}</td>
          <td><EtiquetaStatus :status="atendimento.status" /></td>
          <td>{{ formatarHora(atendimento.dataHoraChegada) }}</td>
          <td>{{ descreverEspera(atendimento.minutosDeEspera) }}</td>
          <td>
            {{ atendimento.quantidadeExames }}
            <span v-if="atendimento.examesPendentesDeColeta" class="fraco">
              ({{ atendimento.examesPendentesDeColeta }} a coletar)
            </span>
          </td>
          <td>
            <div class="acoes">
              <button
                v-if="atendimento.examesPendentesDeColeta"
                class="pequeno"
                :disabled="semResponsavel"
                :title="semResponsavel ? avisoDeResponsavel : undefined"
                @click="chamar(atendimento)"
              >
                Chamar para coleta
              </button>
              <RouterLink
                v-else
                class="pequeno"
                :to="{ name: 'coleta', params: { id: atendimento.id } }"
              >
                <button class="secundario pequeno">Ver</button>
              </RouterLink>
              <button
                v-if="atendimento.examesPendentesDeColeta"
                class="secundario pequeno"
                @click="cancelando = atendimento"
              >
                Cancelar
              </button>
            </div>
          </td>
        </tr>
      </tbody>
    </table>

    <p v-else-if="filtro === 'AColetar'" class="vazio">
      Nenhum paciente aguardando coleta. Abra um atendimento na tela de Recepção.
    </p>

    <p v-else class="vazio">Nenhum atendimento nesta situação hoje.</p>
  </div>
</template>
