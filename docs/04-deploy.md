# Deploy da instância de demonstração

O enunciado marca deploy como no-go, e este projeto respeita isso: não há pipeline, IaC,
observabilidade nem ambiente de homologação. A instância pública existe só para facilitar a
avaliação, e subiu em menos de uma hora reaproveitando os Dockerfiles que já existiam para o
`docker compose`.

**O caminho oficial de execução continua sendo `docker compose up --build`.**

---

## Backend — Railway

1. Novo projeto → *Deploy from GitHub repo*.
2. **Root Directory:** `backend` (o `Dockerfile` está lá).
3. Adicione um **PostgreSQL** ao projeto. O Railway injeta `DATABASE_URL`.
4. Variáveis de ambiente:

   | Variável | Valor |
   |---|---|
   | `Banco__DadosDeDemonstracao` | `true` |
   | `Cors__Origens__0` | a URL do front na Vercel, sem barra no final |
   | `Laboratorio__FusoHorario` | `America/Sao_Paulo` |

`PORT` e `DATABASE_URL` são tratados automaticamente por
[`ConfiguracaoDeHospedagem`](../backend/LabDesk.Api/Comum/ConfiguracaoDeHospedagem.cs): a URL
do Postgres vem no formato `postgresql://...`, que o Npgsql não aceita direto, e é convertida
lá.

## Frontend — Vercel

1. Importe o repositório.
2. **Root Directory:** `frontend`. O Vercel detecta Vite sozinho.
3. Variável de ambiente:

   | Variável | Valor |
   |---|---|
   | `VITE_API_URL` | `https://SUA-API.up.railway.app/api` |

   O Vite injeta a variável **no momento do build**, então mudar depois exige um novo deploy.

4. O `vercel.json` já cuida do fallback de rotas da SPA.

## Ordem

Suba a API primeiro, pegue a URL dela para o `VITE_API_URL` do front, e então volte no Railway
para preencher o `Cors__Origens__0` com a URL do front. É a única dependência circular entre
os dois.
