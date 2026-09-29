# CRM de Leads

CRM interno que recebe os leads das campanhas da Gama Marketing (Facebook, Instagram e Google), distribui entre as atendentes e mede quantos viram clientes em cada campanha.

A fonte da verdade é [docs/especificacao.md](docs/especificacao.md). Ela traz as regras de negócio, o contrato com o n8n, o modelo de dados, as telas e os passos de entrega com critérios de aceite. Leia a seção do passo atual antes de implementar.

## Situação atual

- Admins: Matheus e o chefe. Atendentes: Adriana e Pietra.
- Ainda não há servidor, domínio, app da Meta liberado nem acesso ao Google Ads. Quando um passo depender disso, deixe tudo pronto, teste com dados simulados e liste o que falta.
- Nos testes, os alertas usam a credencial "Gmail account" do n8n e vão para matheusbordinhao11@gmail.com. Os usuários de teste usam aliases com "+" desse e-mail, como matheusbordinhao11+adriana@gmail.com.
- Chaves e senhas nunca vêm pelo chat: diga o comando e eu rodo no terminal.

## Arquitetura

O CRM guarda os dados e aplica as regras. O n8n só conecta sistemas externos e conversa com o CRM pela API.

- Regra de negócio (deduplicação, rodízio, permissões, funil, métricas) fica no CRM, nunca no n8n.
- O n8n não acessa o banco. Ele usa apenas `/api/integracoes/*`, com a chave de integração.
- O CRM avisa o n8n por eventos: grava em `eventos_saida` na mesma transação da mudança e um serviço em segundo plano entrega. Nunca chame o n8n dentro de uma requisição do usuário.

## Stack

- API: ASP.NET Core (.NET 10) com controllers, EF Core 10 + Npgsql (nomes em snake_case), ASP.NET Core Identity com cookie, SignalR, FluentValidation, libphonenumber-csharp, OpenAPI + Scalar em desenvolvimento
- Banco: PostgreSQL 17
- Web: React + TypeScript + Vite, React Router, TanStack Query, React Hook Form + Zod, Tailwind + shadcn/ui, dnd-kit, Recharts, @microsoft/signalr, vite-plugin-pwa
- Testes: xUnit, WebApplicationFactory + Testcontainers (PostgreSQL), Vitest
- Deploy: Docker Compose com a API servindo o build do React, PostgreSQL e Caddy (HTTPS)
- Pré-requisitos: .NET 10 SDK, Node 22 ou mais recente, Docker
- Não use MediatR, AutoMapper nem FluentAssertions: as versões atuais têm licença comercial. Mapeamento manual e o `Assert` do xUnit bastam.

## Estrutura (criada no Passo 0)

```
src/Crm.Api/                 API, organizada por módulo em Features/ (Leads, Funil, Usuarios, Integracoes, Painel, Notificacoes)
src/Crm.Api/Infra/           DbContext, migrations, despachante de eventos, importação CSV
tests/Crm.UnitTests/         regras de negócio, sem banco
tests/Crm.IntegrationTests/  API + PostgreSQL real (Testcontainers)
web/                         React
n8n/                         JSON exportado dos fluxos do n8n
docs/                        especificação e guia de uso do Claude Code
.claude/                     permissões do projeto e o comando /passo
.mcp.json                    servidor MCP do n8n
```

## Comandos

```bash
docker compose up -d db                                  # PostgreSQL local (precisa do .env; veja o README)
dotnet run --project src/Crm.Api                         # API em http://localhost:5080 (lê o .env e aplica migrations em dev)
cd web && npm run dev                                    # front em http://localhost:5173, com proxy de /api e /hubs
dotnet test                                              # testes da API (os de integração precisam do Docker)
cd web && npm test                                       # testes do front (vitest run, sem modo watch)
dotnet format                                            # formata o C# (--verify-no-changes só confere)
cd web && npm run lint && npm run format:check           # ESLint e Prettier do front
dotnet tool restore                                      # uma vez por clone, para ter o dotnet ef
dotnet ef migrations add <NomeDescritivo> --project src/Crm.Api
```

## Convenções

- Interface, mensagens de erro e commits em português do Brasil.
- Nomes de domínio em português sem acento (`Lead`, `Etapa`, `Interacao`, `Responsavel`); termos técnicos em inglês (`Controller`, `Service`, `DbContext`).
- Datas em UTC no banco (`timestamptz`), exibidas em America/Sao_Paulo.
- Telefone em E.164 (`+5551999998888`); e-mail em minúsculas; CPF/CNPJ como texto, sem máscara e em maiúsculas.
- Uma migration por mudança, com nome descritivo. Nunca edite uma migration já aplicada.
- Os tipos da API no front são gerados a partir do OpenAPI; não os escreva à mão.

## Regras que não podem quebrar

- IMPORTANTE: atendente só vê os próprios leads. O filtro fica na API, em toda consulta de leads, interações, notificações e painel, e lead de outra pessoa responde 404. Todo endpoint novo que toca leads precisa de teste de integração cobrindo isso.
- IMPORTANTE: nenhum dado pessoal (nome, e-mail, telefone, CPF/CNPJ) em log, mensagem de exceção ou telemetria. Registre só o id do lead.
- A entrada de leads é idempotente por `(origem, id_externo)`: um reenvio do n8n nunca duplica lead.
- Entrada vinda de integração nunca é recusada por telefone, e-mail ou documento em formato inválido. O dado é guardado e a ficha mostra um aviso. Na tela, a validação é estrita.
- Toda mudança de etapa grava `historico_etapas`, e ir para Perdido exige motivo.
- Segredos só em variáveis de ambiente ou user-secrets. O `.env` nunca vai para o Git; mantenha o `.env.example` atualizado.

## Armadilhas conhecidas

- Desde julho de 2026 o CNPJ pode ser alfanumérico: 12 caracteres de A a Z ou 0 a 9, mais 2 dígitos verificadores calculados pelo código ASCII menos 48. Nunca guarde CPF/CNPJ como número.
- CSV do Excel brasileiro costuma vir com `;` e Windows-1252; o da Meta pode vir em UTF-16 com tab. A importação detecta codificação e separador.
- No n8n Cloud, cada execução de fluxo conta no limite do plano (Starter: 2.500 por mês), inclusive as de gatilhos agendados. Envie ao n8n só os tipos de evento que algum fluxo usa (`N8n__Eventos`) e não agende fluxos a cada poucos minutos.
- O xUnit v3 roda no Microsoft.Testing.Platform, ligado no `global.json`. Não adicione `Microsoft.NET.Test.Sdk` nem `xunit.runner.visualstudio`: com eles, o `dotnet test` do SDK 10 falha.
- Os testes de integração compartilham um único PostgreSQL (`CrmApiFactory`, assembly fixture). Um teste não pode depender de outro ter rodado antes.
- O n8n Cloud não alcança `localhost`. Para testar o n8n chamando a API local, use um túnel (por exemplo, cloudflared) ou teste o endpoint pelo arquivo `src/Crm.Api/Crm.Api.http`.
- Valores iniciais em tabela com id identity (etapas, motivos de perda) entram por `InsertData` sem id na migration. Com `HasData`, o PostgreSQL não avança a sequence, e o próximo insert colide com o id 1.
- O JSON da API exige todo parâmetro de record sem valor padrão (`RespectRequiredConstructorParameters`). Em DTO de integração, campo opcional precisa de `= null`, e os obrigatórios são conferidos pelo validador, com mensagem em português.
- Todo record `*Request` precisa de um `ToString()` próprio, sem dados pessoais nem senha: o MVC imprime os argumentos da action no log de nível Trace, e o `ToString` gerado pelo compilador imprime todos os campos. Um teste por reflexão cobra isso.
- Navegação de entidade para `Usuario` gera a FK com o nome antigo da tabela do Identity (`fk_..._users_...`). Dê o nome com `HasConstraintName`, como em `Lead.Responsavel`.
- O `openapi-typescript` 7.x declara peer `typescript ^5`, e o front usa o TypeScript 6. Um `overrides` no `web/package.json` o faz usar o TypeScript do projeto. Quando sair uma versão que aceite o 6, atualize-a, tire o `overrides` e confira se o `npm run api:tipos` gera o mesmo `schema.d.ts`.

## n8n

- O servidor MCP `n8n`, configurado em `.mcp.json`, dá acesso à instância de desenvolvimento. Use-o para criar, validar e testar os fluxos da seção 4.5 da especificação.
- Nunca publique, ative nem execute um fluxo sem confirmação do usuário: um fluxo ativo pode mandar e-mail de verdade.
- Todo fluxo do CRM tem o nome começando por `CRM - `. Credenciais ficam no n8n, nunca dentro do fluxo.
- Depois de criar ou alterar um fluxo, salve o JSON dele em `n8n/<nome-do-fluxo>.json`.

## Como trabalhar

- Siga os passos da seção 10 da especificação, na ordem. Um passo só termina quando os critérios de aceite passam.
- O usuário inicia cada passo com `/passo N`. O procedimento completo está em `.claude/skills/passo/SKILL.md`.
- A leitura do `.env` é bloqueada em `.claude/settings.json`. Se precisar de um valor de configuração, use o `.env.example` ou pergunte ao usuário.
- Antes de codar um passo, mostre o plano (arquivos, migrations, endpoints, telas) e espere aprovação.
- Rode `dotnet test` e `npm test` antes de dizer que terminou. Faça commits pequenos, um assunto por commit.
- Se algo mudar uma regra, atualize `docs/especificacao.md` no mesmo commit.
- As decisões em aberto da seção 11 têm um padrão definido. Implemente o padrão e deixe configurável quando a especificação pedir.
