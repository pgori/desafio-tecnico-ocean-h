import { createRouter, createWebHistory } from 'vue-router'

// As rotas seguem as estacoes de trabalho do laboratorio, nao as entidades do sistema:
// quem usa a tela e a recepcionista, o coletor e a pessoa da bancada de triagem.
const rotas = [
  { path: '/', redirect: '/fila' },
  {
    path: '/fila',
    name: 'fila',
    component: () => import('./paginas/FilaPagina.vue'),
    meta: { titulo: 'Fila de atendimento' }
  },
  {
    path: '/recepcao',
    name: 'recepcao',
    component: () => import('./paginas/RecepcaoPagina.vue'),
    meta: { titulo: 'Recepcao' }
  },
  {
    path: '/coleta/:id',
    name: 'coleta',
    component: () => import('./paginas/ColetaPagina.vue'),
    meta: { titulo: 'Coleta' }
  },
  {
    path: '/triagem',
    name: 'triagem',
    component: () => import('./paginas/TriagemPagina.vue'),
    meta: { titulo: 'Triagem' }
  },
  {
    path: '/painel',
    name: 'painel',
    component: () => import('./paginas/PainelPagina.vue'),
    meta: { titulo: 'Painel do dia' }
  }
]

export const router = createRouter({
  history: createWebHistory(),
  routes: rotas
})
