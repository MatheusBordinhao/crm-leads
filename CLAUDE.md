# CRM de Leads

CRM interno que recebe os leads das campanhas da Gama Marketing (Facebook, Instagram e Google), distribui entre as atendentes e mede quantos viram clientes em cada campanha.

A fonte da verdade é [docs/especificacao.md](docs/especificacao.md). Ela traz as regras de negócio, o contrato com o n8n, o modelo de dados, as telas e os passos de entrega com critérios de aceite. Leia a seção do passo atual antes de implementar.

## Situação atual

- Admins: Matheus e o chefe. Atendentes: Adriana e Pietra.
- Em 05/10/2026, a ordem do projeto mudou: a empresa só libera o servidor e o domínio depois de ver o sistema funcionando numa demonstração para o chefe, que vem logo depois do Passo 8. A ordem nova está em "Como trabalhar" e na seção 10 da especificação.
- A reunião da demonstração com o chefe, marcada para 15/10/2026, ficou sem data. Desde 16/10/2026, até ela ser marcada, terminamos tudo o que não depende de recurso da empresa, em lotes pequenos (veja "Como trabalhar"). Paramos quando só sobrar o que precisa de servidor, domínio, n8n Cloud pago, e-mail da empresa, acessos da Meta e do Google Ads, decisões da Gama ou das pessoas.
- No fim de cada lote, a demonstração continua funcionando: o `preparar-demonstracao`, um lead pelo rodízio (o `site.json`, pelo n8n local) e o e-mail de alerta no celular, que eu confiro.
- Quando a reunião for marcada, paramos e congelamos: termine o commit em andamento ou desfaça o que não foi commitado, rode as suítes, reconstrua o plano B e rode a massa da demonstração nos dois ambientes. Depois disso, nada muda até a reunião: o código do CRM e do n8n, o Compose, os testes, os JSONs de `n8n/` e a documentação.
- Ainda não há servidor, domínio, n8n de produção, app da Meta liberado nem acesso ao Google Ads. Quando um passo depender disso, deixe tudo pronto, teste com dados simulados e liste o que falta.
- Os usuários de teste usam aliases com "+" de matheusbordinhao11@gmail.com, como matheusbordinhao11+adriana@gmail.com.
- Chaves e senhas nunca vêm pelo chat: diga o comando e eu rodo no terminal.
- O banco de desenvolvimento já tem o admin, a Adriana e a Pietra (aliases +adriana e +pietra), e o `.env` tem `Admin__Email`, `Admin__SenhaInicial`, `Integracao__Chave` e, desde 03/11/2026, a linha `Integracao__ChaveAnterior`, vazia fora da troca da chave.
- O n8n Cloud de teste (`matheusbordinhao.app.n8n.cloud`) acabou em 06/10/2026. A produção vai ser o n8n Cloud, numa conta da empresa, quando o pagamento sair (decidido em 14/10/2026).
- Desde 14/10/2026, o n8n de desenvolvimento é um n8n Community no `docker-compose.yml`, na 2.43.7 desde 04/11/2026 (antes, na 2.43.5) (serviço `n8n`, README › n8n local):
  - a interface fica em `http://localhost:5679`, porque a 5678 é de outro n8n desta máquina, um projeto do Compose no WSL (`/home/mathe/n8n`) que não é do CRM e não deve ser mexido;
  - dentro da rede do Compose, a API chama `http://n8n:5678`, e os fluxos chamam `http://api:5080`;
  - o `.env` tem `N8n__WebhookUrl=http://n8n:5678/webhook/crm-alertas`: com a API no ar, cada evento vira um e-mail de verdade;
  - as credenciais são `CRM - Chave de integração` (`X-Api-Key`), `CRM - Chave dos eventos` (`X-Crm-Chave`) e `CRM - E-mail` (SMTP do `testecrmfs@gmail.com`, uma conta só para os testes, com senha de app);
  - as Data Tables são a `crm_entradas_pendentes`, a fila do reenvio, a `crm_entradas_recusadas`, com as entradas recusadas, por 7 dias, e a `crm_envios_site`, com a hora de cada envio do site, por 31 dias;
  - a chave do Google de desenvolvimento fica em `C:\dev\crm-google-chave-dev.txt`, fora do repositório, e só o SHA-256 dela está no fluxo. Ela foi trocada em 03/11/2026, no ensaio do Lote 3.
- Fluxos no n8n local:

  | Fluxo | Id | Situação |
  | --- | --- | --- |
  | `CRM - Erros` | `jp3A8ziIhGPROWOc` | publicado |
  | `CRM - Alertas` | `GbDVJGhdvk5RI4F0` | publicado |
  | `CRM - Entrada Site` | `VKBgz0tUZJTLkbmP` | publicado |
  | `CRM - Entrada Google` | `3dm8urmanT4Fg0iP` | publicado |
  | `CRM - Reenvio de pendentes` | `yDU0FZa2tXN851ru` | não publicado: roda à mão |
  | `CRM - Relatório semanal Gama` | `ln7BS60hTE8HQ3Pp` | não publicado: roda à mão |
  | `CRM - Resumo diário` | `hdga8JN0W5kRMNrt` | não publicado: roda à mão |
  | `CRM - Entrada Meta` | `GqSyYXbHtcv6vvsh` | não publicado: depende do app da Meta em modo Live |

  Os JSONs de `n8n/` são os exportados deles.
- Depois do congelamento e antes da reunião, reconstrua o plano B (`crm-local`) com o código congelado: `docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env up -d --build` (README › Demonstração › Plano B). O plano B não tem o n8n: nele, não sai e-mail de alerta.
- Para a demonstração do e-mail no celular, deixe no ar a API e o n8n (`docker compose up -d api n8n`), com o `CRM - Erros` e o `CRM - Alertas` publicados, e rode o `preparar-demonstracao`, que deixa a Adriana como a próxima da vez (README › Demonstração › O alerta no celular).

## Arquitetura

O CRM guarda os dados e aplica as regras. O n8n só conecta sistemas externos e conversa com o CRM pela API.

- Regra de negócio (deduplicação, rodízio, permissões, funil, métricas) fica no CRM, nunca no n8n.
- O n8n não acessa o banco. Ele usa apenas `/api/integracoes/*`, com a chave de integração.
- O CRM avisa o n8n por eventos: grava em `eventos_saida` na mesma transação da mudança e um serviço em segundo plano entrega. Nunca chame o n8n dentro de uma requisição do usuário.

## Stack

- API: ASP.NET Core (.NET 10) com controllers, EF Core 10 + Npgsql (nomes em snake_case), ASP.NET Core Identity com cookie, SignalR, FluentValidation, libphonenumber-csharp, OpenAPI + Scalar em desenvolvimento
- Banco: PostgreSQL 17
- Web: React + TypeScript + Vite, React Router (com as telas carregadas sob demanda, pelo `tela()` de `web/src/rotas.tsx`), TanStack Query, React Hook Form + Zod, Tailwind + shadcn/ui, dnd-kit, @microsoft/signalr. Os gráficos do painel são barras em HTML e CSS, sem biblioteca; o Recharts só entra quando um gráfico precisar dele, como uma linha por dia. O PWA é feito à mão, sem o vite-plugin-pwa: o manifesto, os ícones e o service worker ficam em `web/public`, e o service worker não guarda cache
- Testes: xUnit, WebApplicationFactory + Testcontainers (PostgreSQL), Vitest
- Deploy: Docker Compose com a API servindo o build do React, PostgreSQL e Caddy (HTTPS)
- Pré-requisitos: .NET 10 SDK, Node 22 ou mais recente, Docker
- Não use MediatR, AutoMapper nem FluentAssertions: as versões atuais têm licença comercial. Mapeamento manual e o `Assert` do xUnit bastam.

## Estrutura (criada no Passo 0)

```
src/Crm.Api/                 API, organizada por módulo em Features/ (Leads, Funil, Usuarios, Integracoes, Painel, Notificacoes)
src/Crm.Api/Infra/           DbContext, migrations, auditoria, despachante de eventos, importação CSV, tempo real, proxy, chaves da sessão, front e o comando da demonstração
tests/Crm.UnitTests/         regras de negócio, sem banco
tests/Crm.IntegrationTests/  API + PostgreSQL real (Testcontainers)
web/                         React
n8n/                         JSON dos 8 fluxos do n8n e, em exemplos/, os payloads fictícios dos testes
deploy/                      Caddyfile e scripts de backup e de teste da restauração, do Compose de produção
docs/                        especificação, guia de uso do Claude Code e CSVs de exemplo da importação (exemplos/)
Dockerfile                   imagem de produção: a API com o build do React e o migration bundle
docker-compose.yml           desenvolvimento: PostgreSQL, a API e o dotnet ef num contêiner Linux, e o n8n local
docker-compose.prod.yml      produção: Caddy, API, PostgreSQL, migrations e backup
docker-compose.testes.yml    os testes .NET num contêiner Linux
.claude/                     permissões do projeto e o comando /passo
.github/                     o CI (os workflows e, em scripts/, o front.sh e o resumo das 5 rodadas), a conferência dos binários nativos do lockfile do front (scripts/nativos-do-lock.mjs) e o Dependabot (um PR por mês com as imagens, outro com o NuGet e outro com as ações, sem o npm)
.mcp.json                    servidor MCP do n8n Cloud (o do n8n local fica no ~/.claude.json, fora do Git)
```

## Comandos

```bash
docker compose up -d db                                  # PostgreSQL local (precisa do .env; veja o README)
docker compose up -d api                                 # API de dev num contêiner, em http://localhost:5080, com o banco (nesta máquina, o jeito que roda)
docker compose logs -f api                               # log da API do contêiner
docker compose restart api                               # recompila e sobe de novo, depois de mudar o código da API
docker compose stop api                                  # para a API do contêiner (o docker compose down para ela e o banco)
dotnet run --project src/Crm.Api                         # API no Windows, onde as DLLs carregam (lê o .env e aplica migrations em dev)
cd web && npm run dev                                    # front em http://localhost:5173, com proxy de /api e /hubs
dotnet test                                              # testes da API (os de integração precisam do Docker)
docker compose -f docker-compose.testes.yml run --rm testes   # os testes .NET num contêiner Linux (nesta máquina, o jeito que roda)
cd web && npm test                                       # testes do front (vitest run, sem modo watch)
dotnet format                                            # formata o C# (--verify-no-changes só confere)
MSYS_NO_PATHCONV=1 docker compose -f docker-compose.testes.yml run --rm -e ArtifactsPath=/artefatos --entrypoint dotnet testes format Crm.sln --verify-no-changes   # o mesmo, pelo contêiner dos testes
cd web && npm run lint && npm run format:check           # ESLint e Prettier do front
sh .github/scripts/front.sh completo                     # o front como no CI, num contêiner Alpine, sem tocar no node_modules do Windows
MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/repo:ro" -w /repo rhysd/actionlint:1.7.12   # confere os workflows do CI
node .github/scripts/nativos-do-lock.mjs                 # o lockfile do front contra o do último commit: o que mudou e se os binários nativos ficaram iguais
docker compose run --rm ef migrations add <NomeDescritivo>   # dotnet ef num contêiner (nesta máquina, o jeito que roda)
dotnet tool restore                                      # uma vez por clone, para ter o dotnet ef no Windows
dotnet ef migrations add <NomeDescritivo> --project src/Crm.Api
docker compose run --rm --service-ports api --N8n:WebhookUrl=   # API sem mandar eventos ao n8n, com o api parado (no Windows: dotnet run --project src/Crm.Api -- --N8n:WebhookUrl=)
docker compose run --rm -e Demo__Permitir=true api preparar-demonstracao   # APAGA os leads e grava a massa da demonstração, com o api parado (README › Demonstração)
docker compose -f docker-compose.prod.yml up -d --build  # produção, no servidor (README › Publicação)
docker compose -p crm-local -f docker-compose.prod.yml --env-file <arquivo> up -d --build   # produção no próprio PC, com PORTAS_IP=127.0.0.1 no arquivo
docker compose up -d n8n                                 # n8n local em http://localhost:5679 (README › n8n local)
```

## Convenções

- Converse com o usuário sempre em português do Brasil.
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
- CSV do Excel brasileiro costuma vir com `;` e Windows-1252; o da Meta pode vir em UTF-16 com tab. A importação detecta codificação e separador e tira os prefixos da exportação da Meta (`l:`, `c:`, `as:`, `ag:`, `f:`, `p:`). Os CSVs de exemplo ficam fora da normalização de fim de linha do Git (`*.csv -text`), que trocaria o CRLF do arquivo do Excel.
- No n8n Cloud, cada execução de fluxo conta no limite do plano (Starter: 2.500 por mês), inclusive as de gatilhos agendados. Envie ao n8n só os tipos de evento que algum fluxo usa (`N8n__Eventos`) e não agende fluxos a cada poucos minutos.
- O xUnit v3 roda no Microsoft.Testing.Platform, ligado no `global.json`. Não adicione `Microsoft.NET.Test.Sdk` nem `xunit.runner.visualstudio`: com eles, o `dotnet test` do SDK 10 falha.
- Os testes de integração compartilham um único PostgreSQL (`CrmApiFactory`, assembly fixture). Um teste não pode depender de outro ter rodado antes. Para conferir que o banco tem certos registros, como os motivos iniciais, use `Assert.Superset`: o `Assert.Subset` confere o contrário e falha quando outro teste criou um registro antes.
- A busca de leads procura os algarismos do texto (4 ou mais) também no telefone e no CPF/CNPJ de todos os leads. Por isso, os testes não sorteiam esses dados:
  - o telefone, o CPF e os ids da Meta vêm de um contador (`LeadsDeTeste.NumeroUnico`);
  - a marca de busca (`LeadsDeTeste.Marca()`) não tem algarismos.
  - Uma marca com algarismos, como um pedaço de Guid, achava na busca os leads de outros testes cujo telefone tinha os mesmos algarismos, e o teste falhava de vez em quando.
  - O contador recomeça a cada rodada, e isso só funciona porque o banco também nasce vazio: o contêiner do PostgreSQL não é reaproveitado, e a `CrmApiFactory` para a suíte se encontrar leads. Não ligue o `WithReuse` do Testcontainers.
- Teste que muda o que vale para todos (uma configuração, a ordem das etapas, o último motivo ativo) fica na coleção `EstadoGlobal`, que roda sozinha depois das outras, e restaura o estado no fim. Os outros testes só criam etapas novas, que entram no fim das abertas: a etapa inicial continua sendo Novo.
- Quem participa do rodízio recebe os leads de todos os testes. Teste que deixa um usuário ativo com `participa_rodizio`, mesmo por um instante, fica na `EstadoGlobal` e usa o `RodizioIsolado`. Nos outros, crie usuários com `participaRodizio: false`: na fase paralela, ninguém está no rodízio, e os leads da integração ficam sem responsável.
- Os admins ativos recebem os avisos de todos os testes, como o de lead sem responsável. Para conferir as notificações de um admin, filtre pelo lead ou por um título só do teste.
- O despachante lê a fila inteira de `eventos_saida`. Os testes dele usam um banco só deles (`BancosDeTeste.CriarBancoAsync`) e o `FakeTimeProvider`. O serviço em segundo plano só roda com `N8n__WebhookUrl` configurada.
- A hora que passa a vez do rodízio é lida depois da trava `rodizio`. Lida antes, entradas simultâneas gravam horas fora de ordem, e a mesma pessoa recebe dois leads seguidos.
- No n8n, um fluxo só pode ser o fluxo de erro de outro depois de publicado.
- `FromSql("SELECT * FROM leads ...")` falha: o `SELECT *` não traz a coluna de sistema `xmin`, que é a `Versao` do lead. Para travar leads, use `SELECT 1 ... FOR UPDATE` (como em `TravarLeadAsync`) e leia pelo LINQ.
- No quadro do funil, o `KeyboardSensor` do dnd-kit troca o movimento por rolagem numa área que rola, e a etapa embaixo do cartão (`over`) só se atualiza depois do render. Por isso o quadro não rola para o lado e a navegação pelo teclado guarda a etapa escolhida, que é a que vale ao soltar (`navegacaoPorTeclado.ts`).
- Com um diálogo do Radix aberto, o resto da página fica com `aria-hidden`: nos testes do front, consulte o que está fora do diálogo com `hidden: true`.
- O n8n Cloud não alcança `localhost`. Para testar o n8n chamando a API local, use um túnel (por exemplo, cloudflared) ou teste o endpoint pelo arquivo `src/Crm.Api/Crm.Api.http`.
- Valores iniciais em tabela com id identity (etapas, motivos de perda) entram por `InsertData` sem id na migration. Com `HasData`, o PostgreSQL não avança a sequence, e o próximo insert colide com o id 1.
- O JSON da API exige todo parâmetro de record sem valor padrão (`RespectRequiredConstructorParameters`). Em DTO de integração, campo opcional precisa de `= null`, e os obrigatórios são conferidos pelo validador, com mensagem em português.
- Todo record `*Request` precisa de um `ToString()` próprio, sem dados pessoais nem senha: o MVC imprime os argumentos da action no log de nível Trace, e o `ToString` gerado pelo compilador imprime todos os campos. Um teste por reflexão cobra isso.
- O `Request starting` e o `Request finished` do ASP.NET Core (`Microsoft.AspNetCore.Hosting.Diagnostics`, em Information) gravam a URL com a query string, inclusive a busca da lista e da exportação, que pode ser um nome, um e-mail ou um telefone.
  - Só o `Microsoft.AspNetCore` em Warning, no `appsettings.json`, deixa esses registros de fora: não baixe esse nível em produção, nem para investigar um problema.
  - Conferido em 12/10/2026 por um teste temporário: em todos os níveis, a busca saiu nos 4 registros da lista e da exportação; com os níveis da configuração, em nenhum.
  - Um caso da `LogsTests`, que captura todos os níveis, com uma busca por um dado pessoal falharia por esses registros.
- Navegação de entidade para `Usuario` gera a FK com o nome antigo da tabela do Identity (`fk_..._users_...`). Dê o nome com `HasConstraintName`, como em `Lead.Responsavel`.
- O `openapi-typescript` 7.x declara peer `typescript ^5`, e o front usa o TypeScript 6. Um `overrides` no `web/package.json` o faz usar o TypeScript do projeto. Quando sair uma versão que aceite o 6, atualize-a, tire o `overrides` e confira se o `npm run api:tipos` gera o mesmo `schema.d.ts`.
- O serviço da importação retoma qualquer importação com a reserva vencida, de qualquer teste. Os testes de retomada usam um banco só deles e tiram o `ServicoImportacoes` (`ImportacaoRetomadaTests`), como os do despachante.
- Uma rota que aceita qualquer método (`MapFallback`, `MapFallbackToFile`) faz um método errado numa rota da API responder 404 em vez de 405. Por isso o `index.html` é servido fora do roteamento, como último recurso de um GET (`FrontExtensions.UsarFront`).
- O `UseForwardedHeaders` com as listas de proxies vazias confia em qualquer um. Sem `Proxy__Ip`, o `ProxyReverso` desliga os cabeçalhos encaminhados (`ForwardedHeaders.None`).
- O `TestServer` não tem IP de conexão. Para testar o IP de quem acessa, use `Server.SendAsync` e defina o `Connection.RemoteIpAddress` (`ProxyTests`).
- O `TestServer` também não aplica o `[RequestSizeLimit]`: o corpo acima do limite passa. O teste do 413 (`IntegracaoLeadsTests`) sobe o Kestrel de verdade, com `UseKestrel(opcoes => opcoes.Listen(IPAddress.Loopback, 0))`. Sem o `Listen`, ele escuta no `[::]:8080` do `ASPNETCORE_HTTP_PORTS`, e o cliente não conecta.
- O `UseExceptionHandler()` responde 500 a toda exceção que escapa, inclusive à `BadHttpRequestException` do corpo grande demais. O `StatusCodeSelector` (`Program.cs`) passa o código dela. O título do 413 em português vem do `ProblemDetailsPtBr`; o padrão é "Content Too Large", da RFC 9110.
- A CSP do Caddy (`deploy/Caddyfile`) só aceita a própria origem, mais os estilos inline do sonner e do Radix, e não tem `'unsafe-eval'`. Um recurso externo novo (fonte, imagem, script) precisa entrar nela. O `connect-src` repete o domínio com `wss://`, para o WebSocket do tempo real no Safari antigo; com `DOMINIO=localhost` numa porta que não é a 443, quem cobre o WebSocket é o `'self'`, o que basta no Chrome.
- O filtro do log do Caddy (`format filter`, no bloco global do `deploy/Caddyfile`) vale para o log padrão, onde caem o log de erro e o de todos os módulos. Um log de acesso (a diretiva `log` no site) teria o próprio formato, sem o filtro: se for ligado, ele precisa do mesmo filtro. O `LogDoCaddyTests` pega um vazamento no stderr, mas não num arquivo de log.
- Com a API parada no Docker Desktop, o Caddy leva 3 s para responder 502, porque o nome `api` não resolve (`lookup api: i/o timeout`). No `LogDoCaddyTests`, o próprio Caddy responde pelo nome `api` na rede do teste, sem nada na 8080, e o 502 sai na hora (`connection refused`).
- O `z` vem de `@/lib/zod`, que liga o `jitless` antes de qualquer esquema. Com o `z` direto do `zod`, o Zod testa o `new Function` ao criar cada esquema de objeto, e o Chrome registra a violação da CSP, mesmo com o erro tratado. O ESLint barra o import de `zod`.
- Para ver o console e os issues do DevTools sem abrir o navegador (por exemplo, uma violação da CSP), use o Chrome sem janela (`--headless --remote-debugging-port=<porta> --user-data-dir=<pasta temporária>`) e o protocolo do DevTools (`Log`, `Runtime` e `Audits.issueAdded`), contra o Compose de produção local. Os quadros do WebSocket do tempo real chegam pelo `Network.webSocketFrameReceived`.
- Todo campo de formulário precisa de `id` ou `name`: sem os dois, o DevTools acusa `FormEmptyIdAndNameAttributesForInputError`. O `web/src/campos.test.tsx` passa pelas telas e pelos diálogos e cobra isso. Conta também o input escondido que o Radix cria para o RadioGroup e o Switch dentro de um `<form>`: passe o `name` para eles.
- Os avisos do tempo real saem de um interceptador do EF Core (`InterceptadorTempoReal`), que anota cada `SaveChanges` e só entrega à `FilaTempoReal` depois do commit. O que não passa pelo `SaveChanges` (`ExecuteUpdate`, SQL direto) não avisa: uma mudança de lead feita assim precisa chamar a `FilaTempoReal.Publicar`. O interceptador guarda o estado por instância do contexto; com pool de contextos, isso precisaria mudar.
- Uma fábrica do `WithWebHostBuilder` tem o próprio hub e o próprio publicador. Nos testes do tempo real, conecte o hub e faça as ações pela mesma fábrica. Os testes que precisam ver o que o interceptador entrega tiram o `PublicadorTempoReal` e leem a fila (`InterceptadorTempoRealTests`).
- O cliente do SignalR dos testes (`TempoRealDeTeste`) fala com o TestServer por Server-Sent Events, com o cookie da sessão no cabeçalho. O início do cliente termina antes de o hub pôr a conexão nos grupos (`OnConnectedAsync`); por isso, o `ConectarAsync` espera o registro da conexão antes de devolver.
- O Vitest 5 não mostra o console dos testes que passam, nem o aviso do MSW de uma requisição sem tratador, que só faz a requisição falhar. Para vê-los, rode `npx vitest run --silent=false`.
- Nos testes do front, o admin em `/` vai para o painel, que chama `/api/painel`: registre o `comPainel()`. A atendente em `/` vai para o funil (`comQuadro()`).
- Teste que confere os totais do admin, como os do painel, usa um banco só dele (`MassaDoPainel`): no banco compartilhado, os leads dos outros testes entram na conta. Lá, isole os números por uma campanha única ou por uma atendente nova.
- A `MassaDoPainel` é criada uma vez (`ObterAsync`) e lida pelos testes do painel e do relatório. Nenhum teste pode mudá-la.
- Sem `de` e `ate`, as rotas do relatório na integração devolvem a semana anterior, e as do admin, os últimos 30 dias. Um teste que cria leads agora e chama a integração passa as datas, como o `RelatorioTests`.
- Para abrir um CSV no Excel por automação (COM) como no clique duplo, passe `Local` verdadeiro no `Workbooks.Open` (o 14º parâmetro). Sem ele, o Excel separa as colunas pela vírgula, e não pelo ponto e vírgula do Windows em português.
  - Enquanto o arquivo abre, o Excel recusa chamadas (`RPC_E_CALL_REJECTED`): repita a chamada.
  - Uma função do PowerShell que devolve um `Range` o desenrola em células: devolva com a vírgula (`return , $resultado`).
  - Um Excel de automação que fica aberto não tem janela: encerre só esse, nunca um do usuário.
- Nos testes do front, o hub é falso (`test/hubFalso.ts`, devolvido pelo `renderizarApp`): emita os avisos com `hub.emitir`. O MSW recusa requisições não tratadas, e o hub de verdade faria uma. O jsdom não tem a `Notification`: os testes da notificação nativa a trocam por uma falsa (`vi.stubGlobal`) e controlam o `document.visibilityState`.
- A reconexão automática do SignalR não tenta de novo quando o primeiro início falha nem quando o servidor encerra a conexão (sessão encerrada, perfil trocado). O `TempoReal` faz a própria, conferindo a sessão antes de cada tentativa: só um 401 leva ao login.
- Com a aba escondida e a notificação nativa ligada, o `TempoReal` busca a lista do sino pelo `fetchQuery`, para ter o título. Uma invalidação com `cancelRefetch` (o padrão) cancelaria essa busca, e a notificação não apareceria: a recarga do sino usa `cancelRefetch: false`.
- Nos testes do front, numa seleção cuja lista vem de outra consulta (campanhas, usuários), espere a opção aparecer (`findByRole('option')`) antes do `selectOptions`: com a suíte inteira rodando, a lista pode chegar depois.
- No Git Bash, um caminho como `/backup/backup.sh` num `docker compose exec` vira um caminho do Windows. Rode com `MSYS_NO_PATHCONV=1`.
  - Vale também para um `-e NOME=/caminho` do `docker compose run`: o `-e DataProtection__Pasta=/tmp/chaves` virou `C:/Users/.../Temp/chaves`, a pasta caiu dentro do código, que no contêiner é só leitura, e o login deu 500.
  - Com o `MSYS_NO_PATHCONV=1` exportado no script, é o contrário: o curl, que é do Windows, não entende um caminho `/c/...`, como o do `-c` dos cookies, e a sessão se perde. Para ele, use `C:/...`.
- Com a API rodando no Windows (`dotnet run`), o build do Windows falha ao copiar o `Crm.Api.exe`, que fica travado. Pare a API ou use o contêiner dos testes, que compila num volume próprio. Para só conferir que compila, use `dotnet build Crm.sln --artifacts-path <pasta do scratchpad>`, que não escreve no `bin/`. A API do contêiner não trava nada no Windows: o build dela fica no volume `artefatos-api`.
- Para gerar os tipos do front com a API do contêiner, rode `docker compose restart api`, para ela compilar o código novo, e depois o `npm run api:tipos`.
- O serviço `api` e um `docker compose run ... api` (o `preparar-demonstracao`, a API sem o n8n) compilam no mesmo volume e usam a porta 5080: pare o `api` antes.
- Com a API no ar e a configuração igual, o `docker compose up -d api` não faz nada: ela continua com o código e os pacotes que compilou ao subir. Para ela compilar de novo, como num PR do NuGet, use o `docker compose restart api` ou o `docker compose up -d --force-recreate api`, que recria só a API. Conferido em 20 e 21/10/2026: o mesmo contêiner, com a mesma hora de início, no `up -d api`, e o banco e o n8n no ar depois do `--force-recreate api`.
- A `api` de desenvolvimento não tem `restart`, por decisão minha de 21/10/2026. Depois de atualizar ou reiniciar o Docker Desktop, o `db` e o `n8n` voltam sozinhos (`restart: unless-stopped`), e a `api` precisa do `docker compose up -d api` (README › Dia a dia). Com o `restart`, o código sem compilar faria a `api` recompilar em laço, a cada uns 30 s e com quase 2 CPUs, sem ninguém ver. Não ponha o `restart` nela sem pesar isso.
- Para testar a API do contêiner sem tocar no banco de desenvolvimento, suba um PostgreSQL descartável na rede `crm-leads_default` e passe no `docker compose run`:
  - `-e ConnectionStrings__Crm=...` com o nome dele e `Admin__*` fictícios;
  - `-e DataProtection__Pasta=/tmp/chaves`, para as chaves não irem para o volume `chaves-sessao`;
  - e o `--N8n:WebhookUrl=` no fim, porque a API descartável lê o `.env` de verdade: o que não for trocado assim vem dele, inclusive a URL do n8n, quando houver.
- No Windows, um argumento com acento passado ao `curl` perde o UTF-8, e a API responde 400. Mande o corpo de um arquivo (`--data-binary @arquivo`).
- Com uma importação processando, de qualquer teste, anonimizar responde 409. Os testes que anonimizam usam um banco só deles: o `AnonimizacaoTests` e o caso da LGPD no `LogsTests`.
- A entrada de um lead anonimizado guarda `anon:` e o SHA-256 do id externo (`IdExternoAnonimo`), e a idempotência procura pelo id e pelo hash. O hash não pode mudar: se mudasse, o reenvio de um lead anonimizado criaria a pessoa de novo. O `IdExternoAnonimoTests` fixa o valor.
- O `jsonb` reordena as chaves e põe espaços. Nos testes, compare os detalhes da auditoria pelos valores, e não pelo texto.
- A ordem das linhas do painel desempata pelo nome, e os testes do banco compartilhado repetem nomes, como a campanha "Outubro | Serviço Y" e a "Atendente de teste". Num `UPDATE`, a ordem dos empatados pode mudar. Para comparar o JSON inteiro do painel, use um banco próprio e nomes únicos.
- O JSON do titular é escrito com o `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, porque o codificador padrão escreve `Conceição`. Isso é seguro aqui: o arquivo é baixado como `application/json`.
- O contêiner dos testes (`docker-compose.testes.yml`) monta o código só para leitura, compila com `--artifacts-path` num volume e usa `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`, porque o PostgreSQL dos testes sobe no Docker do computador. Teste que precisa achar o repositório parte do próprio arquivo de código (`[CallerFilePath]`, como no `PublicacaoTests`), e não do `AppContext.BaseDirectory`, que no contêiner fica fora dele.
- O comando `preparar-demonstracao` apaga todos os leads, as campanhas e as importações.
  - Ele só roda com `Demo__Permitir=true` no ambiente e com o `Crm__UrlPublica` em `localhost`.
  - A variável nunca vai para um `.env`, e o `docker-compose.prod.yml` não a repassa à API.
  - Não rode o comando no banco de desenvolvimento sem eu pedir: ele apagaria os meus leads. Para conferir, use um banco de teste (`DemonstracaoTests`) ou um Compose descartável.
  - O comando roda antes do `InicializarAcessoAsync` e termina sem subir a API: no `Program.cs`, o que vem depois do desvio não vale para ele.
- O service worker do PWA (`web/public/sw.js`):
  - só é registrado no build de produção (`registrarServiceWorker`, em `web/src/pwa/registro.ts`) e nunca no `localhost:5173`;
  - também não é registrado no `https://localhost` do Compose de produção local. O Chrome o recusa por causa do certificado local do Caddy e registra um erro no console a cada página;
  - não tem evento `fetch` nem cache, de propósito: não intercepta a API nem o tempo real e nunca serve uma versão velha. Antes de dar a ele um `fetch` ou um cache, pense em como uma publicação nova chega a quem já está com a tela aberta;
  - para testá-lo no computador, use `npm run build` e `npm run preview` (`localhost:4173`, que repassa `/api` e `/hubs` como o `npm run dev`).
- Os testes do front leem os arquivos de `web/public` por import com `?raw` e `?inline` (o `sw.js` roda num `self` falso, pelo `new Function`). O `tsconfig.app.json` não tem os tipos do Node: não use `node:fs` nos testes.
- Os PNGs do PWA saem de `web/icones/gerar.sh`, num contêiner Alpine (`rsvg-convert`, README › Instalar como app). Não edite os PNGs à mão: mude o SVG e gere de novo.
- O Npgsql 10 tenta a criptografia GSS (Kerberos) antes da senha, e a imagem Linux da API não tem a biblioteca do Kerberos. A connection string sai da `DadosExtensions.SemGss` com o GSS desligado; sem isso, volta o `Cannot load library libgssapi_krb5.so.2`.
- No celular (abaixo do `md`), todo alvo de toque tem 40 px:
  - o botão, o campo e a seleção, com `h-10 md:h-8` no `Button`, no `Input` e na `Selecao`, e os tamanhos menores do `Button`: o `sm` (`h-10 md:h-7`) e o `icon-sm` (`size-10 md:size-7`). Não passe uma altura própria a eles sem conferir as duas larguras;
  - o link solto, fora de frase e de tabela, como o "Leads" de voltar da ficha (`h-10 md:h-auto`) e o link do registro nos cartões da auditoria;
  - ficam abaixo de 40 px só os links dentro do texto e os controles com rótulo clicável, como os interruptores e os rádios (especificação, Passo 11).
- Os avisos do sonner saem embaixo, no centro, em todas as larguras (`Provedores.tsx`), por cima de tudo, até da gaveta do menu e dos diálogos:
  - o fim da área principal tem um espaço de 128 px (`h-32`, no `Layout.tsx`), a altura do maior aviso, o "Avisos do navegador" em 360×640, com a margem. Um aviso mais alto pede um espaço maior, e um botão fixo no rodapé ficaria por baixo dos avisos;
  - abaixo do `md`, o diálogo fica na área acima desse espaço e rola por dentro (as classes `max-md:` do `DialogContent`);
  - na visão de dispositivo do navegador, em desenvolvimento, o "Avisos do navegador" não aparece: o user agent de celular sem service worker conta como sem notificação (`avisosSuportados`, em `web/src/features/tempo-real/avisosNavegador.ts`). Para ver o aviso, use só a largura, sem trocar o user agent, ou o `npm run preview`, com o service worker.
- No Windows, parar o `npm run dev` pelo `TaskStop` deixa o `node` do Vite no ar, escutando na 5173. O mesmo acontece quando a sessão do Claude Code reinicia com ele em segundo plano (Lote 4B). Confira a porta (`Get-NetTCPConnection -LocalPort 5173 -State Listen`), confira que o processo é o `vite.js` do projeto e encerre-o.
- As telas do front são carregadas sob demanda (Lote 4B). Uma tela nova entra nas rotas pelo `tela()` (`web/src/rotas.tsx`), que dá o `lazy`, o `Carregando` e o `ErroDaTela`, e o `rotas.test.tsx` tem a lista das rotas que ficam no pacote principal (`/` e `*`):
  - o `lazy` fica na forma de função (`lazy: async () => ({ Component })`). Na forma de objeto (`lazy: { Component }`), numa rota sem loader, o React Router 8.4 engole a falha do import (o `callLoaderOrAction` só espera a promessa do loader), e a tela fica em branco, sem passar pelo `ErrorBoundary`;
  - o `ErroDaTela` relança o erro que não é de carga, e ele sobe ao `ErrorBoundary` padrão que o React Router dá à primeira rota. Por isso as rotas ficam dentro de uma rota raiz sem caminho e sem tela: sem ela, o erro relançado no login escaparia do React Router e derrubaria o app;
  - a recarga da versão nova sai do `ErroDaTela`, e não do `vite:preloadError`: quando uma tela não carrega numa navegação, o roteador já fica no endereço novo, com o erro, e a recarga abre a tela clicada. A decisão sai do `tela()`, uma vez por falha. Um `import()` fora das rotas não tem essa recarga;
  - um `Navigate` para uma tela sob demanda deixa a área vazia até o arquivo dela chegar, porque o roteador só troca de tela depois de baixá-lo. Os redirecionamentos do `RotasProtegidas.tsx` usam o `Redirecionar`, que mostra o `Carregando` junto;
  - nos testes do front, o `renderizarApp` monta o roteador com as telas já carregadas (`comAsTelasCarregadas`, num `await` no topo do `renderizar.tsx`), como quando o `rotas.tsx` as importava direto. Com o `lazy`, cada navegação esperava o import, mesmo em cache, e o teste das campanhas estourou o `findBy`. O caminho sob demanda é testado no `rotas.test.tsx`, com rotas do próprio teste;
  - o 404 de um arquivo que não existe em `/assets` sai pelo `UseStatusCodePages`, sem `Cache-Control`: o cache de 1 ano só é posto no `OnPrepareResponse` do `UseStaticFiles`, quando o arquivo é servido. Numa volta de versão, o navegador não guarda o 404.
- Para saber o que cada arquivo do build do front tem, faça o build num contêiner, como o do `front.sh`, com `--manifest --sourcemap`. O manifesto (`.vite/manifest.json`) dá o que o `index.html` e cada tela baixam (os `imports`), e o `source-map-js`, que já vem no `node_modules` do front (pelo `postcss`), dá os bytes de cada pacote, sem instalar nada (Lote 4B).
- Para revisar as telas no celular ou seguir um roteiro pela tela, use o Playwright num contêiner Linux, nada no Windows:
  - a imagem é a `mcr.microsoft.com/playwright`, com o pacote `playwright` da mesma versão instalado num volume do Docker;
  - o contêiner fica ligado à rede da API de um Compose de produção descartável (`--network container:<api>`, `http://localhost:8080`, onde o cookie `Secure` vale) ou à do Caddy (`https://localhost`, com `ignoreHTTPSErrors`), para a CSP;
  - a faixa da rede do Compose descartável não pode colidir com a do `crm-local` (`172.30.0.0/24`): use `REDE_SUBNET` e `CADDY_IP`;
  - a notificação só funciona com o Chromium completo (`chromium.launch({ channel: 'chromium' })`), porque o "headless shell" padrão nega a permissão;
  - depois de um clique num link `tel:` ou `mailto:`, o Chromium sem janela deixa a página sem receber os cliques seguintes. Nos roteiros, impeça a navegação do link (`addInitScript`), como o `naoNavegar` dos testes do front;
  - o arraste pelo teclado com as setas apertadas sem pausa anda menos colunas: use pausas ou o mouse;
  - uma sessão guardada (`storageState`) leva o `localStorage` de quando foi gravada, como o `crm:avisos-do-navegador` já respondido: para o aviso aparecer, apague a chave num `addInitScript`.
- No PowerShell 5.1, as aspas duplas dentro de um argumento para um programa nativo se perdem ou partem o argumento em vários. Por isso, use `git commit -F` com a mensagem num arquivo do scratchpad e, para o contêiner, um script em arquivo copiado com `docker compose cp`.
  - Um `git commit -m` com aspas na mensagem sai sem elas ou falha com "pathspec did not match".
  - Se o commit falhar depois do `git add`, os arquivos ficam no índice e entram no commit seguinte: confira com `git show --stat`.
- O gatilho Webhook do n8n guarda nos dados da execução tudo o que recebe, com os cabeçalhos e o corpo: o lead, o `X-Crm-Chave` do Alertas e o `google_key` do Google. Por isso nenhum fluxo salva execução, desde o Lote 2A. Mesmo assim, a que não fica salva passa pelo banco: ela fica nele, apagada (`deletedAt`) e fora da API e da tela, até a limpeza das apagadas, a cada 15 minutos (`EXECUTIONS_DATA_PRUNE_HARD_DELETE_INTERVAL`). A de produção sai na primeira, porque o `deletedAt` dela vem 1 hora antes; a manual, só depois de 1 hora (`EXECUTIONS_DATA_HARD_DELETE_BUFFER`). Numa varredura, conte as marcas também nas apagadas e, se houver, conte de novo depois da limpeza.
- Um HTTP Request do n8n com credencial nunca baixa a resposta como arquivo (formato "File"). Nesse modo, o n8n guarda no `contextData` da execução a requisição inteira, com o cabeçalho da credencial. Foi assim que o `X-Api-Key` vazou em 14/10/2026. Baixe como texto e refaça o binário no Code, como no relatório semanal.
- As configurações de execução de um fluxo valem na hora, sem publicar de novo.
- No n8n local, o filtro de data da Data Table compara texto: um "maior que" no mesmo dia não acha nada. Compare as datas num Code e apague pelo id, como no Resumo.
- O Retry On Fail do n8n repete qualquer erro, sem escolher pelo código: no envio ao CRM, ele repetia o 400. Deixe a falha temporária para a fila. O MCP não apaga um campo do nó: o `maxTries` e o `waitBetweenTries` ficaram no "Enviar ao CRM" dos três fluxos de entrada, sem efeito sem o `retryOnFail`.
- Abrir um fluxo no editor do n8n pode salvar sozinho uma versão normalizada, que faz o mesmo: sem os valores padrão dos parâmetros, com posições ajustadas e com o `binaryMode: separate`, que só muda algo no modo `combined`. O JSON exportado deixa de bater com o do repositório. Depois de abrir um fluxo no editor, compare a exportação; num fluxo sem publicar, o `n8n import:workflow` do JSON do repositório o volta.
- O "Only Run If" do Webhook é avaliado antes de o nó ler um corpo `multipart/form-data`: nele, o `$json.body` chega vazio, e a condição passa (`Webhook.node.js` e `body-parser.js`, na 2.43.5). Por isso o campo isca do site fica no "Only Run If", para JSON e formulário, e também no "É um robô?", para o multipart. O envio barrado pelo "Only Run If" recebe 200 com `{"message":"Webhook call received"}`, sem execução.
- No nó de Data Table, a saída de erro (`continueErrorOutput`) só recebe as falhas do tipo `NodeOperationError` ou `NodeApiError`, como a tabela que não existe. As outras, como uma coluna que não existe ou a validação de um valor, saem na saída normal, com o item de entrada e sem erro, como se a gravação tivesse dado certo (`DataTable/actions/router.js`, na 2.43.5). Depois de gravar, confira o `id` da linha devolvida, como o "Conferir o limite" e o "Gravou a recusa?" do site.
  - Para testar a falha, use uma coluna que não existe. O MCP recusa no nó o nome de uma tabela que não existe, e renomear a tabela não derruba o fluxo na hora: o n8n guarda o id dela em memória.
- Num nó com a saída de erro e o "Always Output Data", uma falha manda o erro para a saída de erro e um item vazio para a saída normal, e os dois caminhos rodam. No site, o envio iria duas vezes ao CRM. O "Conferir o limite" para o caminho normal quando a busca tem erro (`$('Buscar os envios recentes').all(1)`).
- Num Code, o `$('Nó')` sem o índice da saída lê a que liga aquele nó ao atual. Se ele chega ao atual pela saída normal e pela de erro, ou só pela de erro, o `.first()` e o `.all()` podem ler a saída vazia: passe o índice (`.first(0)`, `.all(1)`).
- No MCP do n8n:
  - o `create_workflow_from_code` não põe credencial nos HTTP Request: passe no nó o `{ id, name }` da credencial, que o `list_credentials` dá;
  - as notas ganham nomes aleatórios: renomeie para "Sobre o fluxo" com o `renameNode` do `update_workflow`;
  - as configurações do fluxo entram pelo `setWorkflowSettings` do `update_workflow`, e o fluxo de erro só aceita um fluxo publicado;
  - o `execute_workflow` não roda o gatilho de Lead Ads. Use o `test_workflow` com pin data, que fixa também os HTTP Request e os nós com credencial. Para levar o lead montado ao CRM, ponha-o na `crm_entradas_pendentes` (`add_data_table_rows`) e rode o Reenvio;
  - chamadas na mesma mensagem rodam juntas: insira a linha na Data Table e só depois, em outra mensagem, execute o fluxo que a lê;
  - quando o n8n reinicia, como na cópia do volume ou numa versão nova, o MCP cai e não volta sozinho, nem depois de o `/healthz` responder. O `/mcp` do Claude Code, que é com o usuário, o reconecta. Aconteceu no Lote 4A-1, na cópia do volume e a cada reinício do Docker Desktop.
- O `N8n__WebhookUrl` não aparece no `printenv` do contêiner da API, porque a própria API lê o `.env` (`ArquivoEnv`). Para saber se ela leu, procure no log da subida a linha "N8n__WebhookUrl não está configurada", que só aparece com a URL vazia.
- Um e-mail mandado de uma conta do Gmail para um alias dela mesma, como o `+adriana`, chega como enviado, sem notificar o celular. Por isso, o remetente dos alertas é outra conta.
- A auditoria do NuGet, com os avisos tratados como erro, derruba o build quando sai um alerta novo para um pacote. Foi o caso da CVE-2026-70159 do SignalR.Common, em 13/10/2026. Suba o pacote para a versão corrigida, no `Directory.Packages.props`.
- Nos testes do front, o MSW usa o tratador registrado por último. O `comQuadro()` e o `comCadastros()` registram as campanhas padrão: registre as do teste depois deles.
- A suíte do front roda com no máximo 2 workers (`maxWorkers: '25%'`, no `web/vite.config.ts`), o que dá 1 no runner de 2 vCPUs do CI.
  - Com o padrão do Vitest (todas as CPUs menos uma), os 7 workers disputavam os 4 núcleos, e a suíte falhava por tempo em toda rodada.
  - Por que 2 e não 4: com a máquina mais ocupada, 4 workers falharam em 2 das 10 rodadas de 20/10/2026, e as falhas eram de lentidão geral, não de um teste específico.
  - A medição só vale com a máquina sem carga pesada de fora. Em 20/10/2026, um vídeo no Chrome ocupou de 3 a 4 threads e derrubou rodadas, até no contêiner com 1 worker.
  - Um `findBy` que só falha com a suíte inteira não ganha um `timeout` maior: meça antes onde vai o tempo.
- Nos testes do front, uma consulta por papel (`*ByRole`) numa tela grande custa caro e, numa espera, se repete a cada mudança no DOM. Com a disputa, a consulta da opção da etapa na ficha ia de 11 para 91 ms, e o `getAllByRole('cell', { name })` numa tabela de 100 linhas respondia por 4,4 s de um teste de 5,1 s. Em tela grande, procure pelo rótulo (`getByLabelText`), pelo texto ou dentro de um pedaço da tela (`within`), como o `seletorDeEtapa()` da ficha.
- Numa espera dos testes do front, tudo roda numa thread só, e a medida de 05/11/2026, no contêiner com `--cpus=1`, mostrou:
  - cada tentativa que falha de um `getBy` ou de um `findBy` formatava a página inteira para montar o erro (o `getElementError` padrão da Testing Library chama o `prettyDOM`), e o `waitFor` descarta esse erro: só o erro final, se a espera estourar, mostra a página. A tentativa que falha custou de 10 a 23 ms de mediana, e até 207 ms, quase tudo no `prettyDOM`, contra 0,1 a 0,5 ms do `queryBy`. A espera confere a cada mudança no DOM, e esse custo atrasa a própria tela: depois do clique na notificação, com o funil ainda na tela, a ficha só foi pedida 760 ms depois da navegação;
  - desde o Lote 6, o `setup.ts` troca o `getElementError`. Nas tentativas de dentro do `waitFor`, que a Testing Library marca com o `_disableExpensiveErrorDiagnostics`, o erro sai só com a mensagem; fora delas, e no erro final do tempo esgotado, que passa de novo pelo `getElementError`, ele continua com a página. A marca é interna da biblioteca, e o `test/esperas.test.tsx` avisa se uma versão nova a mudar. A consulta por papel continua cara numa tela grande: numa espera de tela, use `await waitFor(() => expect(screen.queryBy…(…)).toBeInTheDocument())`, pelo texto, como em "a tela inicial do admin é o painel";
  - o 1 s do `findBy` estica: com a thread ocupada, o temporizador atrasa, e uma espera de 1,5 s passou sem estourar. Por isso a falha por tempo aparece de vez em quando, e não sempre;
  - o 1º teste de cada arquivo paga a subida fria. No painel, o render inicial levou até 0,9 s, contra 79 ms como 2º teste, e o redirecionamento da tela inicial, até 0,8 s. Uma espera que cobre a subida e a tela juntas fica sem folga: espere primeiro o caminho (`router.state`), numa espera que não consulta o DOM, e depois a tela, como em "a tela inicial do admin é o painel".
- Para encurtar nos testes do front um intervalo que espera o relógio de verdade, como o da consulta de andamento da importação, ponha a constante num módulo próprio e troque o valor por `vi.mock`, sem relógio falso (`importacao/andamento.ts`).
- Toda imagem do `Dockerfile` e dos Compose tem versão exata, e o `ImagensTests` cobra o formato de cada uma: uma imagem nova precisa entrar nele. O SDK do .NET aparece em quatro lugares e sobe junto nos quatro, como no PR mensal do Dependabot. Em 01/11/2026, com o #2 do mesmo grupo aberto, o PR de imagens (#4) subiu o SDK só no `Dockerfile`, e o `ImagensTests` ficou vermelho: o conserto está no README (Como conferir um PR do Dependabot).
- Um `docker compose -p <descartável> -f docker-compose.prod.yml up --build` troca a imagem `crm-api`, a mesma do plano B. Para não mexer nela, dê outro nome à imagem por um override (`image:` dos serviços `api` e `migracoes`).
- O Docker Desktop cria no firewall do Windows a regra de entrada "Docker Desktop Backend", em TCP e UDP, que libera o `com.docker.backend` no perfil Público, em qualquer porta e de qualquer origem. As duas redes desta máquina, o Wi-Fi e a Radmin VPN, estão nesse perfil. Uma porta publicada sem IP fica aberta para elas enquanto o contêiner está no ar: o `com.docker.backend` escuta no `::`, que aceita também o IPv4.
  - Toda porta do `docker-compose.yml` fica no `127.0.0.1`, e o `PublicacaoTests` cobra isso.
  - O Compose de produção no próprio computador sobe com o `PORTAS_IP=127.0.0.1`: o plano B, pelo `../crm-teste.env`, e todo Compose descartável. No servidor, ele fica vazio, e o Docker publica a 80 e a 443 no IPv4 e no IPv6; um `0.0.0.0` deixaria o IPv6 de fora.
  - Com o `127.0.0.1`, o `localhost` chega pelo IPv4: o `::1` recusa, e o navegador e o `curl` passam para o `127.0.0.1`. Pelo IP (`https://127.0.0.1:8443`), o TLS falha, porque o certificado do Caddy é do nome `localhost`.
  - O Testcontainers publica sem IP: o PostgreSQL dos testes, com a senha padrão `postgres`, o Ryuk, que apaga os contêineres dos rótulos que recebe pela porta dele, e o Caddy do `LogDoCaddyTests`. A regra própria, abaixo, fecha essas portas para a rede, e a suíte no contêiner continua passando com ela: o contêiner chega ao PostgreSQL pelo `host.docker.internal`, sem passar pelo firewall. Prender essas portas no `127.0.0.1` pelo código não serve: no CI, os testes chegam ao PostgreSQL pelo `host.docker.internal`, que no Linux é o gateway da rede.
  - Desde 26/10/2026, a regra própria "Docker Desktop: sem acesso pela rede" (entrada, Bloquear, perfil Any, para o `com.docker.backend.exe`) fecha para a rede todas as portas que o Docker Desktop publica, inclusive as do Testcontainers e a do n8n do WSL. O bloqueio vence a permissão da regra do Docker. Confira-a depois de cada atualização do Docker Desktop (`Get-NetFirewallRule -DisplayName 'Docker Desktop: sem acesso pela rede'`, README › Primeira vez). O `PORTAS_IP` e o `127.0.0.1` do Compose ficam como segunda camada.
  - Só outro aparelho prova a regra: o Windows não filtra o tráfego para ele mesmo, e um pedido do próprio computador pelo IP do Wi-Fi passa. No Docker Desktop, o log do Caddy mostra o gateway da rede como o IP de todo pedido: quem separa o do celular é o endereço, o caminho e o navegador. O Caddyfile só grava os erros do proxy, e o redirecionamento da porta HTTP não entra no log: para o pedido do celular aparecer, abra o HTTPS direto, num descartável com o IP no `DOMINIO`, com a API parada (o 502).
- Os workflows do CI só leem o repositório, e o `WorkflowsTests` cobra isso em todo arquivo de `.github/workflows`:
  - a permissão `contents: read`;
  - toda ação de terceiros fixada pelo SHA, com a versão num comentário, que o Dependabot atualiza;
  - todo checkout com `persist-credentials: false`;
  - nada de `secrets`, `github.token` ou `pull_request_target`.
- No GitHub Free, repositório privado não tem proteção de branch: o check vermelho não impede o merge. A conferência de um PR é manual (README › Versões fixadas).
- O `front.sh` monta o `web/` só para leitura, com o `node_modules`, o `dist` e o `coverage` em volumes anônimos, e o build sai em `/tmp/dist`, dentro do contêiner. O Docker não cria um ponto de montagem dentro de uma montagem só de leitura: por isso o script cria as três pastas antes, o que no runner são pastas vazias.
  - O `dist` e o `coverage` ficam escondidos desde 21/10/2026 (`436a72b`). Só com o `web/` montado, sem o `.git` e o `.gitignore` da raiz, o Tailwind procura classes também no `web/dist`. Com o build velho do Windows na pasta, o CSS do build no contêiner saía com 3,5 KB a mais; com os volumes, o build no contêiner é igual ao do Windows, arquivo por arquivo.
  - A imagem de produção (o `.dockerignore` tira o `web/dist` e o `web/coverage`) e o CI (um checkout novo) não têm isso. Um contêiner novo que monte só o `web/` precisa do mesmo cuidado, também numa cópia por `git archive`, como a do `e664e8f` na medida do Lote 4B.
- Um pacote do npm muda pelo roteiro do README (Versões fixadas › Um pacote do npm com alerta), e não por um `npm.cmd install <pacote>` direto no Windows, que baixaria um binário nativo novo antes de qualquer conferência. O Controle Inteligente de Aplicativos bloqueia um binário sem assinatura.
  - O lockfile é escrito num contêiner, pelo npm da mesma versão do Windows (o 11.17.0, em 21/10/2026), com o `--package-lock-only`. O resultado é o mesmo, byte a byte, mas no Windows o `--package-lock-only` também regrava o `node_modules/.package-lock.json`.
  - O roteiro termina com um `install` sem pacote: o `uninstall --package-lock-only` deixa marcas `devOptional` velhas, e o `install` sem pacote as recalcula.
  - O `install` de um pacote resolve de novo as dependências dele e pode trazer a versão nova de uma que não precisava mudar, mesmo com o `--prefer-dedupe`. Segure a versão do lockfile: instale-a junto e tire-a do `package.json` logo em seguida, como o `magic-string` no Lote 5B (README).
  - O `npm.cmd install --ignore-scripts` só vem depois de o `node .github/scripts/nativos-do-lock.mjs` dizer IGUAIS.
  - O npm 11 avisa que o `msw` tem um script de instalação fora do `allowScripts`. Nada roda: no contêiner, o `--package-lock-only` não instala nada, e, no Windows, o `--ignore-scripts` não roda nenhum script.
  - O lockfile tem as 6 `bundleDependencies` do `@tailwindcss/oxide-wasm32-wasi` (opcionais, `inBundle`, sem `os` nem `cpu`). O npm 11.17.0 as escreve com o `--package-lock-only`, e o `install` completo as mantém; o lockfile de antes, de 06/10/2026, não as tinha. Um lockfile que muda sem nenhum pacote mexido pede a conferência da versão do npm que o escreveu.
- O CSS do shadcn fica em `web/src/shadcn.css`, copiado do `dist/tailwind.css` do pacote `shadcn` 4.21.0, com o aviso de licença MIT no cabeçalho. O pacote, que é a CLI, saiu do `package.json` em 21/10/2026 (`4d64e35`).
  - Não formate o arquivo: ele fica no `.prettierignore`, igual ao original, para comparar com uma versão nova. Numa versão nova, troque tudo o que vem depois do comentário pelo `dist/tailwind.css` dela.
  - Um componente novo vem pela CLI, num contêiner (README › Versões fixadas › Um componente novo do shadcn). Se ela trouxer de volta o pacote ou o `@import 'shadcn/tailwind.css'`, compare o CSS com o `web/src/shadcn.css` antes do commit.
  - O `cn` não é a CLI: é o pacote da organização do shadcn que substitui o `clsx` e o `tailwind-merge`, e os componentes o importam. Ele fica.
- No modo do Microsoft.Testing.Platform, o `dotnet test` cria a pasta dos resultados (`TestResults`) na pasta de trabalho. No contêiner dos testes, ela é o `/src`, só para leitura, e o `docker-compose.testes.yml` manda os resultados para o volume (`--results-directory /artefatos/TestResults`). Sem isso, num checkout limpo, como o do CI, a suíte falha antes de rodar, com o código 134.
  - Aqui, a pasta do projeto tinha uma `TestResults` vazia desde 28/09/2026, que o `.gitignore` escondia, e o erro não aparecia. Ela foi apagada em 20/10/2026. Se voltar, como depois de um `dotnet test` no Windows, o erro volta a ficar escondido aqui.
  - Uma mudança no contêiner dos testes ou no CI se confere num clone limpo, numa pasta temporária, com um projeto do Compose descartável (`-p`), que tem volumes novos, como o runner. No fim, o `down -v` desse projeto e o clone apagado.
- Uma chave vazia tem o mesmo SHA-256 de um cabeçalho vazio. Uma chave que pode estar vazia nunca entra numa comparação:
  - no CRM, sem a `Integracao__ChaveAnterior`, a segunda comparação repete a atual e não conta (`ChaveIntegracaoHandler`);
  - no `CRM - Entrada Google`, a segunda condição compara com o `googleKeySha256Anterior || googleKeySha256`;
  - a anterior chega ausente em desenvolvimento, porque o `ArquivoEnv` ignora o valor vazio, e vazia no Compose de produção. Os testes cobrem os dois casos.
- No Git Bash, o `sed -i` abre o arquivo em modo texto e tira o CR de todas as linhas, mesmo das que ele não muda: use o `sed -b`, que no Linux é aceito sem efeito. O `grep` do Git Bash também tira o CR ao ler um arquivo de texto: para achar um CR, use o `grep -U`.
- O `clip < /dev/null` não limpa a área de transferência: com a entrada vazia, ele só mostra a ajuda. O `Set-Clipboard -Value $null` limpa. Como ele não limpou uma vez, em 13 seguidas no teste dos blocos, o comando do README diz se a área de transferência ficou vazia, sem mostrar o que tinha.
- Uma conferência depois de um `sed` compara com o valor que ele devia gravar, e não só com o formato: no bloco dos eventos, com o `sed` sem trocar nada e a chave de hoje em 48 caracteres hex, a conferência do formato dizia que deu certo. O valor chega ao `awk` pelo ambiente (`ENVIRON`), e não pela linha de comando, que aparece no `ps`.
- O JSON que o CLI do n8n exporta vem minificado, e o Prettier mantém aberto todo objeto que chega aberto. Ao deixar só `id`, `name`, `nodes`, `connections` e `settings`, grave o JSON minificado (`JSON.stringify(objeto)`) e só então formate: com o `JSON.stringify(objeto, null, 2)`, o diff do fluxo do Google teve 262 linhas, e não 17.
- O PowerShell 5.1 lê um `.ps1` sem BOM como Windows-1252, e um caractere de fora do ASCII pode virar aspas curvas, que ele aceita como aspas. Os scripts `.ps1` ficam só com ASCII.
- Um arquivo que o `docker compose cp` põe no contêiner do n8n fica do root, e o `docker compose exec` roda como `node`. Para apagá-lo, use o `docker compose exec -u root n8n rm -f <arquivo>`.
- O `docker compose restart` do Compose 5 pode terminar mostrando só o "Restarting", sem o "Started". Confira com o `docker compose ps` e o `/health`.

## n8n

- Os fluxos se criam e se conferem no n8n local, pelo servidor MCP `n8n-local`. Ele está no escopo `user` (`~/.claude.json`, fora do Git) e aponta para `http://localhost:5679/mcp-server/http`.
- O `n8n` do `.mcp.json` é o do n8n Cloud de teste, que acabou, e aparece como Failed: não mexa nele até o n8n Cloud de produção existir.
- Antes de escrever um fluxo pelo MCP, leia a referência do SDK (`get_workflow_sdk_reference`) e confira os parâmetros dos nós (`get_node_types`).
- Nunca publique, ative nem execute um fluxo sem confirmação do usuário: publicado, o `CRM - Alertas` manda um e-mail de verdade a cada evento do CRM.
- Regras de todo fluxo do CRM (o `FluxosN8nTests` confere os JSONs):
  - o nome começa por `CRM - `;
  - um nó Set "Configuração", logo depois do gatilho, guarda os endereços e os e-mails. Segredos ficam só nas credenciais `CRM - …`;
  - nada de `$env`, `$vars`, Execute Command, arquivos em disco ou nó da comunidade: o n8n Cloud não tem;
  - nas configurações, nenhuma execução salva (`saveDataSuccessExecution` e `saveDataErrorExecution` em `none`, `saveManualExecutions` e `saveExecutionProgress` em `false`), o fuso America/Sao_Paulo e o `CRM - Erros` como fluxo de erro (menos nele mesmo);
  - nenhum Stop and Error, e o envio ao CRM sem o Retry On Fail. As falhas dele vão pela `destinoDaFalha`, com o mesmo texto nos quatro fluxos que enviam ao CRM;
  - a Data Table vai pelo nome (modo `name`), e não pelo id.
- Depois de criar ou alterar um fluxo, exporte-o pelo CLI e salve o JSON em `n8n/<nome-do-fluxo>.json`. Deixe só `id`, `name`, `nodes`, `connections` e `settings` e formate com o Prettier do `web/` (README › n8n local).
- Depois dos testes, apague as execuções de teste. Não leia pelo MCP o conteúdo de uma execução que possa ter uma chave, porque isso traria a chave para a conversa. Para saber se uma execução guarda um cabeçalho ou campo, rode no contêiner um script que leia o SQLite do n8n e só diga se a marca aparece. O módulo `sqlite3` está dentro do próprio n8n, em `/usr/local/lib/node_modules/n8n/node_modules/.pnpm/sqlite3@*/node_modules/sqlite3`, e o banco fica em `/home/node/.n8n/database.sqlite`.

## Como trabalhar

- Siga os passos da seção 10 da especificação nesta ordem, que vale desde 05/10/2026:
  1. Passos 7 e 8, tudo local. A demonstração para o chefe vem logo depois do 8.
  2. Depois, os Passos 9 e 10, enquanto o servidor não chega.
  3. Com o servidor: a publicação que falta do Passo 5, o Passo 6 e o Passo 11.
- Em 13/10/2026, o Passo 11 foi adiantado e feito. Fica para a terceira fase só o que depende do servidor, como o celular de verdade. A divisão do pacote por tela foi feita no Lote 4B, em 09/11/2026. As etapas definitivas esperam a Gama.
- Em 14/10/2026, o Passo 6 foi adiantado no n8n local, com a tela de campanhas. Fica para a terceira fase o que depende do n8n Cloud, do app da Meta, do Google Ads e da Gama.
- Em 16/10/2026, com a reunião sem data, o trabalho passou a seguir um plano de lotes, aprovado em 19/10/2026, com tudo o que não depende da empresa:
  - 1: infraestrutura (as imagens com versão fixa e o Dependabot), fechado em 19/10/2026;
  - a causa do teste instável do front, trazida do 4A em 19/10/2026 e fechada em 20/10/2026;
  - 1B: CI no GitHub Actions, fechado em 21/10/2026;
  - 5: dependências, antecipado para antes do 2A por causa de 3 alertas do vite e feito em 21/10/2026;
  - 2A: n8n (avisos agrupados e execuções sem chave nem dado pessoal), feito em 26/10/2026;
  - 5B: os alertas do npm de 26/10/2026 (o `zod`, o `undici` e o `@vitest/mocker`), logo depois do push do 2A, feito em 26/10/2026;
  - 1C: as portas só no próprio computador, decidido em 21/10/2026 para logo depois do 2A e antes do 2B, porque o 4A e o 4B sobem Compose descartáveis com o Caddy, feito em 26/10/2026;
  - 2B: n8n (o limite do site), feito em 27/10/2026;
  - 3: troca de chaves sem parar, feito em 03/11/2026;
  - 4A-1: os PRs do Dependabot (o #4 e o #5, com o .NET 10.0.14 e o Node 22.23.4, e o #2, com a Caddy 2.11.7 e o n8n 2.43.7), feito em 05/11/2026;
  - 4A-2: front (os alvos de toque no celular, os avisos embaixo, o Passo 7 pela tela e os testes do painel e do service worker), feito em 09/11/2026;
  - 4B: front (as rotas sob demanda), feito em 09/11/2026;
  - 6: documentação.
  - Cada lote só começa com o meu ok e fecha com a demonstração conferida.
  - Desde 03/11/2026, qualquer envio (um webhook, um `site.json` a mais), execução de fluxo ou publicação fora da lista aprovada do lote só com o meu ok antes, mesmo que seja só para conferir. Para conferir sem enviar nada, use o export pelo CLI, o `/healthz`, o `docker compose ps` e as contagens.
- Um passo só termina quando os critérios de aceite passam.
- O usuário inicia cada passo com `/passo N`. O procedimento completo está em `.claude/skills/passo/SKILL.md`.
- A leitura do `.env` é bloqueada em `.claude/settings.json`. Se precisar de um valor de configuração, use o `.env.example` ou pergunte ao usuário.
- Antes de codar um passo, mostre o plano (arquivos, migrations, endpoints, telas) e espere aprovação.
- Rode `dotnet test` e `npm test` antes de dizer que terminou. Faça commits pequenos, um assunto por commit.
- A suíte do .NET (pelo contêiner) e a do front rodam uma depois da outra, nunca ao mesmo tempo, nem uma em segundo plano enquanto a outra roda.
  - Juntas, elas deixam a máquina lenta, e os testes do front estouram o tempo de espera de 1 segundo do `findBy` e do `waitFor`, falhando até em telas que não mudaram.
  - Em 08/10/2026, rodando junto com a do .NET, a suíte do front teve 5 falhas, com arquivos levando até 34 s; sozinha, passou inteira.
  - Antes de qualquer uma das duas, confira a carga da CPU. Acima do normal, avise antes de rodar, em vez de rodar e repetir (desde 03/11/2026).
- Se algo mudar uma regra, atualize `docs/especificacao.md` no mesmo commit.
- As decisões em aberto da seção 11 têm um padrão definido. Implemente o padrão e deixe configurável quando a especificação pedir.
