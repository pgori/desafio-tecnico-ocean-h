import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import { definirResponsavel } from '../api/cliente'

const CHAVE = 'labdesk:responsavel'

/**
 * Quem esta operando o sistema agora.
 *
 * Substitui o login, que ficou fora do escopo. Nao serve para controlar acesso:
 * serve para o laboratorio saber quem coletou e quem conferiu cada amostra,
 * que e uma exigencia de rastreabilidade e nao de seguranca.
 */
export const usarResponsavel = defineStore('responsavel', () => {
  const nome = ref(localStorage.getItem(CHAVE) ?? '')

  definirResponsavel(nome.value)

  watch(nome, (atual) => {
    localStorage.setItem(CHAVE, atual)
    definirResponsavel(atual)
  })

  return { nome }
})
