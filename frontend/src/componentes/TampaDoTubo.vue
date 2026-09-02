<script setup lang="ts">
import { corDaTampa } from '../formatadores'

// O coletor identifica o tubo pela cor da tampa, entao a tela mostra a cor
// junto do nome em vez de so escrever "EDTA".
defineProps<{ cor: string; aditivo?: string }>()
</script>

<template>
  <span class="tampa">
    <span class="tampa__bolinha" :style="{ background: corDaTampa(cor) }"></span>
    <span>
      {{ cor }}
      <span v-if="aditivo" class="fraco">- {{ aditivo }}</span>
    </span>
  </span>
</template>
