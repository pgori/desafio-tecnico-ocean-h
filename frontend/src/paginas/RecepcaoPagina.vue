<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api/cliente'
import type { AtendimentoResumo, Exame, Paciente, Prioridade } from '../api/tipos'
import { dataParaIso, formatarData, mascararData } from '../formatadores'
import { usarResponsavel } from '../estado/responsavel'
import EtapaDoCheckIn from '../componentes/EtapaDoCheckIn.vue'
import TampaDoTubo from '../componentes/TampaDoTubo.vue'

const router = useRouter()
const responsavel = usarResponsavel()

// Nenhuma acao roda sem responsavel: o laboratorio precisa saber quem fez cada etapa.
const semResponsavel = computed(() => responsavel.nome.trim().length === 0)
const avisoDeResponsavel = 'Informe o responsável no topo da tela antes de executar ações.'

const exames = ref<Exame[]>([])
const pacientes = ref<Paciente[]>([])
const comColetaPendente = ref<AtendimentoResumo[]>([])
const busca = ref('')
const selecionado = ref<Paciente | null>(null)

const exameIds = ref<number[]>([])
const prioridade = ref<Prioridade>('Normal')
const jejumConfirmado = ref(false)
const observacoes = ref('')

const erro = ref('')
const salvando = ref(false)

const cadastrando = ref(false)
const novo = ref({ nomeCompleto: '', dataNascimento: '', documento: '', contato: '' })
const seletorDeData = ref<HTMLInputElement | null>(null)

const hojeIso = new Date().toISOString().slice(0, 10)

// O check-in e sequencial de verdade: sem paciente nao ha pedido, e o preparo so pode ser
// conferido depois de saber quais exames foram pedidos. A tela segue essa ordem, mas sem
// prender ninguem: da para voltar a qualquer etapa ja concluida.
const etapa = ref(1)

const etapaMaximaLiberada = computed(() => {
  if (!selecionado.value) return 1
  if (!exameIds.value.length) return 2

  return 3
})

function irPara(numero: number) {
  if (numero <= etapaMaximaLiberada.value) etapa.value = numero
}

// O erro de um campo so aparece depois que a pessoa saiu dele, para o formulario nao
// abrir todo vermelho antes de ela ter chance de preencher.
const tocados = ref<Record<string, boolean>>({})

function marcarTocado(campo: string) {
  tocados.value[campo] = true
}

/** Validacao espelhando as regras do backend, para o erro aparecer antes de ir na rede. */
const errosDoPaciente = computed(() => {
  const erros: Record<string, string> = {}
  const { nomeCompleto, dataNascimento, documento } = novo.value

  if (!nomeCompleto.trim()) erros.nomeCompleto = 'Informe o nome completo do paciente.'
  if (!documento.trim()) erros.documento = 'Informe o documento do paciente.'

  if (!dataNascimento.trim()) {
    erros.dataNascimento = 'Informe a data de nascimento.'
  } else {
    const iso = dataParaIso(dataNascimento)

    if (!iso) erros.dataNascimento = 'Data inválida. Use o formato dia/mês/ano.'
    else if (iso > hojeIso) erros.dataNascimento = 'A data de nascimento não pode estar no futuro.'
  }

  return erros
})

const pacienteValido = computed(() => Object.keys(errosDoPaciente.value).length === 0)

/** Mostra o erro do campo apenas depois que a pessoa passou por ele. */
function erroDoCampo(campo: string): string | undefined {
  return tocados.value[campo] ? errosDoPaciente.value[campo] : undefined
}

function limparCadastro() {
  novo.value = { nomeCompleto: '', dataNascimento: '', documento: '', contato: '' }
  tocados.value = {}
}

/**
 * Abre o calendario do navegador ao lado do campo.
 *
 * O campo digitavel continua sendo texto com mascara dia/mes/ano: o seletor nativo
 * mostra a data no idioma do navegador, e num Windows em ingles a recepcao leria
 * mes/dia. O calendario entra como atalho de entrada, nao como o formato da tela.
 */
function abrirCalendario() {
  const seletor = seletorDeData.value
  if (!seletor) return

  if (typeof seletor.showPicker === 'function') seletor.showPicker()
  else seletor.click()
}

function aoEscolherNoCalendario(evento: Event) {
  const iso = (evento.target as HTMLInputElement).value
  if (!iso) return

  novo.value.dataNascimento = formatarData(iso)
  marcarTocado('dataNascimento')
}

onMounted(async () => {
  exames.value = await api.exames()
  comColetaPendente.value = await api.fila('AColetar')
  await procurar()
})

/**
 * Atendimento que o paciente escolhido ja tem em aberto, se houver.
 *
 * Um paciente so pode ter um pedido com coleta pendente por vez: dois atendimentos
 * agrupam os tubos separadamente e ele sairia furado a mais. Em vez de deixar a
 * recepcao descobrir isso pelo erro, a tela ja oferece o caminho certo.
 */
const atendimentoAberto = computed(() =>
  selecionado.value
    ? comColetaPendente.value.find((a) => a.pacienteId === selecionado.value!.id) ?? null
    : null
)

async function procurar() {
  pacientes.value = await api.buscarPacientes(busca.value)
}

function escolher(paciente: Paciente) {
  selecionado.value = paciente
  // Idoso tem prioridade legal de atendimento; a recepcao pode ajustar depois.
  prioridade.value = paciente.idade >= 60 ? 'Preferencial' : 'Normal'
}

/** Exames selecionados que exigem jejum. Guia o aviso de preparo do paciente. */
const exigemJejum = computed(() =>
  exames.value.filter((e) => exameIds.value.includes(e.id) && e.exigeJejum)
)

const examesSelecionados = computed(() =>
  exames.value.filter((e) => exameIds.value.includes(e.id))
)

/** Previa dos tubos: agrupa por tampa e setor, do mesmo jeito que o backend faz na coleta. */
const tubosPrevistos = computed(() => {
  const grupos = new Map<string, { cor: string; aditivo: string; ordem: number; setor: string; exames: string[] }>()

  for (const exame of examesSelecionados.value) {
    const chave = `${exame.tuboCor}|${exame.setorDestino}`
    const grupo = grupos.get(chave) ?? {
      cor: exame.tuboCor,
      aditivo: exame.tuboAditivo,
      ordem: exame.ordemColeta,
      setor: exame.setorDestino,
      exames: []
    }

    grupo.exames.push(exame.codigo)
    grupos.set(chave, grupo)
  }

  return [...grupos.values()].sort((a, b) => a.ordem - b.ordem || a.setor.localeCompare(b.setor))
})

const porSetor = computed(() => {
  const setores = new Map<string, Exame[]>()

  for (const exame of exames.value) {
    setores.set(exame.setorDestino, [...(setores.get(exame.setorDestino) ?? []), exame])
  }

  return [...setores.entries()]
})

// Resumo que fica no cabecalho da etapa recolhida. O do paciente e o mais importante:
// o operador precisa saber para quem esta montando o pedido em todas as etapas.
const resumoDoPaciente = computed(() =>
  selecionado.value ? `${selecionado.value.nomeCompleto} - ${selecionado.value.idade} anos` : ''
)

const resumoDosExames = computed(() =>
  exameIds.value.length
    ? `${exameIds.value.length} exame(s), ${tubosPrevistos.value.length} tubo(s)`
    : ''
)

async function cadastrarPaciente() {
  if (!pacienteValido.value) return

  erro.value = ''

  try {
    const paciente = await api.cadastrarPaciente({
      nomeCompleto: novo.value.nomeCompleto.trim(),
      // A tela usa dia/mes/ano; a API recebe o formato ISO.
      dataNascimento: dataParaIso(novo.value.dataNascimento)!,
      documento: novo.value.documento.trim(),
      contato: novo.value.contato.trim() || null
    })

    cadastrando.value = false
    limparCadastro()
    await procurar()
    escolher(paciente)
  } catch (e) {
    erro.value = (e as Error).message
  }
}

async function abrirAtendimento() {
  if (!selecionado.value) return

  erro.value = ''
  salvando.value = true

  try {
    const atendimento = await api.abrirAtendimento({
      pacienteId: selecionado.value.id,
      exameIds: exameIds.value,
      prioridade: prioridade.value,
      jejumConfirmado: jejumConfirmado.value,
      observacoes: observacoes.value || null
    })

    router.push({ name: 'fila', query: { aberto: atendimento.numero } })
  } catch (e) {
    erro.value = (e as Error).message
  } finally {
    salvando.value = false
  }
}

/** Exames novos entram no atendimento que ja existe, para sairem nos mesmos tubos. */
async function adicionarExames() {
  if (!atendimentoAberto.value) return

  erro.value = ''
  salvando.value = true

  try {
    const atendimento = await api.adicionarExames(
      atendimentoAberto.value.id,
      exameIds.value,
      jejumConfirmado.value
    )

    router.push({ name: 'coleta', params: { id: atendimento.id } })
  } catch (e) {
    erro.value = (e as Error).message
  } finally {
    salvando.value = false
  }
}
</script>

<template>
  <div class="cabecalho-pagina"><h1>Recepção</h1></div>
  <p class="subtitulo">
    Check-in do paciente: identificação, exames pedidos e conferência do preparo.
  </p>

  <div v-if="erro" class="aviso aviso--erro">{{ erro }}</div>

  <div class="passos">
    <EtapaDoCheckIn
      :numero="1"
      titulo="Paciente"
      :resumo="resumoDoPaciente"
      :ativa="etapa === 1"
      :liberada="true"
      @abrir="irPara(1)"
    >
      <label class="campo">
        <span>Buscar por nome ou documento</span>
        <input v-model="busca" type="search" @input="procurar" placeholder="Digite para buscar" />
      </label>

      <ul class="lista-limpa">
        <li v-for="paciente in pacientes" :key="paciente.id">
          <label class="linha-opcao">
            <input
              type="radio"
              :checked="selecionado?.id === paciente.id"
              @change="escolher(paciente)"
            />
            <span>
              <strong>{{ paciente.nomeCompleto }}</strong>
              <div class="fraco">
                {{ formatarData(paciente.dataNascimento) }} - {{ paciente.idade }} anos -
                {{ paciente.documento }}
              </div>
            </span>
          </label>
        </li>
      </ul>

      <p v-if="!pacientes.length" class="fraco">Nenhum paciente encontrado.</p>

      <div class="acoes" style="margin-top: 10px">
        <button class="secundario pequeno" @click="cadastrando = !cadastrando; limparCadastro()">
          {{ cadastrando ? 'Cancelar' : 'Cadastrar novo paciente' }}
        </button>
      </div>

      <div v-if="cadastrando" style="margin-top: 16px">
        <p class="dica" style="margin-bottom: 12px">
          Campos marcados com <span class="obrigatorio">*</span> são obrigatórios.
        </p>

        <label class="campo">
          <span>Nome completo <span class="obrigatorio">*</span></span>
          <input
            v-model="novo.nomeCompleto"
            type="text"
            :class="{ 'campo--invalido': erroDoCampo('nomeCompleto') }"
            @blur="marcarTocado('nomeCompleto')"
          />
          <span v-if="erroDoCampo('nomeCompleto')" class="erro-do-campo">
            {{ erroDoCampo('nomeCompleto') }}
          </span>
        </label>

        <label class="campo">
          <span>Data de nascimento <span class="obrigatorio">*</span></span>

          <span class="campo-data">
            <input
              :value="novo.dataNascimento"
              type="text"
              inputmode="numeric"
              maxlength="10"
              placeholder="dia/mês/ano (ex.: 12/03/1948)"
              :class="{ 'campo--invalido': erroDoCampo('dataNascimento') }"
              @input="novo.dataNascimento = mascararData(($event.target as HTMLInputElement).value)"
              @blur="marcarTocado('dataNascimento')"
            />
            <button
              type="button"
              class="campo-data__botao"
              title="Escolher no calendário"
              aria-label="Escolher no calendário"
              @click="abrirCalendario"
            >
              &#128197;
            </button>
            <input
              ref="seletorDeData"
              class="campo-data__seletor"
              type="date"
              tabindex="-1"
              aria-hidden="true"
              :max="hojeIso"
              :value="dataParaIso(novo.dataNascimento) ?? ''"
              @input="aoEscolherNoCalendario"
            />
          </span>

          <span v-if="erroDoCampo('dataNascimento')" class="erro-do-campo">
            {{ erroDoCampo('dataNascimento') }}
          </span>
        </label>

        <label class="campo">
          <span>Documento <span class="obrigatorio">*</span></span>
          <input
            v-model="novo.documento"
            type="text"
            placeholder="CPF ou outro documento"
            :class="{ 'campo--invalido': erroDoCampo('documento') }"
            @blur="marcarTocado('documento')"
          />
          <span v-if="erroDoCampo('documento')" class="erro-do-campo">
            {{ erroDoCampo('documento') }}
          </span>
        </label>

        <label class="campo">
          <span>Contato</span>
          <input v-model="novo.contato" type="text" placeholder="Telefone (opcional)" />
        </label>

        <button
          :disabled="!pacienteValido"
          :title="pacienteValido ? undefined : 'Preencha os campos obrigatórios para cadastrar.'"
          @click="cadastrarPaciente"
        >
          Cadastrar
        </button>
      </div>

      <div v-if="atendimentoAberto" class="aviso aviso--atencao" style="margin-top: 16px">
        <strong>{{ atendimentoAberto.pacienteNome }}</strong> já tem o atendimento
        <span class="mono">{{ atendimentoAberto.numero }}</span> em aberto, com
        {{ atendimentoAberto.examesPendentesDeColeta }} exame(s) aguardando coleta.
        Os exames selecionados vão entrar nesse atendimento, para sair nos mesmos tubos.
        Se o paciente não vai coletar, cancele o atendimento na tela de Fila.
      </div>

      <div class="acoes etapa__navegacao">
        <button :disabled="!selecionado" @click="irPara(2)">Continuar</button>
      </div>
    </EtapaDoCheckIn>

    <EtapaDoCheckIn
      :numero="2"
      titulo="Exames solicitados"
      :resumo="resumoDosExames"
      :ativa="etapa === 2"
      :liberada="etapaMaximaLiberada >= 2"
      @abrir="irPara(2)"
    >
      <div class="grade grade--dois">
        <div v-for="[setor, lista] in porSetor" :key="setor">
          <strong class="fraco">{{ setor }}</strong>
          <label v-for="exame in lista" :key="exame.id" class="linha-opcao">
            <input type="checkbox" :value="exame.id" v-model="exameIds" />
            <span>
              <span class="mono">{{ exame.codigo }}</span> - {{ exame.nome }}
              <span v-if="exame.exigeJejum" class="etiqueta etiqueta--andamento">
                jejum {{ exame.horasJejum }}h
              </span>
            </span>
          </label>
        </div>
      </div>

      <div class="acoes etapa__navegacao">
        <button class="secundario" @click="irPara(1)">Voltar</button>
        <button :disabled="!exameIds.length" @click="irPara(3)">Continuar</button>
      </div>
    </EtapaDoCheckIn>

    <EtapaDoCheckIn
      :numero="3"
      titulo="Preparo, prioridade e conferência"
      :ativa="etapa === 3"
      :liberada="etapaMaximaLiberada >= 3"
      @abrir="irPara(3)"
    >
      <p class="dica" style="margin-bottom: 12px">
        Confira o pedido antes de abrir o atendimento. Daqui o paciente entra na fila e o
        número do atendimento é gerado.
      </p>

      <ul class="lista-limpa">
        <li v-if="selecionado">
          <strong>Paciente</strong>
          <div>
            {{ selecionado.nomeCompleto }}
            <span class="fraco">
              - {{ formatarData(selecionado.dataNascimento) }} - {{ selecionado.idade }} anos -
              {{ selecionado.documento }}
            </span>
          </div>
        </li>
        <li>
          <strong>Exames</strong>
          <div class="fraco">
            {{ examesSelecionados.map((e) => `${e.codigo} - ${e.nome}`).join('; ') }}
          </div>
        </li>
        <li v-if="atendimentoAberto">
          <strong>Atendimento já aberto</strong>
          <div>
            <span class="mono">{{ atendimentoAberto.numero }}</span>
            <span class="fraco"> - os exames entram neste pedido, nos mesmos tubos</span>
          </div>
        </li>
      </ul>

      <div v-if="tubosPrevistos.length" style="margin-top: 18px">
        <strong>Prévia da coleta</strong>
        <p class="dica" style="margin: 6px 0 10px">
          {{ exameIds.length }} exame(s) vão gerar {{ tubosPrevistos.length }} tubo(s):
          exames que usam o mesmo aditivo e vão para o mesmo setor saem no mesmo tubo.
        </p>
        <ul class="lista-limpa">
          <li v-for="tubo in tubosPrevistos" :key="`${tubo.cor}-${tubo.setor}`">
            <TampaDoTubo :cor="tubo.cor" />
            <span class="fraco"> - {{ tubo.setor }} - {{ tubo.exames.join(', ') }}</span>
          </li>
        </ul>
      </div>

      <div v-if="exigemJejum.length" class="aviso aviso--atencao" style="margin-top: 16px">
        Exigem jejum:
        {{ exigemJejum.map((e) => `${e.nome} (${e.horasJejum}h)`).join(', ') }}.
        Confirme com o paciente antes de seguir; sem jejum a amostra será rejeitada
        na triagem e ele terá que voltar.
      </div>

      <label class="linha-opcao" style="margin-top: 16px">
        <input type="checkbox" v-model="jejumConfirmado" />
        <span>O paciente confirmou o jejum exigido pelos exames.</span>
      </label>

      <label class="campo">
        <span>Prioridade</span>
        <select v-model="prioridade">
          <option value="Normal">Normal</option>
          <option value="Preferencial">Preferencial (idoso, gestante, PCD)</option>
          <option value="Urgente">Urgente</option>
        </select>
      </label>

      <label class="campo">
        <span>Observações</span>
        <textarea v-model="observacoes" placeholder="Anotações da recepção"></textarea>
      </label>

      <div class="acoes etapa__navegacao">
        <button class="secundario" @click="irPara(2)">Voltar</button>

        <button
          v-if="atendimentoAberto"
          :disabled="!exameIds.length || salvando || semResponsavel"
          :title="semResponsavel ? avisoDeResponsavel : undefined"
          @click="adicionarExames"
        >
          Adicionar ao atendimento {{ atendimentoAberto.numero }}
        </button>

        <button
          v-else
          :disabled="!selecionado || !exameIds.length || salvando || semResponsavel"
          :title="semResponsavel ? avisoDeResponsavel : undefined"
          @click="abrirAtendimento"
        >
          Abrir atendimento
        </button>
      </div>
    </EtapaDoCheckIn>
  </div>
</template>
