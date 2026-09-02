<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { api } from '../api/cliente'
import type { AtendimentoDetalhe, TuboPrevisto } from '../api/tipos'
import { formatarData, formatarDataHora, formatarHora, nomearMotivoCancelamento } from '../formatadores'
import { usarResponsavel } from '../estado/responsavel'
import EtiquetaStatus from '../componentes/EtiquetaStatus.vue'
import TampaDoTubo from '../componentes/TampaDoTubo.vue'

const rota = useRoute()

const responsavel = usarResponsavel()

// Nenhuma acao roda sem responsavel: o laboratorio precisa saber quem fez cada etapa.
const semResponsavel = computed(() => responsavel.nome.trim().length === 0)
const avisoDeResponsavel = 'Informe o responsável no topo da tela antes de executar ações.'
const id = Number(rota.params.id)

const atendimento = ref<AtendimentoDetalhe | null>(null)
const tubos = ref<TuboPrevisto[]>([])
const identificacaoConfirmada = ref(false)
const erro = ref('')
const sucesso = ref('')

async function carregar() {
  atendimento.value = await api.atendimento(id)
  tubos.value = await api.tubosPrevistos(id)
}

onMounted(carregar)

const temPendencia = computed(() => atendimento.value?.status === 'ComPendencia')
const cancelado = computed(() => atendimento.value?.status === 'Cancelado')

const amostrasDaUltimaColeta = computed(() =>
  atendimento.value?.amostras.filter((a) => a.status === 'Coletada') ?? []
)

async function registrarColeta() {
  erro.value = ''
  sucesso.value = ''

  try {
    atendimento.value = await api.registrarColeta(id, identificacaoConfirmada.value)
    tubos.value = await api.tubosPrevistos(id)
    identificacaoConfirmada.value = false
    sucesso.value = 'Coleta registrada. Etiquete os tubos com os códigos abaixo.'
  } catch (e) {
    erro.value = (e as Error).message
  }
}
</script>

<template>
  <div v-if="atendimento">
    <div class="cabecalho-pagina">
      <h1>Coleta - {{ atendimento.numero }}</h1>
      <EtiquetaStatus :status="atendimento.status" />
    </div>
    <p class="subtitulo">
      Confira a identificação do paciente antes de puncionar e etiquete os tubos na
      presença dele.
    </p>

    <div v-if="erro" class="aviso aviso--erro">{{ erro }}</div>
    <div v-if="sucesso" class="aviso aviso--ok">{{ sucesso }}</div>
    <div v-if="cancelado" class="aviso aviso--atencao">
      Atendimento cancelado em {{ formatarDataHora(atendimento.dataHoraCancelamento) }}
      por {{ atendimento.canceladoPor }}:
      {{ nomearMotivoCancelamento(atendimento.motivoCancelamento) }}.
      Para atender o paciente, abra um atendimento novo na Recepção.
    </div>

    <div v-if="temPendencia" class="aviso aviso--atencao">
      Este atendimento tem exame aguardando recoleta: uma amostra foi rejeitada na triagem.
    </div>

    <div class="grade grade--dois">
      <div>
        <div class="cartao">
          <h2>Identificação do paciente</h2>
          <p class="dica" style="margin-bottom: 12px">
            Confirme em voz alta os dois identificadores com o próprio paciente.
          </p>

          <ul class="lista-limpa">
            <li><strong>Nome</strong><div>{{ atendimento.paciente.nomeCompleto }}</div></li>
            <li>
              <strong>Data de nascimento</strong>
              <div>
                {{ formatarData(atendimento.paciente.dataNascimento) }}
                ({{ atendimento.paciente.idade }} anos)
              </div>
            </li>
            <li><strong>Documento</strong><div>{{ atendimento.paciente.documento }}</div></li>
            <li>
              <strong>Preparo</strong>
              <div>
                {{ atendimento.jejumConfirmado ? 'Jejum confirmado na recepção' : 'Sem exigência de jejum' }}
              </div>
            </li>
            <li v-if="atendimento.observacoes">
              <strong>Observações</strong>
              <div>{{ atendimento.observacoes }}</div>
            </li>
          </ul>
        </div>

        <div class="cartao">
          <h2>Exames do pedido</h2>
          <table>
            <tbody>
              <tr v-for="item in atendimento.itens" :key="item.id">
                <td class="mono">{{ item.exameCodigo }}</td>
                <td>{{ item.exameNome }}</td>
                <td><TampaDoTubo :cor="item.tuboCor" /></td>
                <td><EtiquetaStatus :status="item.status" /></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div>
        <div class="cartao" v-if="tubos.length">
          <h2>Tubos a coletar</h2>
          <p class="dica" style="margin-bottom: 12px">
            Na ordem de coleta. Coletar fora de ordem carrega aditivo de um tubo para o
            outro e altera o resultado.
          </p>

          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Tampa</th>
                <th>Volume min.</th>
                <th>Setor</th>
                <th>Exames</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(tubo, indice) in tubos" :key="`${tubo.tuboCor}-${tubo.setorDestino}`">
                <td>{{ indice + 1 }}</td>
                <td><TampaDoTubo :cor="tubo.tuboCor" :aditivo="tubo.tuboAditivo" /></td>
                <td>{{ tubo.volumeMinimoMl }} mL</td>
                <td>{{ tubo.setorDestino }}</td>
                <td>{{ tubo.exames.join(', ') }}</td>
              </tr>
            </tbody>
          </table>

          <label class="linha-opcao" style="margin-top: 16px">
            <input type="checkbox" v-model="identificacaoConfirmada" />
            <span>
              Confirmo que o paciente foi identificado pelo nome completo e pela data de
              nascimento, e que as etiquetas serão coladas na presença dele.
            </span>
          </label>

          <button
            :disabled="!identificacaoConfirmada || semResponsavel"
            :title="semResponsavel ? avisoDeResponsavel : undefined"
            @click="registrarColeta"
          >
            Registrar coleta
          </button>
        </div>

        <div class="cartao" v-else>
          <h2>Tubos a coletar</h2>
          <p class="vazio">Nenhum exame pendente de coleta neste atendimento.</p>
        </div>

        <div class="cartao" v-if="amostrasDaUltimaColeta.length">
          <h2>Etiquetas geradas</h2>
          <table>
            <tbody>
              <tr v-for="amostra in amostrasDaUltimaColeta" :key="amostra.id">
                <td class="codigo-amostra">{{ amostra.codigo }}</td>
                <td><TampaDoTubo :cor="amostra.tuboCor" /></td>
                <td>{{ formatarHora(amostra.dataHoraColeta) }}</td>
                <td class="fraco">{{ amostra.exames.join(', ') }}</td>
              </tr>
            </tbody>
          </table>
          <p class="dica">Encaminhe os tubos para a bancada de triagem.</p>
        </div>
      </div>
    </div>
  </div>
</template>
