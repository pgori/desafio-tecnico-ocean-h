# Modelo e API

## Entidades

```
Paciente
  NomeCompleto, DataNascimento, Documento, Contato
  └ nome completo + data de nascimento são os dois identificadores da coleta

TipoTubo                                    Exame
  Cor, Aditivo                                Codigo, Nome, SetorDestino
  OrdemColeta   ← sequência da punção         ExigeJejum, HorasJejum
  VolumeMinimoMl                              TipoTuboId ──────────┐
        ▲──────────────────────────────────────────────────────────┘

MotivoRejeicao
  Codigo, Descricao, ExigeRecoleta   ← decide se o exame volta para a fila

Atendimento  (raiz do agregado)
  Numero (AAAAMMDD-0001), PacienteId, Prioridade, Status
  DataHoraChegada, DataHoraChamada, DataHoraPrimeiraColeta, DataHoraConclusao
  JejumConfirmado, Observacoes
  MotivoCancelamento?, DataHoraCancelamento?, CanceladoPor?
  ├── Itens:    ItemAtendimento[]
  └── Amostras: Amostra[]

ItemAtendimento                             Amostra
  ExameId, Status                             Codigo (AAAAMMDD-0001-01)
        │                                     TipoTuboId, DataHoraColeta, ColetadoPor
        └────── N:N ────────────────────►     Status, DataHoraTriagem
                                              MotivoRejeicaoId?, SetorDestino?
                                              └── Eventos: EventoAmostra[]
```

**Por que o N:N entre item e amostra.** Um tubo carrega vários exames. E um exame rejeitado
aparece também no tubo da recoleta. Manter os dois vínculos preserva o histórico: dá para
reconstruir depois por que aquele exame demorou mais que os outros do mesmo pedido.

---

## Máquinas de estado

### Atendimento

O status **nunca é atribuído diretamente** — é sempre derivado dos itens, por
`AtualizarStatus()`. Assim ele não tem como divergir do que aconteceu com os tubos.

```
AguardandoColeta ──chamar──► EmColeta ──coleta──► AguardandoTriagem
       ▲                                                  │
       │                                          triagem │
       │                                                  ▼
       └────────────── ComPendencia ◄──── rejeição que exige recoleta
                                                          │
                            todos os itens em análise     ▼
                            ou cancelados ──────────► Concluido
```

O cancelamento é a única saída lateral, e ela só existe **antes da punção**:

```
AguardandoColeta ──cancelar──► Cancelado
EmColeta         ──cancelar──► Cancelado

ComPendencia     ──cancelar──► AguardandoTriagem ou Concluido,
                               porque os tubos da primeira coleta continuam na bancada
```

| Situação dos itens | Status resultante |
|---|---|
| Cancelado e sem nenhuma amostra | `Cancelado` (vem antes de tudo: nada foi coletado) |
| Algum aguardando recoleta | `ComPendencia` (é o que a operação precisa ver primeiro) |
| Algum aguardando coleta, sem chamada | `AguardandoColeta` |
| Algum aguardando coleta, já chamado | `EmColeta` |
| Todos coletados | `AguardandoTriagem` |
| Todos em análise ou cancelados | `Concluido` |

A primeira linha tem duas condições de propósito. Um exame também fica `Cancelado` quando
a triagem descarta o tubo com um motivo que não exige recoleta, mas ali o paciente foi
furado e o fluxo aconteceu: esse atendimento termina como `Concluido`. `Cancelado` é
reservado para quem não chegou a coletar, e é por isso que a regra olha as amostras.

### Amostra

```
                 ┌──aceitar──► Aceita ──encaminhar──► Encaminhada
Coletada ────────┤
                 └──rejeitar─► Rejeitada
                                   │
                    ExigeRecoleta? ├── sim → itens voltam para AguardandoRecoleta
                                   └── não → itens ficam Cancelado
```

Uma amostra só pode ser conferida uma vez. Tentar aceitar ou rejeitar de novo lança
`RegraDeNegocioException` — e a amostra rejeitada **permanece no sistema**, porque ela é o
registro da não conformidade.

### Item de atendimento

```
AguardandoColeta ──coleta──► Coletado ──amostra aceita e encaminhada──► EmAnalise
       │                         │
       │                         ├── amostra rejeitada, exige recoleta ──► AguardandoRecoleta ──┐
       │                         │                                                              │
       │                         └── amostra rejeitada, sem recoleta ──► Cancelado              │
       │                                                                                        │
       └── atendimento cancelado ──► Cancelado    ◄──────── nova coleta ────────────────────────┘
```

O mesmo `Cancelado` chega por dois caminhos: o tubo descartado na triagem e o pedido
encerrado antes da coleta. O que diferencia os dois é a existência da amostra, e é isso
que o status do atendimento lê.

---

## A regra central: agrupamento da coleta

`Atendimento.RegistrarColeta` é o coração do sistema:

1. Exige `identificacaoConfirmada` (nome completo + data de nascimento conferidos com o
   paciente presente). Sem isso, não executa.
2. Seleciona os itens que precisam de tubo — primeira coleta **ou** recoleta.
3. Agrupa por `(TipoTubo, SetorDestino)`.
4. Ordena os grupos por `TipoTubo.OrdemColeta`.
5. Cria uma `Amostra` por grupo, com código sequencial dentro do atendimento.
6. Recalcula o status do atendimento.

Exemplo real, com um pedido de cinco exames:

| Exame | Tubo | Setor |
|---|---|---|
| TP | Azul | Hemostasia |
| Creatinina | Amarela | Bioquímica |
| Hemoglobina glicada | Roxa | Bioquímica |
| Hemograma | Roxa | Hematologia |
| Glicemia | Cinza | Bioquímica |

Resultado: **5 exames → 5 tubos**, sendo dois roxos (mesmo aditivo, setores diferentes), na
ordem `Azul → Amarela → Roxa → Roxa → Cinza`.

Com um pedido de hemograma + VHS + reticulócitos, o resultado seria **3 exames → 1 tubo**.

---

## Endpoints

Todos abaixo de `/api`. Swagger em `/swagger`. Ações de escrita exigem o cabeçalho
`X-Responsavel`.

### Catálogo e cadastro

| Método | Rota | O que faz |
|---|---|---|
| `GET` | `/exames` | Catálogo com tubo, preparo e setor de cada exame |
| `GET` | `/motivos-rejeicao` | Motivos padronizados da triagem |
| `GET` | `/pacientes?busca=` | Busca por nome ou documento |
| `POST` | `/pacientes` | Cadastra paciente |

### Recepção e coleta

| Método | Rota | O que faz |
|---|---|---|
| `GET` | `/atendimentos?filtro=` | Fila do dia; sem filtro, só quem tem tubo a coletar |
| `GET` | `/atendimentos/{id}` | Detalhe com itens e amostras |
| `POST` | `/atendimentos` | Check-in — valida o preparo e recusa pedido duplicado |
| `POST` | `/atendimentos/{id}/exames` | Acrescenta exames ao pedido já aberto |
| `POST` | `/atendimentos/{id}/cancelar` | Encerra o que ainda não foi coletado |
| `POST` | `/atendimentos/{id}/chamar` | Chama para a sala de coleta |
| `GET` | `/atendimentos/{id}/tubos-previstos` | Tubos a coletar, agrupados e na ordem |
| `POST` | `/atendimentos/{id}/coleta` | Registra a coleta e gera as amostras |

### Triagem

| Método | Rota | O que faz |
|---|---|---|
| `GET` | `/amostras?status=` | Sem filtro, traz as que aguardam conferência |
| `POST` | `/amostras/{id}/aceitar` | Aprova na conferência |
| `POST` | `/amostras/{id}/rejeitar` | Recusa com motivo padronizado |
| `POST` | `/amostras/{id}/encaminhar` | Entrega ao setor técnico |

### Painel

| Método | Rota | O que faz |
|---|---|---|
| `GET` | `/painel` | Fila, taxa de rejeição, motivos frequentes e tempos médios |

### Erros

Regras de negócio retornam **400** com `ProblemDetails`, e o campo `detail` traz a mensagem
escrita para o operador ler na tela:

```json
{
  "title": "Operação não permitida",
  "status": 400,
  "detail": "Estes exames exigem jejum e o paciente não confirmou o preparo: Glicemia de jejum (8h). Oriente o paciente e reagende, ou remova os exames do pedido."
}
```
