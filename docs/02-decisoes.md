# Decisões e recortes

Registro das decisões que tomei, no formato **contexto → decisão → consequência**. As
consequências negativas estão aqui de propósito: toda decisão custa alguma coisa.

---

## 1. Full-stack

**Contexto.** O desafio permite escolher backend, frontend ou os dois.

**Decisão.** Os dois: API em C#/.NET e SPA em Vue 3.

**Por quê.** O fluxo pré-analítico é operacional: recepção, sala de coleta, bancada de
triagem. Uma API sem tela demonstra o modelo, mas não demonstra que eu entendi *o trabalho*.
A tela de coleta listando os tubos na ordem certa comunica o domínio de um jeito que um
endpoint no Swagger não comunica. E a integração entre as duas pontas é REST + CORS: o risco
aqui é de tempo, não técnico.

**Consequência.** Menos profundidade em cada lado do que se eu tivesse escolhido um só.
Compensei cortando escopo funcional com agressividade em vez de cortar qualidade.

---

## 2. Recorte: só a fase pré-analítica

**Contexto.** "Da chegada até a conferência das amostras" pode ser esticado em muitas
direções: resultados, laudos, faturamento, agendamento.

**Decisão.** Um fluxo completo e fechado: check-in → fila → coleta → triagem → encaminhamento
ao setor. Nada além disso.

**Por quê.** Como o próprio enunciado pede, profundidade em vez de largura. Prefiro um
fluxo que roda de ponta a ponta, inclusive no caminho torto (rejeição e recoleta), a dez
telas de cadastro que não se conectam. E a fase pré-analítica é justamente onde nasce a
maior parte do retrabalho de um laboratório, então é o recorte de maior valor real.

**Ficou de fora, e por quê:**

| Fora do escopo | Motivo |
|---|---|
| Resultados, laudos, liberação técnica | Fase analítica e pós-analítica — fora do pedido |
| Integração com analisadores (HL7/ASTM) | Alto custo, impossível de demonstrar sem equipamento |
| Faturamento, convênios, TUSS | Domínio administrativo paralelo, não operacional |
| Autenticação e perfis | No-go explícito do enunciado |
| Agendamento prévio, coleta domiciliar | Alarga a largura sem aprofundar o fluxo |
| Aliquotagem | Complexidade alta, e há um contorno honesto (ver decisão 5) |
| Impressão e leitura de código de barras | O código é gerado e exibido; hardware não cabe aqui |

---

## 3. Monólito modular, sem camadas extras

**Contexto.** É tentador aplicar Clean Architecture completa com MediatR, CQRS, repositório
genérico e AutoMapper.

**Decisão.** Quatro projetos (`Domain`, `Infrastructure`, `Api`, `Tests`), serviços de
aplicação simples injetados nos controllers, `DbContext` usado direto, mapeamento escrito à
mão.

**Por quê.** Cada abstração precisa se pagar. Com cerca de dez casos de uso, MediatR só
adiciona um salto de indireção entre o controller e a lógica. Repositório genérico em cima do
EF Core é uma camada em cima de uma camada que já é o repositório. AutoMapper esconderia
justamente a parte interessante do mapeamento (idade, tempo de espera, cor da tampa), que
não é de-para direto. O enunciado ainda avisa explicitamente: *"se você se pegar montando
arquitetura distribuída pra isso, parou no lugar errado"*.

O que eu **não** abri mão: `LabDesk.Domain` não referencia EF Core nem ASP.NET. É essa
fronteira que garante que as regras estejam no domínio e não espalhadas em serviços.

**Consequência.** Se o projeto crescesse muito, os serviços de aplicação ficariam grandes e
aí sim valeria separar comandos de consultas. Hoje não vale.

---

## 4. `Atendimento` é a raiz do agregado

**Contexto.** A triagem opera sobre uma amostra. O caminho óbvio seria carregar a amostra e
alterá-la.

**Decisão.** Toda transição passa por `Atendimento`. Os métodos de mudança de estado de
`Amostra` são `internal`.

**Por quê.** Rejeitar um tubo não muda só o tubo: muda o status dos exames que ele carregava
e pode colocar o atendimento inteiro em pendência de recoleta. Se a amostra pudesse ser
alterada por fora, esses três estados divergiriam. O status do atendimento é sempre
**derivado dos itens**, nunca atribuído na mão.

**Consequência.** As consultas da triagem carregam um grafo maior do que o estritamente
necessário. Em troca, é impossível deixar o sistema inconsistente.

---

## 5. Um tubo por tipo **e por setor** de destino

**Contexto.** Hemograma e hemoglobina glicada usam o mesmo tubo de EDTA, mas vão para
bancadas diferentes (Hematologia e Bioquímica). Um tubo só não pode estar em dois lugares.

**Decisão.** A coleta agrupa por tipo de tubo **e** setor de destino. No exemplo acima, saem
dois tubos roxos.

**Por quê.** A solução real desse problema é *aliquotagem*: o tubo é centrifugado e o
material dividido em alíquotas para cada setor. Modelar alíquota adicionaria uma entidade e
um passo de fluxo inteiro. Coletar um tubo por setor é uma prática real, resolve o
roteamento sem ambiguidade e mantém o modelo enxuto.

**Consequência.** Em alguns casos coleta-se um tubo a mais do que o mínimo teórico. É um
custo consciente, e a alíquota é o primeiro item da lista de próximos passos.

---

## 6. Jejum bloqueia o check-in

**Contexto.** Um laboratório real costuma permitir seguir com o preparo não cumprido,
registrando a não conformidade e avisando o paciente.

**Decisão.** Se há exame com jejum e a recepção não confirmou o preparo, o atendimento não
abre. A mensagem lista os exames e as horas exigidas.

**Por quê.** A trava rígida deixa a regra explícita e testável, e o custo do erro contrário é
alto: descobrir a falta de jejum depois da punção significa perder o tubo e mandar o paciente
voltar.

**Consequência.** É mais rígido que a realidade. O correto seria permitir seguir com
justificativa e supervisor responsável, registrando a não conformidade. Está na lista de
próximos passos, e a estrutura para isso já existe (`MotivoRejeicao.PREPARO`).

---

## 7. Sem login, mas com responsável registrado

**Contexto.** Autenticação é no-go explícito. Mas todo evento precisa de autor.

**Decisão.** Um seletor de responsável no topo da tela, enviado no cabeçalho
`X-Responsavel` e gravado em cada evento de amostra.

**Por quê.** Saber quem coletou e quem conferiu cada tubo é requisito **de rastreabilidade do
laboratório**, não de segurança da aplicação. Cortar o login não podia significar cortar a
rastreabilidade. A classe `ResponsavelAtual` isola esse ponto: com login, o nome passaria a
vir do usuário autenticado e nada mais no código mudaria.

**Consequência.** Não há nenhum controle de acesso. É deliberado e está documentado.

**Uma armadilha que só apareceu rodando.** O nome do responsável trafega no cabeçalho
`X-Responsavel`, e cabeçalho HTTP só aceita ASCII. Uma recepcionista chamada "João" ou
"Conceição" fazia o servidor recusar **toda** requisição antes de ela chegar na aplicação:
400 com corpo vazio, conexão fechada, sem log e sem mensagem. O valor passou a ir
percent-encoded, e há um teste de integração que cobre exatamente esse caso.

---

## 8. `EnsureCreated` em vez de migrations

**Contexto.** O padrão em EF Core é versionar migrations.

**Decisão.** O schema é criado com `EnsureCreated` na subida.

**Por quê.** O projeto roda em dois provedores (SQLite para clonar e rodar sem Docker,
Postgres no Compose e no deploy), e migrations são específicas por provedor: seriam dois
conjuntos para manter. Como o banco deste recorte é descartável e não existe base com dados
reais para evoluir, migrations custariam manutenção sem entregar nada.

**Consequência.** Não dá para evoluir o schema preservando dados. Num sistema com dados
reais isso seria inaceitável, e migrations entrariam antes do primeiro deploy de verdade.

---

## 9. Dois provedores de banco

**Contexto.** Pedir Docker para rodar o projeto cria atrito na avaliação; usar só SQLite
esconde problemas que só aparecem num banco relacional de verdade.

**Decisão.** SQLite por padrão (`dotnet run` e pronto), Postgres via configuração no Compose
e no deploy.

**Consequência.** A escolha de provedor é uma linha de configuração e o domínio não sabe da
diferença. O custo é a decisão 8.

---

## 10. Datas em UTC, "hoje" no fuso do laboratório

**Contexto.** Os horários de coleta e triagem são registro de rastreabilidade e não podem
aparecer errados.

**Decisão.** Tudo é gravado em UTC (com um conversor global no EF Core, porque o SQLite
devolve `DateTime` sem fuso). O front converte para o horário local do navegador. Mas o
**corte do dia** do painel usa o fuso do laboratório, configurável.

**Por quê.** Este foi um bug real que só apareceu quando rodei o sistema com dados de
demonstração: o painel usava a meia-noite UTC, então às 21h de Brasília o dia virava e os
indicadores zeravam no meio do turno da tarde.

**Consequência.** Uma classe a mais (`RelogioDoLaboratorio`) e o fuso como configuração.

---

## 11. CSS próprio, sem framework de UI

**Contexto.** PrimeVue, Vuetify ou Tailwind acelerariam a montagem das telas.

**Decisão.** CSS próprio, pouco mais de 400 linhas.

**Por quê.** O enunciado diz que UI polida não é necessária. São cinco telas com tabelas,
formulários e etiquetas de status: nada que justifique somar uma dependência grande, uma
configuração de tema e um sistema de componentes que o avaliador teria que conhecer para ler
o código.

**Consequência.** A interface é simples.

---

## 12. Testes: domínio primeiro

**Contexto.** Cobertura total é no-go. Era preciso escolher onde investir.

**Decisão.** 36 testes unitários de domínio (check-in, coleta, triagem, cancelamento e
exames acrescentados ao pedido); 8 de integração passando pela API real com banco real;
4 sobre a carga do catálogo; 2 sobre o corte do dia no fuso do laboratório; e 17 no
frontend, nos formatadores.

**Por quê.** Os testes de domínio cobrem as regras que eu preciso saber explicar: agrupamento
por tubo, ordem de coleta, identificação positiva, consequência de cada tipo de rejeição,
recoleta. Os de integração existem porque **foi exatamente ali que os erros apareceram**: o
mapeamento do EF Core para a relação muitos-para-muitos entre amostra e exame quebrou de
três formas diferentes, e nenhum teste de unidade teria pego isso.

No frontend testei os formatadores porque um deles corrige um bug de verdade: montar `Date`
a partir de `AAAA-MM-DD` faz o navegador aplicar fuso e exibir o dia anterior, o que
trocaria a data de nascimento, que é um dos dois identificadores do paciente.

**Consequência.** Não há teste de componente Vue. Se o layout quebrar, nenhum teste avisa.

---

## 13. Catálogo é dado de referência, sincronizado a cada subida

**Contexto.** A carga inicial populava só banco vazio (`if (await db.TiposTubo.AnyAsync()) return;`).

**Decisão.** A carga do catálogo (tubos, exames e motivos de rejeição) roda a cada subida
e atualiza o que já existe, usando o código (ou a cor, no tubo) como identidade. Pacientes de
exemplo continuam entrando só uma vez.

**Por quê.** Isso apareceu na prática: acentuei o catálogo, o código ficou correto e a tela
continuou mostrando "Amonia" e "Bioquimica", porque o banco já existia e a carga não rodava
mais. Num laboratório, catálogo é dado de referência que muda. Corrige-se o nome de um
exame, revisa-se o volume mínimo de um tubo, troca-se a redação de um motivo. Se corrigir
qualquer um desses exigisse apagar o banco, ninguém corrigiria.

A identidade é o código, nunca o `Id`: recriar o item trocaria a chave e quebraria o vínculo
das amostras e rejeições já registradas.

**Consequência.** Uma edição feita direto no banco é sobrescrita na próxima subida. É o
comportamento correto para dado de referência: não existe tela de edição de catálogo, e a
fonte da verdade é o código.

---

## 14. Data de nascimento digitada em dia/mês/ano, com validação na tela

**Contexto.** O campo era `<input type="date">`, que o navegador renderiza no formato do
idioma dele: num Windows em inglês, a recepção via mm/dd/aaaa. E clicar em "Cadastrar" com
o formulário vazio devolvia "One or more validation errors occurred.".

**Decisão.** Campo de texto com máscara `dd/mm/aaaa` e placeholder explícito, convertido para
ISO antes de ir para a API. Campos obrigatórios marcados com asterisco, botão desabilitado
enquanto o formulário estiver inválido, e erro por campo em português. No servidor, a
validação automática do ASP.NET foi reescrita em português.

**Por quê.** Trocar dia com mês na data de nascimento não é erro de formatação: a data de
nascimento é **um dos dois identificadores** usados na identificação positiva do paciente.
Um campo que mostra mm/dd para quem digita dd/mm produz paciente errado, não data errada.

**Depois:** o campo ganhou um botão de calendário ao lado, que abre o seletor nativo do
navegador e escreve a data de volta no campo em dia/mês/ano. O campo digitável continua
sendo texto com máscara, porque o formato de exibição do `<input type="date">` segue o
idioma do navegador e não é controlável pela página: não dá para garantir dia/mês/ano nele.
Assim quem prefere clicar tem o calendário, e o que aparece escrito na tela é sempre o
formato brasileiro.

**Consequência.** Máscara e parsing escritos à mão, em vez do campo nativo. São ~20 linhas
com teste próprio, incluindo a recusa de datas que não existem (31/02) e a volta completa
entre o calendário e o campo digitado.

---

## 15. Um paciente por vez na fila, com saída para acrescentar exames

**Contexto.** Nada impedia a recepção de abrir um segundo atendimento para um paciente que
já estava na fila com o primeiro em aberto.

**Decisão.** O check-in recusa um pedido novo enquanto o paciente tiver exame aguardando
coleta ou recoleta. Junto com a trava veio a saída: `POST /atendimentos/{id}/exames`
acrescenta exames ao pedido que já existe, refazendo a conferência de jejum.

**Por quê.** O agrupamento por tubo acontece **dentro** de um atendimento. Dois atendimentos
abertos para a mesma pessoa agrupam separadamente, e ela sai com dois tubos roxos onde um
resolveria: é a regra central do sistema sendo furada por fora dela. O paciente na fila duas
vezes também é chamado duas vezes e conta em dobro no tempo médio de espera.

A trava sozinha seria pior que o problema. Sem ter como acrescentar exames, o operador
resolveria cadastrando o paciente de novo com o nome ligeiramente diferente, e um paciente
duplicado no cadastro mina a identificação positiva, que é a regra de segurança mais séria
da coleta. Por isso as duas partes entraram na mesma decisão.

**Consequência.** O check-in passou a fazer uma consulta a mais antes de abrir. Casos
legítimos de dois pedidos simultâneos, como convênios que precisam ser faturados separados,
não são atendidos, mas faturamento está fora do escopo (decisão 2).

---

## 16. Cancelar o atendimento, mas só o que ainda não saiu do braço

**Contexto.** Um atendimento sem coleta não tinha fim. Paciente que desiste da fila, que não
estava em jejum ou que foi aberto por engano ficava na tela para sempre.

**Decisão.** `POST /atendimentos/{id}/cancelar` com motivo de uma lista fechada. O
cancelamento encerra **apenas os exames que ainda não foram coletados**; amostras já
coletadas continuam existindo e seguem para a triagem. O status `Cancelado` só aparece
quando nenhum tubo chegou a ser gerado.

**Por quê.** Sem isso, a decisão 15 viraria uma prisão: um atendimento abandonado impediria
aquele paciente de ser atendido de novo, para sempre. E o atendimento parado não era só
sujeira visual: ele continuava contando tempo de espera e distorcia o indicador do painel.

O limite é a punção. Cancelar um pedido na tela não faz o tubo desaparecer da bancada, e
tubo que existe precisa ser conferido ou recusado pela triagem, com o motivo de rejeição
correspondente. Quem já foi coletado não é cancelado, é triado.

O motivo é lista fechada pela mesma razão dos motivos de rejeição: texto livre não vira
indicador, e evasão de fila é um número que o laboratório acompanha.

**Consequência.** `AtualizarStatus()` ganhou uma condição com duas partes, porque o exame
descartado na triagem também deixa o item cancelado e aquele atendimento **não** é um
cancelamento. O teste que fixava esse comportamento foi o que pegou a primeira versão errada
da regra.

---

## 17. A fila é a lista de trabalho do dia, não o histórico

**Contexto.** A fila mostrava tudo que não estivesse concluído, sem recorte de data e sem
filtro. Paciente já coletado continuava ocupando linha sem ter ação possível ali.

**Decisão.** Filtro rápido por situação, com o padrão mostrando só quem ainda tem tubo a
coletar. A lista cobre o dia corrente, e o que ficou de dias anteriores só continua
aparecendo enquanto a coleta não aconteceu.

**Por quê.** A pergunta que a tela responde é "quem eu chamo agora". Um atendimento em
triagem não tem resposta ali: a ação dele é em outra tela.

O corte por dia resolve o crescimento sem paginar. Paginação combina mal com uma tela que se
atualiza sozinha a cada 15 segundos, porque a página muda debaixo de quem está lendo. E a
exceção importa mais que a regra: o que tem coleta pendente **nunca** é escondido por data,
senão um atendimento abandonado sumiria da tela sem ninguém poder cancelá-lo, que é
exatamente o problema que a decisão 16 resolve.

**O que ficou de fora, de propósito:**

- **Ordenar clicando na coluna.** A ordem da fila não é preferência de visualização, é regra
  de atendimento: pendência de recoleta, depois prioridade legal (idoso, gestante, PCD),
  depois chegada. Um cabeçalho clicável em "Chegada" deixaria o operador montar, com um
  clique e sem perceber, uma fila que ignora a preferência legal. Filtrar o que aparece é do
  operador; a ordem de quem é chamado, não.
- **Bloquear novas aberturas quando a fila estiver grande.** Laboratório não recusa paciente
  que entrou pela porta. Fila longa é informação para o painel, não trava na recepção.

**Consequência.** Quem quiser ver o histórico completo do laboratório não consegue por esta
tela. É o preço de ela ser operacional, e uma tela de gestão sobre o histórico, essa sim com
ordenação livre, está na lista do que eu faria com mais tempo.

---

## 18. Check-in em etapas, com a última servindo de conferência

**Contexto.** A tela de Recepção mostrava as três seções ao mesmo tempo, em duas colunas:
paciente, exames e preparo.

**Decisão.** Uma seção por vez, em acordeão, na ordem do fluxo. O nome do paciente fica no
cabeçalho da etapa recolhida, visível durante todas as outras. A terceira etapa deixou de
ser só o formulário de preparo e virou a conferência do pedido: paciente, exames, prévia dos
tubos e o botão de abrir o atendimento.

**Por quê.** A ordem não é arbitrária, é dependência real: sem paciente não há pedido, e o
preparo só pode ser conferido depois de saber quais exames foram pedidos.

O ganho maior é a conferência. O check-in é o ponto do fluxo em que o erro custa caro:
paciente trocado, exame errado, jejum não perguntado. Depois dele o número do atendimento
existe, o paciente entra na fila e desfazer significa cancelar, que é um dos motivos de
cancelamento previstos (decisão 16). Uma tela que mostra tudo ao mesmo tempo não obriga
ninguém a reler nada antes de confirmar.

**Consequência.** São dois cliques a mais para quem já sabe o que está fazendo, e a recepção
faz isso dezenas de vezes por dia. Por isso qualquer etapa anterior continua clicável a
qualquer momento: é um acordeão com ordem sugerida, não um assistente que prende o operador.
Se isso fosse medido em uso real e o tempo de check-in subisse, o caminho seria um modo
rápido com tudo aberto, não voltar atrás na conferência.
