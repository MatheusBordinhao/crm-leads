# CRM de Leads

CRM interno que recebe os leads das campanhas da Gama Marketing (Facebook, Instagram e Google), distribui entre as atendentes e mede quantos viram clientes em cada campanha.

- Regras, contrato com o n8n, modelo de dados e passos de entrega: [docs/especificacao.md](docs/especificacao.md)
- Como desenvolver com o Claude Code: [docs/guia-claude-code.md](docs/guia-claude-code.md)

## Pré-requisitos

- .NET 10 SDK
- Node.js 22 ou mais recente
- Docker Desktop, aberto enquanto você trabalha (o banco local e os testes de integração usam o Docker)

## Primeira vez

1. Crie o `.env` a partir do `.env.example`. Os comandos abaixo geram uma senha aleatória para o banco e a colocam nos dois lugares em que ela é usada (`POSTGRES_PASSWORD` e `ConnectionStrings__Crm`).

   PowerShell:

   ```powershell
   $senha = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object { [char]$_ })
   $linhas = (Get-Content .env.example) -replace '^POSTGRES_PASSWORD=$', "POSTGRES_PASSWORD=$senha" -replace '^(ConnectionStrings__Crm=.*Password=)$', "`${1}$senha"
   [IO.File]::WriteAllText("$PWD\.env", ($linhas -join "`n") + "`n")
   ```

   Bash:

   ```bash
   senha=$(openssl rand -hex 16)
   sed -e "s/^POSTGRES_PASSWORD=$/POSTGRES_PASSWORD=$senha/" \
       -e "s/^\(ConnectionStrings__Crm=.*Password=\)$/\1$senha/" .env.example > .env
   ```

   As outras variáveis ficam vazias até o passo que as usa.

2. Defina o primeiro admin no `.env`, trocando `voce@exemplo.com` pelo seu e-mail. Os comandos geram uma senha aleatória e a mostram no terminal. Ela vale só para o primeiro acesso, quando o sistema pede uma senha nova.

   PowerShell:

   ```powershell
   $senha = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 16 | ForEach-Object { [char]$_ })
   $linhas = (Get-Content .env) -replace '^Admin__Email=.*$', 'Admin__Email=voce@exemplo.com' -replace '^Admin__SenhaInicial=.*$', "Admin__SenhaInicial=$senha"
   [IO.File]::WriteAllText("$PWD\.env", ($linhas -join "`n") + "`n")
   "Senha inicial do admin: $senha"
   ```

   Bash:

   ```bash
   senha=$(openssl rand -hex 8)
   sed -i -e "s/^Admin__Email=.*/Admin__Email=voce@exemplo.com/" \
          -e "s/^Admin__SenhaInicial=.*/Admin__SenhaInicial=$senha/" .env
   echo "Senha inicial do admin: $senha"
   ```

   O admin é criado quando a API sobe e ainda não há nenhum. Depois disso, mudar essas variáveis não tem efeito: os outros usuários são criados na tela de usuários.

3. Instale as ferramentas e as dependências:

   ```bash
   dotnet tool restore      # dotnet ef, para criar migrations
   cd web && npm ci
   ```

## Dia a dia

```bash
docker compose up -d db                  # PostgreSQL 17 em localhost:5432
dotnet run --project src/Crm.Api         # API em http://localhost:5080
cd web && npm run dev                    # front em http://localhost:5173
```

| Endereço | O que é |
| --- | --- |
| http://localhost:5173 | Front. `/api` e `/hubs` são repassados para a API |
| http://localhost:5080/health | Saúde da API e do banco |
| http://localhost:5080/scalar | Documentação da API (só em desenvolvimento) |
| http://localhost:5080/openapi/v1.json | Documento OpenAPI (só em desenvolvimento) |

Em desenvolvimento, a API lê o `.env` da raiz. Uma variável que já exista no ambiente tem prioridade sobre o arquivo. As migrations são aplicadas quando a API sobe, só em desenvolvimento. O arquivo [src/Crm.Api/Crm.Api.http](src/Crm.Api/Crm.Api.http) tem requisições prontas para testar a API pelo VS Code.

### Primeiro acesso

1. Abra http://localhost:5173 e entre com o `Admin__Email` e a senha inicial que o comando da primeira vez mostrou.
2. O sistema pede uma senha nova. Depois disso, a senha do `.env` não vale mais.
3. Em Configurações › Usuários, crie as atendentes. Para testar, use aliases do seu Gmail, como `matheusbordinhao11+adriana@gmail.com`. A tela gera a senha provisória para você copiar e enviar.

Depois de 5 senhas erradas, o login fica bloqueado por 15 minutos. Um admin libera antes redefinindo a senha na tela de usuários.

O cookie da sessão é `Secure`. Chrome, Edge e Firefox o aceitam em `http://localhost`, mas não pelo IP da máquina: para abrir no celular, é preciso HTTPS, que chega no Passo 5. Até lá, confira as telas de celular com a visão de dispositivo do DevTools.

## Tipos da API no front

Os tipos das chamadas à API, em `web/src/api/schema.d.ts`, são gerados a partir do OpenAPI; não os edite à mão. Depois de mudar um endpoint ou um DTO, com a API rodando:

```bash
cd web && npm run api:tipos
```

O arquivo gerado vai para o Git, para o build do front não depender da API. O cliente `api` de [web/src/api/cliente.ts](web/src/api/cliente.ts) usa esses tipos.

## Testes e qualidade

```bash
dotnet test                              # unitários e de integração (estes sobem um PostgreSQL pelo Testcontainers)
cd web && npm test                       # Vitest, sem modo watch (npm run test:watch para o modo watch)

dotnet format                            # formata o C#; --verify-no-changes só confere
cd web && npm run lint                   # ESLint
cd web && npm run format                 # Prettier; format:check só confere
```

O `dotnet test` usa o Microsoft.Testing.Platform, ligado no `global.json`. O build trata avisos como erros.

## Banco de dados

```bash
dotnet ef migrations add <NomeDescritivo> --project src/Crm.Api   # nova migration, em src/Crm.Api/Infra/Dados/Migrations
docker compose down                                               # para o banco e mantém os dados
docker compose down -v                                            # para o banco e APAGA os dados
```

- Uma migration por mudança, com nome descritivo. Nunca edite uma migration já aplicada.
- A senha do banco é gravada quando o volume é criado. Se você trocar `POSTGRES_PASSWORD` depois disso, o banco continua com a senha antiga. Em desenvolvimento, é mais simples recriar o volume com `docker compose down -v`.

## Estrutura

```
src/Crm.Api/                 API (ASP.NET Core, .NET 10), organizada por módulo em Features/ (Usuarios/: acesso e cadastro)
src/Crm.Api/Infra/           DbContext e migrations (Dados/), auditoria (Auditoria/), .env (Configuracao/),
                             ProblemDetails, validação e convenções da API (Web/)
tests/Crm.UnitTests/         regras de negócio, sem banco
tests/Crm.IntegrationTests/  API + PostgreSQL real (Testcontainers)
web/                         React + TypeScript + Vite: features/ por módulo, components/ui/ do shadcn/ui,
                             api/ com o cliente e os tipos gerados
n8n/                         JSON exportado dos fluxos do n8n
docs/                        especificação e guia de uso do Claude Code
```

A publicação (Docker Compose de produção com Caddy e backup) entra no Passo 5.
