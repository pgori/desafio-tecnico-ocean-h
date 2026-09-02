<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { api } from '../api/cliente'
import type { Painel } from '../api/tipos'
import { descreverEspera } from '../formatadores'

const painel = ref<Painel | null>(null)
let temporizador: number | undefined

async function carregar() {
  painel.value = await api.painel()
}

onMounted(() => {
  carregar()
  temporizador = window.setInterval(carregar, 15000)
})

onUnmounted(() => window.clearInterval(temporizador))
</script>

<template>
  <div class="cabecalho-pagina">
    <h1>Painel do dia</h1>
    <button class="secundario pequeno" @click="carregar">Atualizar</button>
  </div>
  <p class="subtitulo">Como a operação esta andando agora.</p>

  <div v-if="painel">
    <div class="indicadores">
      <div class="indicador">
        <div class="indicador__valor">{{ painel.aguardandoColeta }}</div>
        <div class="indicador__rotulo">Aguardando coleta</div>
      </div>
      <div class="indicador">
        <div class="indicador__valor">{{ painel.emColeta }}</div>
        <div class="indicador__rotulo">Em coleta</div>
      </div>
      <div class="indicador">
        <div class="indicador__valor">{{ painel.amostrasAguardandoTriagem }}</div>
        <div class="indicador__rotulo">Amostras na triagem</div>
      </div>
      <div class="indicador">
        <div class="indicador__valor">{{ painel.comPendencia }}</div>
        <div class="indicador__rotulo">Pendentes de recoleta</div>
      </div>
      <div class="indicador">
        <div class="indicador__valor">{{ painel.concluidosHoje }}</div>
        <div class="indicador__rotulo">Concluídos hoje</div>
      </div>
    </div>

    <div class="grade grade--dois">
      <div class="cartao">
        <h2>Qualidade da fase pre-analítica</h2>
        <p class="dica" style="margin-bottom: 14px">
          A taxa de rejeição mostra quanto retrabalho a operação esta gerando: cada
          amostra recusada significa um paciente coletado de novo.
        </p>

        <div class="indicadores" style="margin-bottom: 0">
          <div class="indicador">
            <div class="indicador__valor">{{ painel.taxaRejeicaoPercentual }}%</div>
            <div class="indicador__rotulo">Taxa de rejeição hoje</div>
          </div>
          <div class="indicador">
            <div class="indicador__valor">
              {{ painel.amostrasRejeitadasHoje }}/{{ painel.amostrasTriadasHoje }}
            </div>
            <div class="indicador__rotulo">Rejeitadas / conferidas</div>
          </div>
        </div>

        <h2 style="margin-top: 20px">Motivos mais frequentes</h2>
        <ul class="lista-limpa" v-if="painel.motivosMaisFrequentes.length">
          <li v-for="motivo in painel.motivosMaisFrequentes" :key="motivo.motivo">
            {{ motivo.motivo }} <strong>({{ motivo.quantidade }})</strong>
          </li>
        </ul>
        <p v-else class="fraco">Nenhuma amostra rejeitada hoje.</p>
      </div>

      <div class="cartao">
        <h2>Tempos</h2>
        <p class="dica" style="margin-bottom: 14px">
          Da chegada até a coleta, e da coleta até a conferência.
        </p>

        <div class="indicadores" style="margin-bottom: 0">
          <div class="indicador">
            <div class="indicador__valor">
              {{ painel.tempoMedioEsperaMinutos === null ? '-' : descreverEspera(painel.tempoMedioEsperaMinutos) }}
            </div>
            <div class="indicador__rotulo">Espera média até a coleta</div>
          </div>
          <div class="indicador">
            <div class="indicador__valor">
              {{ painel.tempoMedioTriagemMinutos === null ? '-' : descreverEspera(painel.tempoMedioTriagemMinutos) }}
            </div>
            <div class="indicador__rotulo">Coleta até a triagem</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
