<script setup lang="ts">
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { usarResponsavel } from './estado/responsavel'

const rota = useRoute()
const responsavel = usarResponsavel()

const menu = [
  { nome: 'fila', rotulo: 'Fila' },
  { nome: 'recepcao', rotulo: 'Recepção' },
  { nome: 'triagem', rotulo: 'Triagem' },
  { nome: 'painel', rotulo: 'Painel' }
]
</script>

<template>
  <header class="topo">
    <span class="topo__marca">LabDesk</span>

    <nav class="topo__menu">
      <RouterLink
        v-for="item in menu"
        :key="item.nome"
        :to="{ name: item.nome }"
        class="topo__link"
        :class="{ 'topo__link--ativo': rota.name === item.nome }"
      >
        {{ item.rotulo }}
      </RouterLink>
    </nav>

    <!-- Substitui o login: identifica quem executa cada acao para a rastreabilidade. -->
    <label class="topo__responsavel">
      Responsável
      <input v-model="responsavel.nome" placeholder="ex.: Ana - recepção" />
    </label>
  </header>

  <main class="conteudo">
    <div v-if="!responsavel.nome" class="aviso aviso--atencao">
      Informe o responsável no topo da tela. Toda coleta e toda conferência ficam
      registradas com o nome de quem executou.
    </div>

    <RouterView />
  </main>
</template>
