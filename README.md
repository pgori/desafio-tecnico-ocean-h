# LabDesk

Sistema de apoio à operação de um laboratório de análises clínicas na **fase pré-analítica**:
da chegada do paciente até a amostra ser conferida e liberada para o setor técnico.

Backend em **C# / .NET 10** e frontend em **Vue 3**.

> **Demonstração:** _(link do front)_ · **API/Swagger:** _(link da API)_
> A instância pública sobe com atendimentos de exemplo em vários pontos do fluxo.
> Deploy era no-go no enunciado; subi uma instância só para facilitar a avaliação, sem
> pipeline nem infraestrutura — detalhes em [`docs/04-deploy.md`](docs/04-deploy.md).

---

## Como rodar

### Com Docker (um comando)

```bash
docker compose up --build
```

- Front: <http://localhost:8080>
- API/Swagger: <http://localhost:5080/swagger>

Sobe Postgres, API e front. A base é criada e populada automaticamente, com atendimentos de
demonstração.

### Sem Docker

Precisa de **.NET SDK 10** e **Node 20+**. O backend usa SQLite por padrão, então não há banco
para instalar.

```bash
# terminal 1 — API em http://localhost:5080
cd backend
dotnet run --project LabDesk.Api

# terminal 2 — front em http://localhost:5173
cd frontend
npm install
npm run dev
```

O Vite faz proxy de `/api` para a API, então as duas pontas ficam na mesma origem e não há
CORS para configurar em desenvolvimento.

Para subir com dados de exemplo:

```bash
cd backend
Banco__DadosDeDemonstracao=true dotnet run --project LabDesk.Api
```

### Testes

```bash
cd backend  && dotnet test   # 50 testes (domínio + infraestrutura + integração)
cd frontend && npm run test  # 17 testes
```

### Roteiro de 2 minutos para ver o fluxo inteiro

1. Preencha o **responsável** no topo da tela (substitui o login, toda ação fica registrada
   com um nome).
2. **Recepção** → o check-in é em três etapas. Escolha um paciente, avance, marque `HEMOG`,
   `HBA1C` e `GLI`, avance de novo. A terceira etapa é a conferência do pedido: repare no
   aviso de jejum e na prévia dos tubos, onde **3 exames viram 3 tubos**, sendo dois roxos,
   porque hemograma e hemoglobina glicada usam o mesmo aditivo mas vão para setores
   diferentes.
3. **Fila** → *Chamar para coleta*.
4. **Coleta** → confira a identificação, marque a confirmação e registre. Os tubos aparecem
   na ordem de coleta, com os códigos das etiquetas.
5. **Triagem** → **rejeite** um tubo por *Amostra hemolisada*. O atendimento vai para
   *Pendente de recoleta* e o exame volta para a fila.
6. **Coleta** de novo → sai um tubo novo só para o exame pendente.
7. **Painel** → taxa de rejeição, motivos mais frequentes e tempos médios.

---

## Escolha da stack

**Full-stack.** O fluxo pré-analítico é operacional: recepção, sala de coleta, bancada de
triagem. Uma API sem tela demonstra o modelo, mas não demonstra que eu entendi o trabalho, e a
tela ainda é esteticamente melhor. A de coleta, listando os tubos na ordem certa,
comunica o domínio de um jeito que um endpoint no Swagger não comunica.

**Arquitetura:** monorepo, monólito modular. Quatro projetos no backend (`Domain`,
`Infrastructure`, `Api`, `Tests`) e uma SPA Vue separada. Sem MediatR, CQRS, repositório
genérico ou AutoMapper: com cerca de dez casos de uso, essas camadas custam indireção e não
pagam nada de volta. O que eu **não** abri mão: `LabDesk.Domain` não referencia EF Core nem
ASP.NET, e é essa fronteira que garante que as regras estejam no domínio.

```
backend/
  LabDesk.Domain/          entidades e regras, sem dependência externa nenhuma
  LabDesk.Infrastructure/  EF Core, mapeamentos, carga inicial do catálogo
  LabDesk.Api/             controllers, contratos, serviços de aplicação
  LabDesk.Tests/           testes de domínio e de integração
frontend/                  Vue 3 + TypeScript + Vite + Pinia
docs/                      domínio, decisões e modelo
```

---

## Recorte de escopo

**Dentro:** check-in com validação de preparo → fila com prioridade e filtro por situação →
coleta com agrupamento de tubos → triagem com aceite/rejeição → recoleta → encaminhamento ao
setor → painel de indicadores. Fora do caminho feliz: exames acrescentados a um pedido já
aberto e cancelamento com motivo padronizado.

**Fora, e por quê:** resultados e laudos (fase analítica e pós-analítica); integração com
analisadores; faturamento e convênios; autenticação (no-go do enunciado); aliquotagem;
impressão e leitura de código de barras.

Como sugerido no enunciado, a escolha foi **profundidade em vez de largura**: um fluxo que
roda de ponta a ponta, incluindo o caminho torto, rejeição e recoleta, em vez de várias telas
de cadastro que não se conectam.

→ **[`docs/02-decisoes.md`](docs/02-decisoes.md)** tem as 18 decisões com contexto, motivo e o
custo de cada uma.

---

## Como pesquisei e entendi o domínio

Escrevi **[`docs/01-dominio.md`](docs/01-dominio.md)** antes de escrever código. Resumo das
descobertas que mudaram a modelagem:

**Fontes.** Manuais de coleta públicos de laboratórios grandes (Fleury, DASA, Hermes Pardini)
foram a fonte mais concreta. Publicam o mapeamento exame → tubo, critérios de rejeição e
orientações de preparo. Complementei com material sobre qualidade na fase pré-analítica
(ISO 15189 e resoluções da ANVISA para laboratórios clínicos) para entender *por que* a
padronização existe. Usei IA para levantar o vocabulário da área, não como fonte. Foi ela
que me deu o que procurar, mas **removi do texto final todo número de norma e percentual que
eu não consegui confirmar**.

**1. Exame não é amostra.** Minha intuição inicial estava errada. O tubo é definido pelo
aditivo que o exame precisa, não pelo exame: um único tubo de EDTA atende hemograma, VHS e
reticulócitos ao mesmo tempo. Coletar três tubos seria furar o paciente à toa. Por isso a
coleta **agrupa** os exames pendentes e gera uma amostra por grupo. É a regra mais testada do
projeto.

**2. A ordem dos tubos importa.** Existe uma sequência recomendada de coleta porque o aditivo
de um tubo pode ser carregado para o seguinte e alterar o resultado. Virou
`TipoTubo.OrdemColeta`, e a tela de coleta sempre lista os tubos numerados nessa ordem.

**3. Rejeitar amostra não é apagar amostra.** A triagem usa uma lista fechada de motivos —
motivo em texto livre não vira indicador. A amostra rejeitada continua registrada, porque ela
*é* o dado de não conformidade. E nem toda rejeição gera recoleta: hemólise gera, mas um tubo
coletado a mais é só descartado. Daí a flag `ExigeRecoleta`.

**4. Identificação positiva e preparo são travas, não avisos.** Etiquetar tubo antes de
conferir quem está na cadeira é uma das principais causas de troca de amostra: a coleta não
executa sem a confirmação. E a validação de jejum está no check-in, não na coleta: descobrir
que faltou jejum depois da punção significa perder o tubo e mandar o paciente voltar.

---

## Testes

| Onde | Quantos | O quê |
|---|---|---|
| Domínio | 36 | Agrupamento por tubo, ordem de coleta, identificação positiva, consequência de cada tipo de rejeição, recoleta, cancelamento, exames acrescentados ao pedido, transições inválidas |
| API e infraestrutura | 14 | Fluxo completo pela API real com banco real (rejeição, recoleta, responsável com acento), pedido duplicado recusado, cancelamento, recortes da fila, carga do catálogo idempotente e corte do dia no fuso do laboratório |
| Frontend | 17 | Formatadores: fuso na data de nascimento, entrada de data em dia/mês/ano, a volta entre o calendário e o campo digitado, rótulos de status e de motivo de cancelamento |

Os testes de integração existem porque **foi exatamente ali que os erros apareceram**: o
mapeamento do EF Core para a relação muitos-para-muitos entre amostra e exame quebrou de três
formas diferentes, e nenhum teste de unidade teria pego isso.

---

## O que eu faria a seguir

1. **Aliquotagem.** Hoje, quando o mesmo tubo serve dois setores, o sistema manda coletar dois
   tubos. O correto é centrifugar e dividir em alíquotas. É o item que mais aproximaria o
   sistema da realidade.
2. **Jejum com justificativa.** Hoje bloqueia. O real permite seguir registrando a não
   conformidade, com supervisor responsável; a estrutura já existe.
3. **Código de barras.** O código da etiqueta já é gerado; falta gerar a etiqueta para
   impressão e permitir bipar o tubo na triagem em vez de procurar na lista.
4. **Login e perfis** substituindo o seletor de responsável, sem mudar nada além da classe
   `ResponsavelAtual`.
5. **Migrations** no lugar do `EnsureCreated`, obrigatório assim que houver dados reais.
6. **Prazo de estabilidade da amostra** — hoje é só um motivo de rejeição manual; poderia ser
   calculado por exame e alertado automaticamente.
7. **Indicadores por período** em vez de só o dia corrente, para acompanhar a taxa de rejeição
   ao longo do tempo, que é o número que justifica o sistema existir.
8. **Testes de componente Vue.** Hoje se o layout quebrar, nenhum teste avisa.
9. **Busca na fila** por nome ou número, e uma tela de gestão sobre o histórico, essa sim com
   ordenação livre por coluna. A fila operacional não tem ordenação justamente porque a
   ordem dela é regra de atendimento (decisão 17).
10. **Motivo de cancelamento como catálogo no banco**, no lugar do enum, com a evasão de fila
    virando indicador no painel ao lado da taxa de rejeição.

---

## Como usei IA

**Ferramenta:** Claude Code (Opus), no terminal, durante todo o trabalho, pesquisa de
domínio, discussão de arquitetura, implementação e revisão. O arquivo
[`CLAUDE.md`](CLAUDE.md) está versionado: é o contexto e as regras que eu impus ao assistente
(vocabulário do domínio, convenções de código, e uma lista explícita do que **não** adicionar).

**Onde ajudou de verdade**

- **Levantar o vocabulário do domínio.** Foi o maior ganho. Sozinho eu levaria muito mais
  tempo para descobrir que existe o termo "fase pré-analítica", que *order of draw* é um
  conceito com nome, ou que QNS é a sigla usada para volume insuficiente. Isso me deu **o que
  procurar**.
- **Boilerplate.** Configurações do EF Core, DTOs, esqueleto dos componentes Vue, montagem do
  Docker Compose. Trabalho mecânico que consumiria horas.
- **Casos de borda de teste** que eu não tinha pensado. Por exemplo, o exame repetido no
  mesmo pedido, e o que acontece ao tentar coletar um atendimento que já foi todo coletado.

**Onde eu corrigi ou discordei**

- **Paciente, Exame, Amostra, um para um.** A distinção entre exame pedido e tubo físico,
  que é o eixo do domínio inteiro, veio da pesquisa nos manuais de coleta, não do modelo.
  Reescrevi.
- **Arquitetura inflada.** A sugestão inicial incluía MediatR, repositório genérico e
  AutoMapper. Cortei todos: com dez casos de uso, cada um deles só adiciona indireção. O
  enunciado ainda avisava explicitamente contra isso.
- **`EnsureCreated` em vez de migrations.** Aqui eu discordei do padrão que a IA defendia. Com
  dois provedores de banco e nenhum dado real para preservar, migrations seriam dois conjuntos
  para manter sem entregar nada. Está documentado como decisão consciente, com o custo
  declarado.
- **A tela de Recepção era uma parede de formulário.** Paciente, exames e preparo apareciam
  os três de uma vez, em duas colunas. Pedi para virar um fluxo em etapas, uma seção por vez,
  com o nome do paciente sempre visível e a última etapa servindo de conferência antes de
  abrir o atendimento. O check-in é o ponto onde o erro custa caro, paciente trocado ou exame
  errado, e uma tela que mostra tudo junto não obriga ninguém a conferir nada. Continuo
  podendo voltar a qualquer etapa com um clique: queria uma conferência, não um assistente
  que prende o operador.
- **O campo de data.** Eu quis o seletor nativo, com calendário, em vez do campo de texto com
  máscara. A resposta foi que o campo nativo mostra a data no idioma do navegador e num
  Windows em inglês a recepção leria mm/dd, o que troca um identificador do paciente. O
  acordo foi ficar com os dois: o campo digitável continua sendo texto em dia/mês/ano, e um
  botão ao lado abre o calendário do navegador para quem preferir clicar.
- **Um bug que a revisão não pegou e a execução pegou.** O painel do dia usava a meia-noite
  **UTC** como corte. Em Brasília isso zeraria os indicadores às 21h, no meio do turno. Só
  apareceu quando rodei o sistema com dados de demonstração e os números vieram errados.
  Virou a classe `RelogioDoLaboratorio`, com o fuso configurável e teste próprio.

