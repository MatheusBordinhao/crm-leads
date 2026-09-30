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
   $linhas = (Get-Content .env.example -Encoding UTF8) -replace '^POSTGRES_PASSWORD=$', "POSTGRES_PASSWORD=$senha" -replace '^(ConnectionStrings__Crm=.*Password=)$', "`${1}$senha"
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
   $linhas = (Get-Content .env -Encoding UTF8) -replace '^Admin__Email=.*$', 'Admin__Email=voce@exemplo.com' -replace '^Admin__SenhaInicial=.*$', "Admin__SenhaInicial=$senha"
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

3. Gere a chave da integração (`Integracao__Chave`), que o n8n manda no cabeçalho `X-Api-Key` para enviar leads. Os comandos usam um gerador criptográfico e gravam a chave direto no `.env`, sem mostrá-la. Sem chave, a integração fica desligada e responde 401; com menos de 32 caracteres, a API não sobe.

   PowerShell:

   ```powershell
   $bytes = New-Object byte[] 24; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
   $chave = -join ($bytes | ForEach-Object { $_.ToString('x2') })
   $linhas = (Get-Content .env -Encoding UTF8) -replace '^Integracao__Chave=.*$', "Integracao__Chave=$chave"
   [IO.File]::WriteAllText("$PWD\.env", ($linhas -join "`n") + "`n")
   ```

   Bash:

   ```bash
   sed -i "s/^Integracao__Chave=.*/Integracao__Chave=$(openssl rand -hex 24)/" .env
   ```

   A mesma chave vai para a credencial Header Auth do n8n no Passo 6. Para trocá-la, rode o comando de novo e atualize a credencial.

4. Instale as ferramentas e as dependências:

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

Se o banco não tiver nenhum admin (por exemplo, depois de `docker compose down -v`), a API só sobe com `Admin__Email` e `Admin__SenhaInicial` preenchidos no `.env`: rode de novo o passo 2 da primeira vez. Sem eles, a API para com a mensagem "Nenhum admin cadastrado".

Depois de 5 senhas erradas, o login fica bloqueado por 15 minutos. Um admin libera antes redefinindo a senha na tela de usuários.

O cookie da sessão é `Secure`. Chrome, Edge e Firefox o aceitam em `http://localhost`, mas não pelo IP da máquina: para abrir no celular, é preciso HTTPS, que chega no Passo 5. Até lá, confira as telas de celular com a visão de dispositivo do DevTools.

### Funil e ficha

A tela inicial é o funil: uma coluna por etapa aberta, e Ganho e Perdido como áreas de soltar, com quantos leads foram para lá nos últimos 30 dias.

- Arraste o cartão com o mouse, ou pelo teclado: foque a alça do cartão (⋮⋮), aperte espaço, escolha a etapa com as setas para a direita e para a esquerda e aperte espaço de novo. Esc cancela.
- Soltar em Perdido pede o motivo; cancelar devolve o cartão. Ganho exige CPF/CNPJ válido na ficha, a não ser que o admin desligue a exigência.
- No celular (ou na visão de dispositivo do DevTools), o funil vira uma lista por etapa, com um "Mover para" em cada cartão.
- Na ficha, os botões WhatsApp, Ligar e E-mail abrem o contato e, em seguida, o registro da interação com o tipo já escolhido. A primeira ligação, WhatsApp, e-mail ou reunião grava o primeiro contato, e a linha do tempo mostra quem fez o quê e quando.
- As etapas, os motivos de perda e a exigência de CPF/CNPJ para Ganho ficam em Configurações › Etapas e motivos (só admin).

### Leads pela integração

O n8n envia os leads para `POST /api/integracoes/leads` (seção 4.2 da especificação). Para simular um envio sem o n8n, com a API rodando, use a requisição "Lead pela integração" do [src/Crm.Api/Crm.Api.http](src/Crm.Api/Crm.Api.http) e informe o valor de `Integracao__Chave` quando o VS Code pedir.

- Reenviar a mesma requisição responde `ja_recebido` e não duplica o lead. Troque o `idExterno` para simular um lead novo.
- O exemplo vai com `"teste": true`: lead de teste fica sem responsável e só aparece para admins.
- Até o rodízio (Passo 4), os leads da integração ficam sem responsável. O admin os encontra em Leads, com o filtro Responsável › Sem responsável.
- Telefone, e-mail ou CPF/CNPJ em formato inválido não recusam o lead: a ficha mostra o aviso embaixo do campo.

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

O `dotnet test` usa o Microsoft.Testing.Platform, ligado no `global.json`. O build trata avisos como erros. Os testes de integração que mudam o que vale para todos (configurações, ordem das etapas) ficam na coleção `EstadoGlobal`, que roda sozinha depois das outras.

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
src/Crm.Api/                 API (ASP.NET Core, .NET 10), organizada por módulo em Features/: Usuarios/ (acesso e cadastro),
                             Leads/ (leads, entrada, normalização, campanhas, interações e linha do tempo),
                             Funil/ (etapas, motivos de perda, mudança de etapa com histórico e quadro),
                             Integracoes/ (chave e rotas do n8n)
src/Crm.Api/Infra/           DbContext e migrations (Dados/), auditoria (Auditoria/), .env e configurações do sistema
                             (Configuracao/), horário de Brasília (Datas/), ProblemDetails, validação e convenções da API (Web/)
tests/Crm.UnitTests/         regras de negócio, sem banco
tests/Crm.IntegrationTests/  API + PostgreSQL real (Testcontainers)
web/                         React + TypeScript + Vite: features/ por módulo (acesso, funil, leads, configuracoes,
                             usuarios), components/ui/ do shadcn/ui, api/ com o cliente e os tipos gerados
n8n/                         JSON exportado dos fluxos do n8n
docs/                        especificação e guia de uso do Claude Code
```

A publicação (Docker Compose de produção com Caddy e backup) entra no Passo 5.
