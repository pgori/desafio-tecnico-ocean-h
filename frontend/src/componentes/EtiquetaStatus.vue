<script setup lang="ts">
import { classeDoStatus, nomearStatus } from '../formatadores'

defineProps<{ status: string }>()
</script>

<template>
  <span :class="classeDoStatus(status)">{{ nomearStatus(status) }}</span>
</template>
