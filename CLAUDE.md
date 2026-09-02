# Instruções para assistentes de IA neste repositório

Este arquivo é versionado de propósito. Ele é o contexto que eu passo para a IA antes de
pedir qualquer coisa, e serve também como registro de quais regras eu impus ao trabalho.

## O que é o projeto

LabDesk: apoio à operação de um laboratório de análises clínicas na **fase pré-analítica**,
da chegada do paciente até a amostra ser conferida e liberada para o setor técnico.

Backend em C# (.NET 10) e frontend em Vue 3. Detalhes em `README.md` e `docs/`.

## Vocabulário do domínio (use estes termos, não invente sinônimos)

| Termo | Significado |
|---|---|
| Atendimento | Ordem de serviço. Um paciente que chegou, com os exames que veio fazer. |
| Item de atendimento | Um exame pedido dentro de um atendimento. |
| Amostra | Um tubo físico coletado. **Não é sinônimo de exame.** |
| Tipo de tubo | Tampa e aditivo (roxa/EDTA, azul/citrato, amarela/gel, cinza/fluoreto, verde/heparina). |
| Ordem de coleta | Sequência dos tubos na punção, para o aditivo de um não contaminar o outro. |
| Triagem | Conferência da amostra antes de liberar para o setor. |
| Motivo de rejeição | Razão padronizada para recusar uma amostra (hemólise, QNS, coágulo...). |
| Recoleta | Nova coleta gerada quando a amostra foi rejeitada. |
| Identificação positiva | Conferir nome completo + data de nascimento com o paciente presente. |
| Setor | Bancada técnica de destino (Hematologia, Bioquímica, Hemostasia...). |
| Cancelamento | Encerrar os exames que ainda **não** foram coletados. Tubo já coletado não é cancelado, é recusado na triagem. |

**A regra que não pode ser quebrada:** vários exames podem ser atendidos por um único tubo.
A coleta agrupa os exames pendentes por tipo de tubo e setor de destino. Nunca gere
uma amostra por exame.

**O corolário dela:** o agrupamento acontece dentro de um atendimento, então um paciente só
pode ter um atendimento com coleta pendente por vez. Exame que chega depois entra no
atendimento que já existe (`AdicionarExames`), não em um novo. Quem não vai coletar sai pelo
cancelamento, senão a trava vira prisão.

## Acentuação: onde usar e onde não usar

A regra é a consequência do texto, não o arquivo em que ele está.

**Leva acento — tudo que uma pessoa lê na tela:**

- Rótulos, títulos, textos, `placeholder` e mensagens no template dos componentes Vue.
- Mensagens de `RegraDeNegocioException` e o `Title` do `ProblemDetails` — o operador lê
  isso no aviso da tela.
- Rótulos de status no `NOMES_DE_STATUS` (os **valores**; as chaves são o enum da API).
- Dados de catálogo do seed que aparecem na tela: nome do exame, aditivo do tubo, setor
  de destino, descrição do motivo de rejeição, nome do paciente.
- `<title>` da página, título e descrição do Swagger.
- Os `/// <summary>` dos **controllers** e dos DTOs: o `IncludeXmlComments` os publica no
  Swagger, então são documentação de API exibida, não comentário interno.
- Documentação em Markdown (`README.md`, `docs/`, este arquivo).

**Não leva acento — tudo que é identificador ou só o desenvolvedor lê:**

- Nomes de classe, método, variável, propriedade, parâmetro, arquivo e namespace.
- Valores de `enum` e as strings que os representam no front (`'AguardandoColeta'`).
- Rotas, nomes de rota do Vue Router, nomes de cabeçalho (`X-Responsavel`), chaves de
  configuração, nomes de tabela e coluna.
- Códigos de catálogo (`HEMOG`, `HBA1C`, `HEMOLISE`) e a cor da tampa (`Roxa`, `Cinza`),
  que também servem de chave em dicionário.
- **Comentários de código** (`//` e `///` fora dos controllers): mantenha ASCII. O código
  circula por ferramentas e ambientes diferentes e ASCII evita problema de encoding.

**Cabeçalho HTTP não trafega acento.** O `X-Responsavel` carrega nome de pessoa, e nome
brasileiro tem acento. O front envia `encodeURIComponent(nome)` e o `ResponsavelAtual`
decodifica com `Uri.UnescapeDataString`. Sem isso o servidor recusa a requisição inteira
antes de ela chegar na aplicação — 400 com corpo vazio, sem log e sem pista.

**Ao mudar uma string, verifique se algum teste faz asserção sobre ela.** Várias mensagens
de negócio são verificadas com `WithMessage("*trecho*")` nos testes de domínio, e o rótulo
de status tem asserção no teste do front.

**Português correto, não só acentuado.** Crase nunca ocorre antes de verbo: é "tubos **a**
coletar", nunca "tubos à coletar".

## Regras de código

- Nomes de classes, métodos e variáveis em **português** para o domínio; sufixos técnicos
  em inglês quando já são convenção (`Controller`, `Dto`, `DbContext`).
- Comentário explica **por que**, nunca o que a linha faz. Se o comentário só repete o
  código, apague o comentário.
- `LabDesk.Domain` não referencia EF Core, ASP.NET nem nada de infraestrutura. Se uma
  regra precisa de banco para funcionar, ela está modelada errado.
- Regras de negócio ficam **dentro das entidades**. Serviços só orquestram: carregam,
  chamam o método do domínio e salvam.
- `Atendimento` é a raiz do agregado. Amostra não é alterada por fora: aceitar ou rejeitar
  um tubo muda também os itens e o status do atendimento, e os três andam juntos.
- Coleções são expostas como `IReadOnlyCollection` com campo privado por trás.
- Um controller por arquivo.
- Mensagens de `RegraDeNegocioException` são lidas pelo operador na tela. Escreva em
  português claro, dizendo o que fazer, não só o que deu errado.

## Comandos

```bash
# Backend
cd backend && dotnet run --project LabDesk.Api      # http://localhost:5080/swagger
cd backend && dotnet test                            # 50 testes

# Frontend
cd frontend && npm run dev                           # http://localhost:5173
cd frontend && npm run test                          # 17 testes

# Tudo junto
docker compose up --build                            # front em :8080, API em :5080
```

## O que NÃO fazer neste projeto

O desafio tem um escopo declarado e um tempo curto. Não adicione, mesmo que pareça
"boa prática":

- Autenticação, JWT, perfis de usuário (fora do escopo por decisão explícita)
- MediatR, CQRS, repositório genérico, Unit of Work, AutoMapper
- Microsserviços, mensageria, cache distribuído
- Resultados de exame, laudos, integração com analisadores (é a fase analítica)
- Faturamento, convênios, TUSS
- Bibliotecas novas sem necessidade clara

Se a sugestão for "vamos abstrair isso para o caso de mudar depois", não.

## Regras sobre informação de domínio

- **Não invente número de norma, percentual ou estatística.** Se não houver fonte
  conferida, escreva o conceito sem o número.
- Mapeamento exame -> tubo, motivos de rejeição e ordem de coleta são informação técnica
  real. Se propuser mudar algo no catálogo, diga de onde veio a informação.
- Prefira sinalizar incerteza a preencher lacuna com algo plausível.

## Depois de alterar qualquer coisa, verifique o que a alteração arrastou junto

Nenhuma mudança neste projeto termina no arquivo que foi editado. Antes de dizer que
acabou, passe por esta lista:

1. **Build e testes.** `dotnet build`, `dotnet test` e `npm run test`. Se a contagem de
   testes mudou, ela aparece na seção "Comandos" deste arquivo e no `docs/02-decisoes.md`.
2. **Regra de negócio quebrada em outro lugar.** `Atendimento` é a raiz do agregado: mexer
   em item, amostra ou status costuma exigir revisar `AtualizarStatus()`. Status de
   atendimento é projeção derivada dos itens, nunca valor atribuído solto.
3. **Contrato da API contra o front.** Campo novo ou renomeado em DTO precisa aparecer em
   `frontend/src/api/tipos.ts`. Enum novo precisa de rótulo em `NOMES_DE_STATUS`, senão a
   tela mostra o valor cru do enum.
4. **Documentação que afirma número ou comportamento.** `README.md` e `docs/` citam
   contagem de testes, quantidade de decisões, linhas de CSS, estrutura de pastas e nomes
   de arquivo. Se o texto afirma algo verificável, confira se ainda é verdade.
5. **Texto de tela.** Regra de acentuação acima, e a asserção de teste que possa existir
   sobre a string alterada.
6. **Efeito no banco existente.** Mudança em entidade ou no catálogo precisa funcionar em
   banco que já existe, não só em banco vazio. O projeto usa `EnsureCreated`, então não há
   migration para consertar dado antigo: quem faz isso é a carga do catálogo.
7. **Coerência com o que já foi decidido.** Se a mudança contradiz uma decisão registrada
   em `docs/02-decisoes.md`, atualize a decisão ou não faça a mudança. Não deixe as duas
   versões convivendo.

O que **não** entra em `docs/02-decisoes.md`: escolha óbvia, sem alternativa razoável. Um
documento de decisões registra o que foi pesado, não o que qualquer pessoa faria igual.
