<script setup lang="ts">
// Uma etapa do check-in. So a etapa ativa mostra o conteudo; as outras ficam recolhidas
// com um resumo do que ja foi preenchido, para o operador nao perder o contexto.
defineProps<{
  numero: number
  titulo: string
  resumo?: string
  ativa: boolean
  liberada: boolean
}>()

defineEmits<{ abrir: [] }>()
</script>

<template>
  <section class="cartao etapa" :class="{ 'etapa--ativa': ativa }">
    <button
      type="button"
      class="etapa__cabecalho"
      :disabled="!liberada"
      :title="liberada ? undefined : 'Conclua a etapa anterior para chegar aqui.'"
      @click="$emit('abrir')"
    >
      <span class="etapa__titulo">
        <span class="etapa__numero">{{ numero }}</span>
        {{ titulo }}
      </span>
      <span v-if="resumo && !ativa" class="etapa__resumo">{{ resumo }}</span>
    </button>

    <div v-if="ativa" class="etapa__corpo">
      <slot />
    </div>
  </section>
</template>
