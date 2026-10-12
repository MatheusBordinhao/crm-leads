# CRM de Leads

CRM interno que recebe os leads das campanhas da Gama Marketing (Facebook, Instagram e Google), distribui entre as atendentes e mede quantos viram clientes em cada campanha.

A fonte da verdade é [docs/especificacao.md](docs/especificacao.md). Ela traz as regras de negócio, o contrato com o n8n, o modelo de dados, as telas e os passos de entrega com critérios de aceite. Leia a seção do passo atual antes de implementar.

## Situação atual

- Admins: Matheus e o chefe. Atendentes: Adriana e Pietra.
- Em 05/10/2026, a ordem do projeto mudou: a empresa só libera o servidor e o domínio depois de ver o sistema funcionando numa demonstração para o chefe, que vem logo depois do Passo 8. A ordem nova está em "Como trabalhar" e na seção 10 da especificação.
- Ainda não há servidor, domínio, n8n de produção, app da Meta liberado nem acesso ao Google Ads. Quando um passo depender disso, deixe tudo pronto, teste com dados simulados e liste o que falta.
- Os usuários de teste usam aliases com "+" de matheusbordinhao11@gmail.com, como matheusbordinhao11+adriana@gmail.com.
- Chaves e senhas nunca vêm pelo chat: diga o comando e eu rodo no terminal.
- Passos 0 a 3 validados manualmente por mim em 30/09/2026, além dos testes automáticos:
  - login do admin, troca de senha obrigatória e cadastro da Adriana e da Pietra;
  - login e troca de senha da Pietra;
  - cadastro manual de leads, aviso "Sem CPF/CNPJ" e botões de contato conforme os dados do lead;
  - funil: mudança de etapa, motivo obrigatório em Perdido e recusa de Ganho sem CPF/CNPJ, com o botão "Abrir a ficha";
  - lead cadastrado à mão fica "Ainda sem contato" até a primeira interação;
  - visibilidade: logada como Pietra, os leads da Adriana dão "Lead não encontrado", e o funil dela fica vazio, sem o filtro de responsável e sem o menu Configurações.
- O banco de desenvolvimento já tem o admin, a Adriana e a Pietra (aliases +adriana e +pietra), e o `.env` tem `Admin__Email`, `Admin__SenhaInicial` e `Integracao__Chave`.
- O teste do n8n Cloud de desenvolvimento acaba em 06/10/2026. Nele, os fluxos `CRM - Alertas` (`jG8npUo12JBbqAKS`) e `CRM - Erros` (`zqvER5UUTkTxFM4G`) foram criados e publicados em 30/09/2026, com o `CRM - Erros` como fluxo de erro do Alertas. A credencial Header Auth do Webhook se chama "Header Auth account", e os e-mails de teste saíam pela credencial "Gmail account" para matheusbordinhao11@gmail.com. Os JSONs dos dois fluxos estão em `n8n/`.
- O n8n de produção será decidido junto com o servidor. Até lá, o `N8n__WebhookUrl` do `.env` fica vazio (os eventos ficam pendentes em `eventos_saida`, e nenhum e-mail sai), o servidor MCP do n8n fica desconectado, e o que depender do n8n fica pendente:
  - levar o `CRM - Alertas` e o `CRM - Erros` para o n8n de produção pelos JSONs de `n8n/`, recriar as credenciais, preencher o `N8n__WebhookUrl` e repetir lá o e-mail com o link da ficha (Passo 4);
  - o fluxo `CRM - Relatório semanal Gama` (Passo 9);
  - a revisão das configurações de execução dos fluxos (Passo 10);
  - apontar o `.mcp.json` para o n8n de produção.
- Passo 4 validado de ponta a ponta em 30/09/2026, com `N8n__WebhookUrl` e `N8n__WebhookChave` no `.env` de desenvolvimento: um lead real pela integração foi para a Adriana pelo rodízio, o `lead.criado` foi entregue ao n8n na primeira tentativa, em 7 segundos, e o e-mail chegou com os dados do lead e o link da ficha, que abre o lead.
- Passo 5 implementado em 01/10/2026 e conferido pelo Claude no Compose de produção rodando localmente (`DOMINIO=localhost`, portas 8088 e 8443): HTTPS com HSTS e CSP, sessão mantida depois de recriar o contêiner da API, IP de quem acessa na auditoria (no Docker Desktop, o gateway `172.30.0.1`) e um `X-Forwarded-For` forjado ignorado, importação dos exemplos de `docs/exemplos/` com os acentos certos, reimportação sem duplicar, backup e restauração. Em 02/10/2026, com o Zod sem `new Function` e o favicon, o console do Chrome ficou sem erros e sem issues da CSP com a sessão aberta. Sem sessão, o único erro é o 401 do `GET /api/auth/eu` na tela de login, que é esperado.
- Passo 5 validado manualmente por mim em 02/10/2026:
  - em desenvolvimento, pela tela: `leads-meta.csv` e `leads-excel.csv` com os acentos certos, o telefone da Meta sem o `p:` e a campanha da Meta; reimportar o Excel deu 3 "já importados"; um CSV com erros deu 2 leads e as linhas 3 e 4 com os motivos, e o lead de telefone "123" entrou;
  - pelo rodízio, os leads foram para a Adriana, que recebeu "Você recebeu N leads da importação" e não tem o menu Importação; logada como Pietra, nada da Adriana aparece;
  - no Compose de produção local: migrations com `Exited (0)`, importação com o aviso de leads sem responsável, sessão mantida depois de recriar o contêiner da API, logins na auditoria com o IP `172.30.0.1`, e backup e teste da restauração com as contagens iguais.
- Opcionais do Passo 5 conferidos pelo Claude em 03/10/2026, pela tela, num Chrome sem janela contra o Compose de produção local:
  - os cabeçalhos HSTS, CSP, `nosniff`, `X-Frame-Options`, Referrer-Policy e Permissions-Policy, sem o `Server`;
  - um `.xlsx` de verdade, gerado pelo Excel, recusado com a instrução de salvar como CSV;
  - o `leads-excel.csv` importado com uma campanha nova e para uma atendente escolhida: os 3 leads ficaram com essa campanha e com ela, sem passar pelo rodízio;
  - o relatório de erros aberto no Excel como um clique duplo: "Linha" e "Motivo" em colunas, com os acentos certos.
- Só um CSV exportado de verdade pela Meta não foi testado: depende de uma exportação real da Gama ou da Meta.
- No banco de desenvolvimento, a Adriana e a Pietra estão no rodízio desde 06/10/2026, como precisam estar na demonstração: os leads da integração, os importados pelo rodízio e os cadastrados com "Rodízio" se alternam entre as duas. Desde 08/10/2026, o banco tem a massa da demonstração (`preparar-demonstracao`), e os leads de validação, como "teste" a "teste4", saíram. Em 12/10/2026, na validação do Passo 10, o comando rodou de novo, pelo contêiner. Para voltar aos 60 leads antes da demonstração, é só rodar o comando de novo.
- Falta a publicação do Passo 5, que depende do servidor, do domínio e do destino da cópia do backup. No servidor, repetir o HTTPS com certificado de verdade, a sessão depois de reiniciar a API, o IP real na auditoria e o teste da restauração. Antes de publicar, também:
  - tirar do log do Caddy a query string e os cabeçalhos com dados ou chaves. O Caddyfile não liga o log de acesso, mas o log de erro (`http.log.error`) fica sempre ligado e grava cada falha do proxy, como um 502 com a API reiniciando. Ele leva a URL com a query string e todos os cabeçalhos, menos o `Cookie` e o `Authorization`, que saem como `REDACTED`. Num Caddy 2.11.4 descartável, em 12/10/2026, saíram a busca (`?busca=...@example.com`) e o `X-Api-Key` da integração, em texto puro. O `Referer` das chamadas da tela de leads também leva a busca, que fica no endereço (`/leads?busca=`). O caminho é o filtro de campos do log do Caddy (`format filter`) no `request>uri`, no `request>headers>Referer` e no `request>headers>X-Api-Key`, conferido de novo com a API parada;
  - na auditoria, conferir o IP de um cliente IPv4 e o de um IPv6, como um celular no 4G. Se o servidor tiver IPv6 e a rede do Compose não, o Docker repassa essas conexões pelo `docker-proxy`, e a auditoria mostra o gateway da rede (`172.30.0.1`). Com um proxy ou CDN na frente do Caddy, ele precisa do `trusted_proxies`.
- Desde 02/10/2026, o Controle Inteligente de Aplicativos do Windows desta máquina bloqueia o `Testcontainers.dll`, que não é assinado (evento 3077 do Code Integrity), e, em 06/10/2026, bloqueou também o `Crm.UnitTests.dll` recém-compilado: o `dotnet test` do Windows falha com "Uma política de Controle de Aplicativo bloqueou este arquivo". Os testes .NET rodam pelo contêiner (`docker-compose.testes.yml`): 654 de 654 em 09/10/2026, com o Passo 10, e os do front, 197 de 197.
- Em 12/10/2026, o bloqueio chegou ao `EFCore.NamingConventions.dll`, e o `dotnet run` da API passou a falhar ao subir (`FileLoadException`, 0x800711C7). Desde então, no Windows, tudo o que carrega as DLLs do projeto roda em contêiner: a API de desenvolvimento e o `dotnet ef` pelo `docker-compose.yml` (serviços `api` e `ef`, README › Dia a dia), e os testes pelo `docker-compose.testes.yml`. O `dotnet build`, o `dotnet format` e o front continuam no Windows, porque só leem as DLLs. A API de desenvolvimento roda no contêiner, contra o banco de desenvolvimento. Conferido pelo Claude em 12/10/2026:
  - pelo proxy do Vite, numa API descartável do serviço `api`: login, troca de senha, o negotiate do hub, o WebSocket com o handshake 101 e o aviso `LeadsAlterados` de um lead novo;
  - a sessão continuou valendo depois de recriar o contêiner, com as chaves num volume;
  - o `preparar-demonstracao` pelo serviço, num banco descartável: recusou sem o `Demo__Permitir` e gravou os 60 leads com ele;
  - o `--N8n:WebhookUrl=` depois do nome do serviço desligou o envio ao n8n;
  - o `dotnet ef`: `migrations list`, `has-pending-model-changes` e uma migration de teste criada e removida, sem mudar nada fora da pasta das migrations;
  - o `npm run api:tipos` gerou o mesmo `schema.d.ts`.
- Passo 7 implementado em 06/10/2026 e conferido pelo Claude:
  - testes automáticos dos dois critérios de aceite: o lead da integração chega à responsável do rodízio em menos de 5 s (`TempoRealRodizioTests`), e a atendente não recebe o id de lead de outra pessoa em nenhum caso (`TempoRealTests`, com um lead da outra atendente como sentinela);
  - no Chrome sem janela, contra o Compose de produção local: o cartão apareceu no funil da atendente 1,6 s depois do envio pela integração com a API ainda fria e 0,35 s com ela quente, pelo WebSocket `wss://localhost:8444/hubs/notificacoes` (handshake 101); a outra atendente não recebeu o id do lead; o console ficou sem erros e sem issues da CSP;
  - o corpo da requisição "Lead real pela integração" do `Crm.Api.http`, que respondia 400 sem o `recebidoEm`, respondeu 201.
- Passo 7 validado manualmente por mim em 06/10/2026, das 11:36 às 11:43, em desenvolvimento (API com `dotnet run` e front em http://localhost:5173), com três sessões ao mesmo tempo: o admin no Edge, a Adriana no Chrome e a Pietra numa janela anônima do Chrome. O admin criou os leads em Leads › Novo lead, com Responsável "Rodízio". No começo, a distribuição estava ligada, e só a Adriana estava no rodízio:
  - lead novo em tempo real: o "teste" (lead 14) foi para a Adriana e apareceu no funil dela na hora, sem recarregar a página, e o sino dela contou; o funil e o sino da Pietra continuaram vazios;
  - notificação nativa: com os avisos do navegador ativados e a janela da Adriana minimizada, o "teste2" (lead 15) gerou a notificação do Windows "Administrador cadastrou um lead para você", sem o nome do lead, e o clique abriu a ficha;
  - troca de responsável: o admin passou o "teste2" da Adriana para a Pietra. Na Adriana, ele sumiu da lista, a ficha aberta parou de mostrar os dados, e o sino voltou para 5, sem a notificação dele; na Pietra, ele apareceu na lista, e o sino contou 1;
  - rodízio com as duas: com o rodízio da Pietra ligado, o "teste3" foi para a Pietra e o "teste4" para a Adriana; no fim, a Pietra via só os 2 leads dela, e a Adriana não via nenhum dos dois.
- Do Passo 7, o quadro parado durante o arraste e a ficha que guarda o que foi digitado não foram conferidos pela tela: ficam cobertos só pelos testes automáticos. A notificação nativa só foi conferida na Adriana, porque o Chrome bloqueia as notificações do navegador na janela anônima.
- Passo 8 implementado em 06/10/2026 e conferido pelo Claude. Por decisão minha:
  - as barras do painel são em HTML e CSS, e o Recharts saiu do Stack;
  - o painel mostra também os ganhos e a conversão por origem e por campanha;
  - o painel se atualiza pelo tempo real;
  - o painel é a tela inicial só do admin, e a atendente continua entrando no funil.
- Conferências do Passo 8:
  - os dois critérios de aceite têm testes automáticos:
    - a massa conhecida (`MassaDoPainel`, num banco só dela) confere cada número do painel do admin e da atendente, com os filtros e as viradas de dia em Brasília (`PainelMassaConhecidaTests`);
    - os leads de teste ficam de fora, pela integração de verdade (`PainelTests`);
  - no Chrome sem janela, contra um Compose de produção descartável (`-p crm-painel`, portas 8090 e 8453), com os dados montados pela API:
    - o admin entrou no painel, e a atendente, no funil;
    - em 1280 e em 360 px, não houve rolagem lateral;
    - no `/painel`, o console ficou sem erros e sem issues;
    - um lead pela integração somou na hora nos leads novos (de 13 para 14), com uma recarga só do painel.
- O comando `preparar-demonstracao` ficou pronto em 07/10/2026 (README › Demonstração).
  - Ele foi conferido pelo Claude num Compose de produção descartável, com os dados criados antes pela API:
    - sem `Demo__Permitir`, recusou com o código 1;
    - com a variável, trocou os leads pela massa;
    - duas rodadas deram os mesmos números no painel: 60 leads, 65 entradas, 11 ganhos, 8 perdidos e mediana de 25 minutos, com a Adriana como próxima do rodízio.
  - Em 08/10/2026, eu o rodei no banco de desenvolvimento, antes de validar o Passo 8.
  - Os números da Adriana e da Pietra ficam equilibrados (31 e 29 leads, 6 e 5 ganhos), para a demonstração não sugerir que uma atende melhor que a outra.
- Passo 8 validado manualmente por mim em 08/10/2026, em desenvolvimento, com a massa da demonstração:
  - o admin cai no painel, com 60 leads novos e 18,3% de conversão;
  - com o filtro de responsável na Adriana, o painel mostra os 31 leads e os 19,4% de conversão dela;
  - cada campanha mostra os ganhos e a conversão, e as somas da Adriana e da Pietra fecham com o total em todos os blocos;
  - a tabela por atendente mostra a Adriana com 31 leads e a Pietra com 29;
  - a Adriana cai no funil, e o Painel dela mostra só os números dela, sem o filtro de responsável e sem a tabela por atendente;
  - com o painel do admin aberto, um lead criado pela Adriana subiu os leads novos de 60 para 61, sem recarregar a página.
- Passo 9 implementado em 08/10/2026 e conferido pelo Claude em 09/10/2026. Por decisão minha:
  - os leads sem campanha entram no relatório numa linha por origem, sempre no fim;
  - a integração entrega também o CSV pronto (`/api/integracoes/relatorios/campanhas/csv`), que o fluxo semanal vai anexar ao e-mail da Gama;
  - sem `de` e `ate`, as rotas da integração devolvem a semana anterior inteira, de segunda a domingo no horário de Brasília, e o fluxo não calcula data. A tela continua com os últimos 30 dias.
- Conferências do Passo 9:
  - os dois critérios de aceite têm testes automáticos:
    - cada linha do CSV bate com o painel filtrado por ela, e o total, com o painel sem filtros, na massa conhecida (`RelatorioMassaConhecidaTests`);
    - nenhum dado pessoal no JSON e no CSV das quatro rotas (`RelatorioTests`) nem no log (`LogsTests`);
  - a semana anterior é testada com o relógio parado numa segunda às 8h e numa quarta (`RelatorioSemanaAnteriorTests`);
  - num Compose de produção descartável (`-p crm-relatorio`, portas 8091 e 8455), com a massa da demonstração:
    - pela API, as 5 campanhas e o total bateram com o painel, e a integração sem datas devolveu a semana de 28/09 a 04/10;
    - no Chrome sem janela, o `/relatorio` ficou sem rolagem lateral em 1280 e em 360 px, sem erros no console e sem issues, e o Baixar CSV trouxe os mesmos bytes da API;
    - no Excel, aberto como no clique duplo, as colunas e os acentos ficaram certos, e a taxa e a mediana viraram números;
    - uma campanha "-20% Black Friday" apareceu no Excel com o apóstrofo na frente.
  - O Excel desta máquina confirmou que o apóstrofo é necessário: sem ele, "+Leads outubro" vira `#NOME?`, e "=1+1", "-2+3" e "@SOMA(1;2)" viram contas.
- Passo 9 validado manualmente por mim em 09/10/2026, em desenvolvimento, com a massa da demonstração:
  - o Relatório só aparece no menu do admin;
  - nos últimos 30 dias, o total do relatório (59 leads novos, 64 entradas, 42 em andamento, 10 ganhos, 7 perdidos e 16,9% de conversão) bate com o painel;
  - as linhas sem campanha ficam no fim;
  - o CSV abriu no Excel com um clique duplo, com os acentos certos e a taxa como número;
  - a Adriana não tem o item no menu, e `/relatorio` a leva ao funil;
  - pela integração, sem datas e com a chave, veio a semana de 28/09 a 04/10; sem a chave, a resposta foi 401.
- Do Passo 9, só o fluxo `CRM - Relatório semanal Gama` fica pendente, para o n8n de produção.
- Passo 10 implementado em 09/10/2026 e conferido pelo Claude. Por decisão minha:
  - a anonimização guarda só um hash do id externo (`anon:` e o SHA-256), e não o id da Meta ou do Google. O reenvio e a reimportação continuam respondendo "já recebido";
  - a anonimização apaga também o detalhe do motivo de perda e o gclid, e os eventos do lead em `eventos_saida`;
  - as notificações ficam como estão, porque o título nunca teve dados do lead;
  - com uma importação processando, anonimizar responde 409, porque o arquivo dela ainda tem os dados das pessoas;
  - no CSV de leads, o telefone do Brasil sai no formato nacional, e o CPF/CNPJ com a máscara.
- Conferências do Passo 10:
  - os dois critérios de aceite têm testes automáticos, em bancos próprios:
    - uma varredura de todas as colunas de texto, JSON, lista e bytes de todas as tabelas acha os dados do lead antes da anonimização e nada depois, nem o id da Meta (`AnonimizacaoTests`, com a `VarreduraDoBanco`);
    - o painel (sem filtros, por atendente, por campanha e sem responsável), o relatório em JSON e em CSV e os totais do funil saem iguais antes e depois de anonimizar todos os leads;
  - num Compose de produção descartável (`-p crm-lgpd`, portas 8092 e 8457), no Chrome sem janela:
    - login pela tela, a ficha com o cartão Privacidade, a anonimização e a exclusão pela tela, a lista com o Exportar CSV e a auditoria;
    - sem rolagem lateral em 1280 e em 360 px;
    - no console, só o 401 esperado do `/api/auth/eu` no login;
    - os issues são só os `FormEmptyIdAndNameAttributesForInputError` que já existiam, na busca da lista e no funil;
  - no banco e nos logs da API desse Compose, nenhum dado dos dois leads anonimizados. O painel e o CSV do relatório ficaram iguais byte a byte, e o reenvio do lead anonimizado respondeu `ja_recebido`;
  - no Excel, aberto como no clique duplo:
    - os acentos certos;
    - o telefone do Brasil e o CPF/CNPJ como texto, com o zero do começo do CPF;
    - o apóstrofo no telefone de fora e no nome que começava com `=`;
    - a entrada como data.
- Passo 10 validado manualmente por mim em 12/10/2026, em desenvolvimento, com a API no contêiner:
  - um lead de teste pela integração, com a chave do `.env`, entrou com a hora certa, sem responsável e visível só para o admin. A exclusão pela ficha funcionou, e um lead da massa não tem a opção de excluir;
  - os dados do titular vieram num JSON completo, com os acentos normais. Os do lead anonimizado vieram sem dados pessoais e com o hash no lugar do id externo;
  - a anonimização deixou a ficha como "Titular removido", os totais do painel não mudaram, e a Adriana vê o lead sem os dados;
  - o CSV de leads no Excel: colunas e acentos certos, telefone no formato nacional, documento com a máscara (inclusive o CNPJ alfanumérico), datas no horário de Brasília e o lead anonimizado sem dados pessoais;
  - a auditoria registra os logins (inclusive o recusado), a criação, a exclusão, as exportações e a anonimização, com quem fez e a hora, sem dados pessoais. A exportação com busca grava "1 lead, busca", sem o termo buscado;
  - logada como Adriana: sem Auditoria no menu, sem Exportar CSV e sem o cartão Privacidade. O `/auditoria` volta para o funil, e o `/api/leads/exportacao` responde 403;
  - o `preparar-demonstracao` pelo contêiner gravou 60 leads, 11 ganhos e 8 perdidos. A sessão continuou valendo, e as telas se atualizaram sozinhas: com o `start api`, o tempo real reconecta, e o front recarrega tudo.
- Em 12/10/2026, a consulta da auditoria ganhou um teste próprio do 403 da atendente, mesmo filtrando pelos registros dela (`AuditoriaTests`). As outras quatro rotas do Passo 10 já tinham o dele, com um lead da própria atendente: a exportação CSV (`ExportacaoLeadsTests`), os dados do titular e a exclusão (`LgpdTests`) e a anonimização (`AnonimizacaoTests`). O `AutorizacaoTests` cobre todas as rotas de admin, mas só as que têm a política: uma rota nova sem o atributo sairia da lista sem o teste falhar.
- Do Passo 10, fica pendente a revisão das configurações de execução dos fluxos no n8n de produção:
  - conferir que as execuções de produção que deram certo não são salvas (os JSONs já levam `saveDataSuccessExecution: none`);
  - apontar o fluxo de erro do Alertas para o id novo do `CRM - Erros`;
  - ver por quanto tempo ficam as execuções com erro.
- Do Passo 10, fica também uma mudança que decidi em 12/10/2026, para fazer só depois da reunião de 13/10/2026. Até lá, o código da mudança de etapa não muda, porque a demonstração usa o funil.
  - O problema: o lead anonimizado ainda aceita o detalhe do motivo ao ir para Perdido. O detalhe é texto livre e pode trazer um dado pessoal de volta. A especificação (3.8) deixa a etapa mudar, para o admin fechar o lead.
  - A mudança: num lead anonimizado, a mudança para Perdido recusa um detalhe preenchido com 400, como a edição e a interação. O motivo continua obrigatório. O `MotivoPerdaDialog`, aberto pela ficha (`EtapaDoLead`) e pelo quadro (`useMovimento`), esconde o campo do detalhe.
  - No mesmo commit: a seção 3.8 da especificação, um teste de integração (400 com o detalhe e 200 sem ele) e os testes do front, na ficha e no quadro.
  - Ganho já é impossível num lead anonimizado, porque exige o CPF/CNPJ.

## Arquitetura

O CRM guarda os dados e aplica as regras. O n8n só conecta sistemas externos e conversa com o CRM pela API.

- Regra de negócio (deduplicação, rodízio, permissões, funil, métricas) fica no CRM, nunca no n8n.
- O n8n não acessa o banco. Ele usa apenas `/api/integracoes/*`, com a chave de integração.
- O CRM avisa o n8n por eventos: grava em `eventos_saida` na mesma transação da mudança e um serviço em segundo plano entrega. Nunca chame o n8n dentro de uma requisição do usuário.

## Stack

- API: ASP.NET Core (.NET 10) com controllers, EF Core 10 + Npgsql (nomes em snake_case), ASP.NET Core Identity com cookie, SignalR, FluentValidation, libphonenumber-csharp, OpenAPI + Scalar em desenvolvimento
- Banco: PostgreSQL 17
- Web: React + TypeScript + Vite, React Router, TanStack Query, React Hook Form + Zod, Tailwind + shadcn/ui, dnd-kit, @microsoft/signalr, vite-plugin-pwa. Os gráficos do painel são barras em HTML e CSS, sem biblioteca; o Recharts só entra quando um gráfico precisar dele, como uma linha por dia
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
n8n/                         JSON exportado dos fluxos do n8n
deploy/                      Caddyfile e scripts de backup e de teste da restauração, do Compose de produção
docs/                        especificação, guia de uso do Claude Code e CSVs de exemplo da importação (exemplos/)
Dockerfile                   imagem de produção: a API com o build do React e o migration bundle
docker-compose.yml           desenvolvimento: PostgreSQL, e a API e o dotnet ef num contêiner Linux
docker-compose.prod.yml      produção: Caddy, API, PostgreSQL, migrations e backup
docker-compose.testes.yml    os testes .NET num contêiner Linux
.claude/                     permissões do projeto e o comando /passo
.mcp.json                    servidor MCP do n8n
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
cd web && npm run lint && npm run format:check           # ESLint e Prettier do front
docker compose run --rm ef migrations add <NomeDescritivo>   # dotnet ef num contêiner (nesta máquina, o jeito que roda)
dotnet tool restore                                      # uma vez por clone, para ter o dotnet ef no Windows
dotnet ef migrations add <NomeDescritivo> --project src/Crm.Api
docker compose run --rm --service-ports api --N8n:WebhookUrl=   # API sem mandar eventos ao n8n, com o api parado (no Windows: dotnet run --project src/Crm.Api -- --N8n:WebhookUrl=)
docker compose run --rm -e Demo__Permitir=true api preparar-demonstracao   # APAGA os leads e grava a massa da demonstração, com o api parado (README › Demonstração)
docker compose -f docker-compose.prod.yml up -d --build  # produção, no servidor (README › Publicação)
docker compose -p crm-local -f docker-compose.prod.yml --env-file <arquivo> up -d --build   # produção no próprio PC
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
- A CSP do Caddy (`deploy/Caddyfile`) só aceita a própria origem, mais os estilos inline do sonner e do Radix, e não tem `'unsafe-eval'`. Um recurso externo novo (fonte, imagem, script) precisa entrar nela. O `connect-src` repete o domínio com `wss://`, para o WebSocket do tempo real no Safari antigo; com `DOMINIO=localhost` numa porta que não é a 443, quem cobre o WebSocket é o `'self'`, o que basta no Chrome.
- O `z` vem de `@/lib/zod`, que liga o `jitless` antes de qualquer esquema. Com o `z` direto do `zod`, o Zod testa o `new Function` ao criar cada esquema de objeto, e o Chrome registra a violação da CSP, mesmo com o erro tratado. O ESLint barra o import de `zod`.
- Para ver o console e os issues do DevTools sem abrir o navegador (por exemplo, uma violação da CSP), use o Chrome sem janela (`--headless --remote-debugging-port=<porta> --user-data-dir=<pasta temporária>`) e o protocolo do DevTools (`Log`, `Runtime` e `Audits.issueAdded`), contra o Compose de produção local. Os quadros do WebSocket do tempo real chegam pelo `Network.webSocketFrameReceived`. Os issues `FormEmptyIdAndNameAttributesForInputError` do funil (a busca e as seleções sem `id` nem `name`) já existiam antes do Passo 7.
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

## n8n

- O servidor MCP `n8n` do `.mcp.json` aponta para a instância de desenvolvimento, que acaba em 06/10/2026, e fica desconectado até o n8n de produção existir. Até lá, não crie nem altere fluxos, e liste como pendente o que depender do n8n. Quando o `.mcp.json` apontar para o n8n de produção, use o MCP para criar, validar e testar os fluxos da seção 4.5 da especificação.
- Nunca publique, ative nem execute um fluxo sem confirmação do usuário: um fluxo ativo pode mandar e-mail de verdade.
- Todo fluxo do CRM tem o nome começando por `CRM - `. Credenciais ficam no n8n, nunca dentro do fluxo.
- Depois de criar ou alterar um fluxo, salve o JSON dele em `n8n/<nome-do-fluxo>.json`. O `FluxosN8nTests` confere que cada JSON é de um fluxo `CRM - ` e não salva as execuções de produção que deram certo (`saveDataSuccessExecution: none`).

## Como trabalhar

- Siga os passos da seção 10 da especificação nesta ordem, que vale desde 05/10/2026:
  1. Passos 7 e 8, tudo local. A demonstração para o chefe vem logo depois do 8.
  2. Depois, os Passos 9 e 10, enquanto o servidor não chega.
  3. Com o servidor: a publicação que falta do Passo 5, o Passo 6 e o Passo 11.
- Um passo só termina quando os critérios de aceite passam.
- O usuário inicia cada passo com `/passo N`. O procedimento completo está em `.claude/skills/passo/SKILL.md`.
- A leitura do `.env` é bloqueada em `.claude/settings.json`. Se precisar de um valor de configuração, use o `.env.example` ou pergunte ao usuário.
- Antes de codar um passo, mostre o plano (arquivos, migrations, endpoints, telas) e espere aprovação.
- Rode `dotnet test` e `npm test` antes de dizer que terminou. Faça commits pequenos, um assunto por commit.
- A suíte do .NET (pelo contêiner) e a do front rodam uma depois da outra, nunca ao mesmo tempo, nem uma em segundo plano enquanto a outra roda.
  - Juntas, elas deixam a máquina lenta, e os testes do front estouram o tempo de espera de 1 segundo do `findBy` e do `waitFor`, falhando até em telas que não mudaram.
  - Em 08/10/2026, rodando junto com a do .NET, a suíte do front teve 5 falhas, com arquivos levando até 34 s; sozinha, passou inteira.
- Se algo mudar uma regra, atualize `docs/especificacao.md` no mesmo commit.
- As decisões em aberto da seção 11 têm um padrão definido. Implemente o padrão e deixe configurável quando a especificação pedir.
