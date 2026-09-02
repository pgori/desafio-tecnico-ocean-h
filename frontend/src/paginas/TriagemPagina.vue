<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api } from '../api/cliente'
import type { Amostra, MotivoRejeicao } from '../api/tipos'
import { formatarData, formatarHora } from '../formatadores'
import { usarResponsavel } from '../estado/responsavel'
import EtiquetaStatus from '../componentes/EtiquetaStatus.vue'
import TampaDoTubo from '../componentes/TampaDoTubo.vue'

const responsavel = usarResponsavel()

// Nenhuma acao roda sem responsavel: o laboratorio precisa saber quem fez cada etapa.
const semResponsavel = computed(() => responsavel.nome.trim().length === 0)
const avisoDeResponsavel = 'Informe o responsável no topo da tela antes de executar ações.'

const aguardando = ref<Amostra[]>([])
const aceitas = ref<Amostra[]>([])
const motivos = ref<MotivoRejeicao[]>([])

const erro = ref('')
const sucesso = ref('')

// Amostra em processo de rejeicao: o motivo so e pedido quando o operador recusa.
const rejeitando = ref<Amostra | null>(null)
const motivoEscolhido = ref<number | null>(null)
const observacao = ref('')

async function carregar() {
  ;[aguardando.value, aceitas.value] = await Promise.all([
    api.amostras('Coletada'),
    api.amostras('Aceita')
  ])
}

onMounted(async () => {
  motivos.value = await api.motivosRejeicao()
  await carregar()
})

async function executar(acao: () => Promise<unknown>, mensagem: string) {
  erro.value = ''
  sucesso.value = ''

  try {
    await acao()
    sucesso.value = mensagem
    await carregar()
  } catch (e) {
    erro.value = (e as Error).message
  }
}

const aceitar = (amostra: Amostra) =>
  executar(() => api.aceitarAmostra(amostra.id), `Amostra ${amostra.codigo} aceita.`)

const encaminhar = (amostra: Amostra) =>
  executar(
    () => api.encaminharAmostra(amostra.id),
    `Amostra ${amostra.codigo} encaminhada ao setor.`
  )

function abrirRejeicao(amostra: Amostra) {
  rejeitando.value = amostra
  motivoEscolhido.value = null
  observacao.value = ''
}

async function confirmarRejeicao() {
  if (!rejeitando.value || !motivoEscolhido.value) return

  const amostra = rejeitando.value
  const motivo = motivos.value.find((m) => m.id === motivoEscolhido.value)!

  await executar(
    () => api.rejeitarAmostra(amostra.id, motivo.id, observacao.value || null),
    motivo.exigeRecoleta
      ? `Amostra ${amostra.codigo} rejeitada. O exame voltou para a fila de recoleta.`
      : `Amostra ${amostra.codigo} rejeitada e descartada, sem recoleta.`
  )

  rejeitando.value = null
}
</script>

<template>
  <div class="cabecalho-pagina">
    <h1>Triagem</h1>
    <button class="secundario pequeno" @click="carregar">Atualizar</button>
  </div>
  <p class="subtitulo">
    Conferência das amostras que chegaram da coleta, antes de liberar para os setores.
  </p>

  <div v-if="erro" class="aviso aviso--erro">{{ erro }}</div>
  <div v-if="sucesso" class="aviso aviso--ok">{{ sucesso }}</div>

  <div class="cartao">
    <h2>Aguardando conferência ({{ aguardando.length }})</h2>

    <table v-if="aguardando.length">
      <thead>
        <tr>
          <th>Amostra</th>
          <th>Paciente</th>
          <th>Tampa</th>
          <th>Volume min.</th>
          <th>Coleta</th>
          <th>Exames</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <template v-for="amostra in aguardando" :key="amostra.id">
          <tr>
            <td class="codigo-amostra">{{ amostra.codigo }}</td>
            <td>
              {{ amostra.pacienteNome }}
              <div class="fraco">{{ formatarData(amostra.pacienteDataNascimento) }}</div>
            </td>
            <td><TampaDoTubo :cor="amostra.tuboCor" :aditivo="amostra.tuboAditivo" /></td>
            <td>{{ amostra.volumeMinimoMl }} mL</td>
            <td>
              {{ formatarHora(amostra.dataHoraColeta) }}
              <div class="fraco">{{ amostra.coletadoPor }}</div>
            </td>
            <td class="fraco">{{ amostra.exames.join(', ') }}</td>
            <td>
              <div class="acoes">
                <button
                  class="pequeno"
                  :disabled="semResponsavel"
                  :title="semResponsavel ? avisoDeResponsavel : undefined"
                  @click="aceitar(amostra)"
                >
                  Aceitar
                </button>
                <button
                  class="perigo pequeno"
                  :disabled="semResponsavel"
                  :title="semResponsavel ? avisoDeResponsavel : undefined"
                  @click="abrirRejeicao(amostra)"
                >
                  Rejeitar
                </button>
              </div>
            </td>
          </tr>

          <tr v-if="rejeitando?.id === amostra.id">
            <td colspan="7">
              <div class="aviso aviso--atencao" style="margin: 0">
                <strong>Motivo da rejeição de {{ amostra.codigo }}</strong>

                <label class="campo" style="margin-top: 10px">
                  <select v-model="motivoEscolhido">
                    <option :value="null" disabled>Selecione o motivo</option>
                    <option v-for="motivo in motivos" :key="motivo.id" :value="motivo.id">
                      {{ motivo.descricao }}
                      {{ motivo.exigeRecoleta ? '(gera recoleta)' : '(sem recoleta)' }}
                    </option>
                  </select>
                </label>

                <label class="campo">
                  <input v-model="observacao" type="text" placeholder="Observação (opcional)" />
                </label>

                <div class="acoes">
                  <button
                    class="perigo pequeno"
                    :disabled="!motivoEscolhido || semResponsavel"
                    :title="semResponsavel ? avisoDeResponsavel : undefined"
                    @click="confirmarRejeicao"
                  >
                    Confirmar rejeição
                  </button>
                  <button class="secundario pequeno" @click="rejeitando = null">Cancelar</button>
                </div>
              </div>
            </td>
          </tr>
        </template>
      </tbody>
    </table>

    <p v-else class="vazio">Nenhuma amostra aguardando conferência.</p>
  </div>

  <div class="cartao">
    <h2>Aceitas, aguardando envio ao setor ({{ aceitas.length }})</h2>

    <table v-if="aceitas.length">
      <tbody>
        <tr v-for="amostra in aceitas" :key="amostra.id">
          <td class="codigo-amostra">{{ amostra.codigo }}</td>
          <td>{{ amostra.pacienteNome }}</td>
          <td><TampaDoTubo :cor="amostra.tuboCor" /></td>
          <td><EtiquetaStatus :status="amostra.status" /></td>
          <td class="fraco">{{ amostra.exames.join(', ') }}</td>
          <td>
            <button
              class="pequeno"
              :disabled="semResponsavel"
              :title="semResponsavel ? avisoDeResponsavel : undefined"
              @click="encaminhar(amostra)"
            >
              Encaminhar ao setor
            </button>
          </td>
        </tr>
      </tbody>
    </table>

    <p v-else class="vazio">Nenhuma amostra aguardando envio.</p>
  </div>
</template>
