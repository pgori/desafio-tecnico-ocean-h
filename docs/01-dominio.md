# O domínio: fluxo pré-analítico de um laboratório de análises clínicas

Este documento é o que eu levantei sobre o domínio antes de começar a implementação. Ele explica
o vocabulário, o fluxo real e, principalmente, as três ou quatro coisas que eu não sabia e
que mudaram a modelagem.

---

## 1. O recorte: fase pré-analítica

O enunciado pede "da chegada do paciente até a conferência das amostras". Pesquisando,
descobri que isso é uma fase reconhecida, com nome próprio no laboratório. Foi o primeiro
ganho da pesquisa: em vez de inventar um recorte, limitei o escopo ao que o próprio domínio
já usa.

O trabalho de um laboratório clínico se divide em três fases:

| Fase | O que acontece | Está no sistema? |
|---|---|---|
| **Pré-analítica** | Pedido, cadastro, preparo, coleta, identificação, transporte, triagem | **Sim** |
| Analítica | Processamento da amostra no equipamento, geração do resultado bruto | Não |
| Pós-analítica | Validação técnica, laudo, liberação, entrega ao paciente/médico | Não |

A literatura da área é consistente em apontar que **a maior parte das não conformidades de um
laboratório clínico nasce na fase pré-analítica**, e não no equipamento. Amostra hemolisada,
volume insuficiente, tubo errado, identificação trocada: nada disso é erro de análise, é erro
de processo antes da análise. É por isso que um sistema que organiza essa fase é útil de
verdade, e é por isso que o núcleo deste projeto é a triagem com motivos padronizados.

---

## 2. O fluxo, passo a passo

```
[1] Chegada / recepção
      │  paciente chega com a requisição (pedido médico)
      ▼
[2] Check-in
      │  confere identificação, exames pedidos e PREPARO (jejum)
      │  gera a ORDEM DE SERVIÇO (atendimento)
      ▼
[3] Fila de espera
      │  ordem de chamada com prioridade legal (idoso, gestante, PCD)
      │
      ├──► CANCELAMENTO  ✕  desistiu, não compareceu, sem preparo, aberto por engano
      │                     (só enquanto nenhum tubo saiu)
      ▼
[4] Coleta (punção venosa)
      │  agrupa os exames pendentes por tubo  ──►  gera as AMOSTRAS
      │  respeita a ORDEM DE COLETA entre os tubos
      │  identificação positiva + etiqueta colada na presença do paciente
      ▼
[5] Triagem / conferência
      │  aceita  ──►  encaminha ao setor
      │  rejeita ──►  motivo padronizado ──►  RECOLETA (volta para [4])
      ▼
[6] Setor técnico (bancada)
      ✕  fim do escopo deste sistema
```

---

## 3. Regras de domínio descobertas

Estas são as descobertas da pesquisa que mudaram a modelagem e viraram regras de domínio.
São elas que separam o sistema de um CRUD genérico.

### 3.1 Exame não é amostra

Esta é a descoberta central. Inicialmente, eu achava que cada exame pedido geraria um registro de amostra.

Não é assim. **O tubo é definido pelo aditivo que o exame precisa, não pelo exame.** Um
único tubo de EDTA atende hemograma, VHS e contagem de reticulócitos ao mesmo tempo. Coletar
três tubos para esses três exames seria furar o paciente à toa e desperdiçar material.

Consequência direta no código: `Atendimento.RegistrarColeta` agrupa os exames pendentes e
gera **uma amostra por grupo**, não uma por exame. É a regra mais testada do projeto.

### 3.2 Cada exame tem um tubo obrigatório, identificado pela cor da tampa

O coletor não lê "EDTA" na bancada: ele pega a tampa roxa. O sistema fala a mesma língua.

| Tampa | Aditivo | Para que serve |
|---|---|---|
| Azul | Citrato de sódio 3,2% | Coagulograma (TP, TTPA) |
| Amarela | Ativador de coágulo + gel separador | Bioquímica, hormônios, sorologia |
| Verde | Heparina de lítio | Plasma heparinizado |
| Roxa | EDTA | Hematologia, hemoglobina glicada |
| Cinza | Fluoreto de sódio + EDTA | Glicemia |

O tubo de citrato tem uma particularidade que virou regra no sistema: ele exige **volume
exato**, porque a proporção sangue/anticoagulante altera o resultado do tempo de protrombina.
Por isso `TipoTubo` tem `VolumeMinimoMl` e a tela de triagem mostra esse valor ao lado da
amostra: é o critério objetivo para recusar por volume insuficiente.

### 3.3 A ordem dos tubos importa

Existe uma sequência recomendada para coletar os tubos quando são vários na mesma punção
(*order of draw*). O motivo é concreto: o aditivo de um tubo pode ser carregado para o
seguinte pela agulha e alterar o resultado. Citrato vem cedo; tubos com anticoagulante forte
vêm depois.

No sistema isso virou `TipoTubo.OrdemColeta`, e a tela de coleta lista os tubos **sempre
ordenados por ele**, numerados de 1 a N. É um detalhe pequeno de implementar e que muda
completamente a utilidade da tela para quem coleta.

### 3.4 Rejeitar amostra não é apagar amostra

A conferência da triagem tem uma lista fechada de motivos de recusa. Isso não é burocracia:

- **Motivo em texto livre não vira indicador.** Sem padronizar, o laboratório não consegue
  responder "qual é a nossa maior causa de retrabalho neste mês".
- **A amostra rejeitada continua existindo no sistema.** Ela é o registro da não
  conformidade. Se o registro sumir, o indicador some junto.

E há uma distinção que eu só entendi pesquisando: **nem toda rejeição gera recoleta.**
Hemólise gera: precisa de sangue novo. Já um tubo coletado a mais, sem exame vinculado, é
simplesmente descartado; furar o paciente de novo não faria sentido nenhum. Por isso
`MotivoRejeicao` tem a flag `ExigeRecoleta`, e ela decide se o exame volta para a fila de
coleta ou é cancelado.

Motivos que entraram no catálogo:

| Código | Descrição | Gera recoleta |
|---|---|---|
| `HEMOLISE` | Amostra hemolisada | sim |
| `QNS` | Volume insuficiente para o exame (QNS) | sim |
| `COAGULO` | Amostra coagulada em tubo com anticoagulante | sim |
| `TUBO` | Tubo incorreto para o exame solicitado | sim |
| `IDENT` | Identificação ausente ou divergente do paciente | sim |
| `LIPEMIA` | Amostra lipêmica | sim |
| `ESTABILIDADE` | Fora do prazo de estabilidade ou temperatura de transporte | sim |
| `PREPARO` | Preparo do paciente não cumprido | sim |
| `EXTRA` | Tubo coletado a mais, sem exame vinculado | **não** |
| `CANCELADO` | Exame cancelado após a coleta | **não** |

Sobre `IDENT`: a regra correta é recoletar, nunca reetiquetar. Corrigir a etiqueta de um tubo
cuja identificação está em dúvida é exatamente como se troca a amostra de dois pacientes.

---

## 4. Duas regras de segurança do paciente que viraram trava no sistema

### Identificação positiva

A regra de ouro da coleta: confirmar **dois identificadores** com o próprio paciente (nome
completo e data de nascimento) e colar a etiqueta **na presença dele**, nunca antes.

Etiquetar tubos com antecedência, na bancada, é prático e é uma das principais causas de
troca de amostra. No sistema, `RegistrarColeta` só executa com `identificacaoConfirmada`
verdadeiro, e a tela de coleta exibe nome e data de nascimento em destaque justamente para
serem lidos em voz alta.

### Preparo do paciente

Vários exames exigem jejum (glicemia, perfil lipídico, ferro sérico). Descobrir que faltou
jejum **depois** da punção significa perder o tubo, gerar retrabalho e mandar o paciente
voltar outro dia.

Por isso a validação está no **check-in**, não na coleta: se o pedido tem exame com jejum e a
recepção não confirmou o preparo, o atendimento nem abre, e a mensagem de erro diz quais
exames travaram e quantas horas cada um exige.

---

## 5. Rastreabilidade

Cada amostra tem:

- um **código único** impresso na etiqueta (`20260830-0001-03`: atendimento + sequência), que
  é como o tubo circula pelo laboratório e o candidato natural a virar código de barras;
- um **histórico de eventos** (`EventoAmostra`) com o que aconteceu, quando e **quem fez**.

O "quem fez" é a razão de existir o seletor de responsável no topo da tela, mesmo sem login:
saber quem coletou e quem conferiu cada tubo é requisito de rastreabilidade do laboratório,
não de segurança da aplicação. Autenticação ficou fora do escopo; o registro do responsável,
não.

---

## 6. Indicadores que a operação acompanha

- **Taxa de rejeição na triagem** — quanto retrabalho a operação está gerando. Cada amostra
  recusada é um paciente coletado de novo.
- **Motivos mais frequentes** — onde atacar primeiro.
- **Tempo de espera** (chegada → coleta) e **coleta → triagem** — se a fila está andando.
  São recortes do que o laboratório chama de TAT (*turnaround time*), que na versão completa
  vai até a entrega do laudo, fora deste escopo.

---

## 7. Como eu pesquisei

1. **Vocabulário primeiro.** Usei IA para levantar os termos da área ("fase pré-analítica",
   *order of draw*, QNS, aliquotagem, TAT). Isso me deu o que procurar; não usei como fonte.
2. **Manuais de coleta públicos de laboratórios grandes** (Fleury, DASA, Hermes Pardini).
   Foram a fonte mais útil e mais concreta: eles publicam o mapeamento exame → tubo, os
   critérios de rejeição e as orientações de preparo, exatamente no formato que o sistema
   precisava.
3. **Material sobre qualidade na fase pré-analítica** — ISO 15189, resoluções da ANVISA para
   laboratórios clínicos e material de programas de controle de qualidade, para entender por
   que a padronização dos motivos de rejeição existe.

O que eu faria com mais tempo: conversar com alguém que trabalha na coleta. Nenhum manual
substitui vinte minutos com quem faz a triagem todo dia, e certamente há decisões de tela
aqui que um profissional da área acharia estranhas.
