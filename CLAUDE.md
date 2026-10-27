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
- Passos 0 a 3 validados manualmente por mim em 30/09/2026, além dos testes automáticos:
  - login do admin, troca de senha obrigatória e cadastro da Adriana e da Pietra;
  - login e troca de senha da Pietra;
  - cadastro manual de leads, aviso "Sem CPF/CNPJ" e botões de contato conforme os dados do lead;
  - funil: mudança de etapa, motivo obrigatório em Perdido e recusa de Ganho sem CPF/CNPJ, com o botão "Abrir a ficha";
  - lead cadastrado à mão fica "Ainda sem contato" até a primeira interação;
  - visibilidade: logada como Pietra, os leads da Adriana dão "Lead não encontrado", e o funil dela fica vazio, sem o filtro de responsável e sem o menu Configurações.
- O banco de desenvolvimento já tem o admin, a Adriana e a Pietra (aliases +adriana e +pietra), e o `.env` tem `Admin__Email`, `Admin__SenhaInicial` e `Integracao__Chave`.
- O n8n Cloud de teste (`matheusbordinhao.app.n8n.cloud`) acabou em 06/10/2026. A produção vai ser o n8n Cloud, numa conta da empresa, quando o pagamento sair (decidido em 14/10/2026).
- Desde 14/10/2026, o n8n de desenvolvimento é um n8n Community 2.43.5 no `docker-compose.yml` (serviço `n8n`, README › n8n local):
  - a interface fica em `http://localhost:5679`, porque a 5678 é de outro n8n desta máquina, um projeto do Compose no WSL (`/home/mathe/n8n`) que não é do CRM e não deve ser mexido;
  - dentro da rede do Compose, a API chama `http://n8n:5678`, e os fluxos chamam `http://api:5080`;
  - o `.env` tem `N8n__WebhookUrl=http://n8n:5678/webhook/crm-alertas`: com a API no ar, cada evento vira um e-mail de verdade;
  - as credenciais são `CRM - Chave de integração` (`X-Api-Key`), `CRM - Chave dos eventos` (`X-Crm-Chave`) e `CRM - E-mail` (SMTP do `testecrmfs@gmail.com`, uma conta só para os testes, com senha de app);
  - as Data Tables são a `crm_entradas_pendentes`, a fila do reenvio, a `crm_entradas_recusadas`, com as entradas recusadas, por 7 dias, e a `crm_envios_site`, com a hora de cada envio do site, por 31 dias;
  - a chave do Google de desenvolvimento fica em `C:\dev\crm-google-chave-dev.txt`, fora do repositório, e só o SHA-256 dela está no fluxo.
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
- Passo 6 e os fluxos validados pelo Claude em 14/10/2026, no n8n local, com a minha autorização para publicar e executar:
  - `site.json`: 200 `{"recebido":true}`; o lead foi para a Adriana pelo rodízio, e o `lead.criado` foi entregue ao Alertas 7 s depois. O robô não criou nada, e o envio sem nome deu 400 no CRM e o e-mail do `CRM - Erros`;
  - Google: a chave errada deu 403 e o e-mail do `CRM - Erros`. Com a chave certa, num comando que eu rodei, entrou um lead de teste com a campanha provisória, e o segundo envio foi `ja_recebido`;
  - com a API parada, o lead do site foi para a fila ("Falha de conexão"); com a API de volta, o Reenvio manual o criou e apagou a linha;
  - Meta: o `test_workflow` com os exemplos no lugar do gatilho e da Graph API montou o lead certo. Passado pela fila e pelo Reenvio, o lead de teste entrou com campanha, conjunto, anúncio, formulário e plataforma, e o segundo envio foi `ja_recebido`;
  - Relatório: o CSV da semana anterior chegou anexo ao meu e-mail, com 535 bytes, BOM e CRLF, igual ao da API;
  - o meu cadastro manual (lead 202) foi para a Pietra, e o alerta chegou no `+pietra`.
- Em 14/10/2026, a chave de integração vazou nos dados de uma execução do Relatório. O "Buscar o CSV" baixava a resposta como arquivo, e nesse modo o n8n guarda a requisição inteira, com o cabeçalho `X-Api-Key`. O que foi feito:
  - o fluxo passou a baixar o CSV como texto;
  - eu troquei o `Integracao__Chave` e a credencial;
  - as execuções de teste foram apagadas;
  - uma varredura sem mostrar o conteúdo confirmou que nenhum outro fluxo guarda a chave.
- Também em 14/10/2026, entrou a tela de campanhas (Configurações › Campanhas, `PUT /api/campanhas/{id}`), e o cliente do SignalR dos testes subiu para a 10.0.13, por causa da CVE-2026-70159. Nesse dia, sem a correção, a auditoria do NuGet derrubava o build dos testes.
- Falta, do n8n:
  - levar os 8 fluxos e as 3 Data Tables para o n8n Cloud (README › n8n Cloud: importar os fluxos);
  - decidir o horário do Reenvio quando o plano do n8n Cloud for escolhido, porque cada rodada agendada conta como execução (Lote 2A);
  - o app da Meta em modo Live e o "Enviar dados de teste" de verdade do Google Ads;
  - o e-mail da Gama, no lugar do meu, no Relatório;
  - apontar o `.mcp.json` para o n8n Cloud e remover o MCP local.
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
- No banco de desenvolvimento, a Adriana e a Pietra estão no rodízio desde 06/10/2026, como precisam estar na demonstração: os leads da integração, os importados pelo rodízio e os cadastrados com "Rodízio" se alternam entre as duas. Desde 08/10/2026, o banco tem a massa da demonstração (`preparar-demonstracao`), e os leads de validação, como "teste" a "teste4", saíram. Em 12/10/2026 e em 13/10/2026, nas validações dos Passos 10 e 11, o comando rodou de novo, pelo contêiner. Em 14/10/2026, depois dos testes do n8n, ele rodou de novo: 60 leads, nenhum de teste, a fila de eventos vazia e a Adriana como a próxima da vez. Para voltar a esse estado antes da demonstração, é só rodar o comando de novo.
- Falta a publicação do Passo 5, que depende do servidor, do domínio e do destino da cópia do backup. No servidor, repetir o HTTPS com certificado de verdade, a sessão depois de reiniciar a API, o IP real na auditoria, o teste da restauração e a conferência do log do Caddy com a API parada (README › Cuidados). Antes de publicar, também conferir na auditoria o IP de um cliente IPv4 e o de um IPv6, como um celular no 4G. Se o servidor tiver IPv6 e a rede do Compose não, o Docker repassa essas conexões pelo `docker-proxy`, e a auditoria mostra o gateway da rede (`172.30.0.1`). Com um proxy ou CDN na frente do Caddy, ele precisa do `trusted_proxies`.
- O log do Caddy saiu das pendências da publicação em 14/10/2026, antes do congelamento do código:
  - o bloco global do `deploy/Caddyfile` passa o log padrão do Caddy, onde caem o log de erro e o dos outros módulos, pelo `format filter`: a query string vira `?REDACTED`, e o `Referer` e o `X-Api-Key`, `REDACTED`. O `Cookie` e o `Authorization` continuam como `["REDACTED"]`, pelo próprio Caddy, e o log de erro continua ligado;
  - o `LogDoCaddyTests` sobe a imagem do Caddy do `docker-compose.prod.yml` com o Caddyfile de verdade e confere a entrada do 502. Com o Caddyfile anterior, ele falha na URL com a busca;
  - conferido pelo Claude num Compose de produção descartável (`-p crm-caddylog`, portas 8096 e 8465, rede `172.31.96.0/24`), sem build, com a imagem `crm-api` que já existia, e com a API parada:
    - controle: com o Caddyfile anterior, montado por um override, os cinco 502 gravaram a busca, o `Referer`, o `X-Api-Key` e o `id` da conexão do tempo real;
    - com o Caddyfile novo, nenhuma dessas marcas, em HTTP/1.1 e em HTTP/2, com o `x-api-key` em minúsculas e no WebSocket. O caminho continuou na URL (`/api/leads/exportacao?REDACTED`), e a URL sem query ficou igual;
    - com `level DEBUG` numa cópia do Caddyfile, os logs do próprio `reverse_proxy` (`upstream roundtrip`, com a requisição repassada à API) também saíram filtrados;
    - com a API no ar, o proxy e os cabeçalhos de segurança não mudaram, e o 502 do `/health` com a API subindo (`connection refused`) continuou no log;
    - o redirecionamento da porta 80 para o HTTPS não grava nada, e o Caddy não grava arquivo de log no contêiner;
  - a rotação já existia: os cinco serviços do Compose de produção usam o `json-file` com 5 arquivos de 10 MB (`x-logs`), o que o `docker inspect` confirmou nos contêineres.
- Desde 02/10/2026, o Controle Inteligente de Aplicativos do Windows desta máquina bloqueia o `Testcontainers.dll`, que não é assinado (evento 3077 do Code Integrity), e, em 06/10/2026, bloqueou também o `Crm.UnitTests.dll` recém-compilado: o `dotnet test` do Windows falha com "Uma política de Controle de Aplicativo bloqueou este arquivo". Os testes .NET rodam pelo contêiner (`docker-compose.testes.yml`): 721 de 721 em 14/10/2026, com os dos fluxos do n8n e os das campanhas, e os do front, 260 de 260.
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
- Do Passo 10, fica pendente a conferência das configurações de execução no n8n de produção. Desde o Lote 2A, nenhum fluxo salva execução, e os JSONs levam as quatro configurações. Lá:
  - conferir que elas valem depois de importar os fluxos;
  - apontar o fluxo de erro de cada fluxo para o id novo do `CRM - Erros`;
  - ver por quanto tempo o plano guarda uma execução "crashed".
- A mudança que decidi em 12/10/2026 para depois da reunião entrou em 13/10/2026, antes do congelamento do código: o lead anonimizado vai para Perdido só com o motivo.
  - O detalhe é texto livre e poderia trazer um dado pessoal de volta. Preenchido, responde 400 no campo `motivoPerdaDetalhe` (`RegrasFunil.Conferir`); o motivo continua obrigatório.
  - O cartão do funil traz `anonimizado`, e o `MotivoPerdaDialog`, aberto pela ficha (`EtapaDoLead`) e pelo quadro e pela lista do celular (`useMovimento`), esconde o campo do detalhe.
  - Testes: o unitário da regra, o de integração (400 com o detalhe, sem mudar a etapa nem o histórico, e 200 sem ele, em `AnonimizacaoTests`) e os do front, na ficha e no funil.
  - Ganho já é impossível num lead anonimizado, porque exige o CPF/CNPJ.
- As outras pendências da validação do Passo 10 entraram em 13/10/2026:
  - a auditoria e o limite de logins gravam o IPv4 sem o prefixo `::ffff:` (`IpDoCliente`). Os registros antigos de desenvolvimento ficam como estão, porque a auditoria não tem registro reescrito;
  - o Npgsql entra no banco sem GSS (`DadosExtensions.SemGss`), e o `Cannot load library libgssapi_krb5.so.2` sumiu da API, das migrations e do `preparar-demonstracao`;
  - o telefone do Brasil aparece no formato nacional na lista e na ficha (`formatarTelefone`). O de fora e o não reconhecido ficam como foram guardados, e os links de WhatsApp e Ligar continuam com o E.164;
  - todo campo de formulário tem `id` ou `name`;
  - o `npm audit` acusou 7 vulnerabilidades altas, todas em ferramentas de desenvolvimento e build. Nenhuma vai para o pacote do navegador nem para a imagem de produção, que leva só o `web/dist`. São a CLI do `shadcn` (pela cadeia `ts-morph` › `fast-glob` › `micromatch` › `braces`) e o `source-map-js`. Nenhum pacote mudou.
- Passo 11 adiantado e feito em 13/10/2026, menos as etapas definitivas, o celular de verdade e a divisão do pacote por tela (seção 10 da especificação):
  - PWA: o manifesto, os ícones e o service worker (`web/public/sw.js`), só no build de produção, sem `fetch` nem cache. A notificação nativa sai por ele, também no celular. Sem ele, em desenvolvimento e no `https://localhost` do Compose local, ela continua pelo `new Notification` do Passo 7;
  - celular: botões, campos e seleções com 40 px de altura abaixo do `md`; o computador não mudou;
  - README de operação (publicação com o que depende do servidor e do domínio, troca de chaves e senhas, plano B e pedido do titular) e o guia das atendentes (`docs/guia-das-atendentes.md`).
- Conferências do Passo 11 feitas pelo Claude em 13/10/2026, num Compose de produção descartável (`-p crm-celular`, portas 8094 e 8461, rede `172.31.94.0/24`), com o Playwright num contêiner:
  - nenhuma linha do `libgssapi` no log da API, no das migrations e no `preparar-demonstracao`;
  - em 360 e 412 px, como admin e atendente, no login, no funil, na lista, na ficha e no painel, com os diálogos, o menu e o sino: sem rolagem lateral, sem texto cortado e sem campo com fonte menor que 16 px;
  - o PWA de ponta a ponta:
    - o service worker controla a página, e o manifesto não tem erros (`application/manifest+json`, `no-cache`);
    - a notificação de um lead novo saiu só com o título e, nos dados, o id e o endereço da ficha;
    - o clique no service worker abriu a ficha e marcou a notificação como lida;
  - pelo Caddy, só o 401 esperado do login no console, sem violação da CSP e sem nenhum issue do DevTools;
  - o roteiro do guia: uma atendente nova fez o caminho do guia, do primeiro acesso a Ganho e Perdido, no computador e em 360 px, achando cada botão pelo nome escrito no guia.
- Passo 11 validado manualmente por mim em 13/10/2026, em desenvolvimento (`localhost:5173`, com a API no contêiner):
  - notificação no computador: um lead que o admin cadastrou com "Rodízio" foi para a Adriana, e a notificação do Windows saiu só com "Administrador cadastrou um lead para você";
  - telefone: a lista e a ficha no formato nacional, e editar só o nome gravou "Campos: nome" na auditoria;
  - auditoria: os registros novos saem com `172.18.0.1`, sem o `::ffff:`;
  - lead anonimizado: no funil, Perdido pediu só o motivo, sem o detalhe, e o lead foi para Perdido;
  - PWA: instalado pelo menu do Chrome, o CRM abriu numa janela própria, com o nome e o ícone;
  - celular: a lista, o menu, o funil (com o "Mover para") e o painel em 400 px, na visão de dispositivo do Edge, sem rolagem lateral nem texto cortado;
  - no fim, o `preparar-demonstracao` rodou de novo no banco de desenvolvimento.
- Do Passo 11, fica pendente:
  - as etapas definitivas, com a Gama;
  - a instalação e a notificação num celular de verdade, que exigem o HTTPS do servidor;
  - a divisão do pacote por tela (rotas sob demanda), no Lote 4B, porque mexe no carregamento de todas as telas. O build ainda avisa que o pacote passa de 500 kB;
  - no Lote 4A, os alvos de toque secundários ainda menores que 40 px no celular: os botões do cartão Privacidade (só do admin), o "Leads" de voltar da ficha, o "Buscar", com 38 px (o Enter também busca), e o fechar dos diálogos, que têm o Cancelar;
  - no Lote 4A, o aviso "Avisos do navegador", que no celular fica no topo e cobre o menu e o sino até a pessoa responder. O guia manda responder logo depois de entrar;
  - a validação com a Adriana e a Pietra, usando o guia: o aceite do Passo 11.
- Pendências novas, sem implementar ainda:
  - no Lote 3, aceitar a chave de integração antiga e a nova durante a troca, para nenhuma entrada se perder com 401 (README › Trocar chaves e senhas);
  - no Lote 4A, o PR #2 do Dependabot (Caddy 2.11.7 e n8n 2.43.7):
    - ele não tem nenhuma rodada do CI, porque foi aberto antes de os workflows existirem na `master`. Antes da avaliação, um `@dependabot rebase` faz o CI rodar nele;
    - como ele sobe o n8n, o volume `crm-leads_dados-n8n` é copiado e conferido antes, pelo roteiro do README (Versões fixadas › O n8n numa versão nova);
    - o roteiro foi testado pelo Claude em 21/10/2026, com os mesmos comandos do README, em volumes de teste:
      - a cópia e a volta deixaram os mesmos arquivos, donos, permissões e conteúdos;
      - a conferência pegou um byte e um arquivo a mais;
      - a volta com a cópia inexistente, vazia, sem o `config` ou com o banco vazio parou sem apagar nada.
  - fechar as portas na máquina, e não só no Compose, pela regra "Docker Desktop Backend" do firewall ou pelo `ip` padrão do Docker. A proposta do Lote 1C espera a minha decisão, depois do push.
- Lote 1 (infraestrutura), feito em 19/10/2026:
  - toda imagem tem a versão exata (README › Versões fixadas):
    - o SDK 10.0.402, que traz o runtime 10.0.13 com a correção da CVE-2026-70159, no build de produção, na API de desenvolvimento, no `dotnet ef` e nos testes;
    - o ASP.NET Core 10.0.13 na imagem de produção;
    - a `caddy:2.11.4`, a `postgres:17.11` em desenvolvimento e em produção e a `node:22.23.3-alpine3.24` no build do front;
    - o `ImagensTests` cobra isso, e os testes de integração usam a imagem do PostgreSQL do Compose de produção;
  - o `PublicacaoTests` exige o `logging: *logs` em todo serviço do Compose de produção;
  - o Dependabot (`.github/dependabot.yml`) abre um PR por mês com as imagens, com o `Dockerfile` e os Compose juntos, e outro com o NuGet, sem o npm e sem versão principal. Ele só vale depois do push, com os alertas ligados em Settings › Code security;
  - conferido pelo Claude:
    - a suíte .NET no contêiner: 729 de 729, em 3 min 14 s com o build;
    - a API de desenvolvimento recriada com o ASP.NET Core 10.0.13, e o banco recriado com a `postgres:17.11`, com os dados de antes (62 leads). A `postgres:17.11` é uma reconstrução mais nova da imagem que o `postgres:17` do cache, com o mesmo PostgreSQL;
    - a imagem de produção num Compose descartável (`-p crm-imagens`, portas 8097 e 8467), com a imagem da API com outro nome, para não trocar a do plano B. O build levou 1 min 52 s. As migrations terminaram com `Exited (0)`. A API rodou o ASP.NET Core 10.0.13, o Caddy foi o v2.11.4, e o PostgreSQL, o 17.11. Pelo Caddy, o `/health` e o login responderam 200, com HSTS e CSP. No fim, o projeto saiu com `down -v`;
    - num banco novo, o migration bundle registra um "Failed executing DbCommand" do `SELECT` na `__EFMigrationsHistory`, que ainda não existe, e segue aplicando as migrations. É o comportamento do EF Core, não deste lote;
    - a suíte do front, que este lote não mudou: com o padrão de workers, de 2 a 4 falhas de tempo por rodada, em arquivos diferentes, que passam sozinhos; com `--maxWorkers=4`, 260 de 260. A máquina estava com 2,7 GB de memória livre;
    - a demonstração: o `preparar-demonstracao` gravou 60 leads, e o `site.json` criou o lead 328, que foi para a Adriana, com o `lead.criado` entregue na primeira tentativa, 5,6 s depois;
  - validado por mim em 19/10/2026: o e-mail de alerta do lead 328 chegou no celular da Adriana, às 10:08.
- A causa do teste instável do front, trazida do Lote 4A em 19/10/2026, foi medida e corrigida em 20/10/2026, antes do Lote 1B: o CI vai rodar a suíte do front num runner de 2 vCPUs, e, com ela falhando por tempo, ele ficaria vermelho à toa.
  - Todas as falhas eram de tempo (esperas de 1 s e limites de 5 s por teste), nenhuma de asserção.
  - A causa principal era a disputa: o Vitest abria 7 workers nos 4 núcleos, a memória livre caía a cerca de 300 MB, e tudo ficava de 4 a 6 vezes mais lento. Só o render inicial de uma tela ia de 150 ms para 700 a 940 ms.
  - Agravava o custo das consultas por papel em telas grandes (Armadilhas conhecidas).
  - O teste do service worker não tinha corrida: a mensagem chegava e a navegação acontecia, mas, com a disputa, a ficha não renderizava a tempo. O `TempoReal.tsx` não mudou.
  - A correção:
    - no máximo 2 workers (`maxWorkers: '25%'`, no `web/vite.config.ts`);
    - consultas mais baratas nos testes da ficha, da importação e do painel, sem mudar o que eles conferem, e sem os prazos de 3 s;
    - o `INTERVALO_ANDAMENTO` num módulo próprio (`importacao/andamento.ts`), que os testes trocam por `vi.mock`, com a consulta a cada 200 ms. Na tela, ele continua com 1 s.
  - Conferido pelo Claude, com a suíte inteira:
    - antes, em 19/10/2026, com o padrão: de 3 a 13 falhas por rodada, em 80 a 106 s;
    - em 20/10/2026, com 4 workers e a máquina mais ocupada: 2 das 10 rodadas falharam, por lentidão geral;
    - com 2 workers: 10 rodadas seguidas sem falha, em 97 a 134 s;
    - no contêiner de 2 CPUs, com 1 worker, como o CI: 5 rodadas seguidas sem falha, em 173 a 197 s (antes, em 185 a 229 s). O "Perdido pede o motivo", o teste de menos folga, levou de 684 a 1.350 ms, com mediana de 776 ms (antes, 1.140 ms).
  - A medição vale com a máquina sem carga pesada de fora. Em 20/10/2026, um vídeo no Chrome ocupou de 3 a 4 threads, e a máquina ficou cerca de 1,7 vez mais lenta: no contêiner com 1 worker, 4 testes falharam por tempo (3 esperas de 1 s e 1 limite de 5 s). Essas rodadas ficaram fora da conta.
  - No runner do GitHub, a folga é maior que no contêiner daqui: no Lote 1B, as 5 rodadas da suíte do front passaram, cerca de 2 vezes mais rápidas.
  - Na demonstração de fim do item, o `preparar-demonstracao` gravou 60 leads, e o `site.json` criou o lead 389, que foi para a Adriana, com o `lead.criado` entregue na primeira tentativa, 9,8 s depois.
  - Validado por mim em 20/10/2026: o e-mail de alerta do lead 389 chegou no celular.
  - O volume `crm-medicao-node-modules`, com o `node_modules` do contêiner da medição, foi apagado no fim do Lote 1B, em 20/10/2026.
- Lote 1B (CI no GitHub Actions), feito e validado em 20/10/2026, com o registro do fechamento em 21/10/2026:
  - três workflows em `.github/workflows` (README › Testes e qualidade › CI no GitHub Actions):
    - `testes.yml`, em todo push na `master` e em todo PR, menos os só de `.md`: a suíte .NET, o `dotnet format` e o front completo;
    - `imagem.yml`, quando muda o que entra na imagem ou nos Compose: o build e o Compose de produção com dados sorteados, com as migrations, o `/health` pelo Caddy e o runtime 10.0.13 ou mais novo;
    - `front-5-rodadas.yml`, disparado por mim pela tela do Actions: a suíte do front em 5 runners ao mesmo tempo, com o resumo de cada rodada na página dela;
  - o front roda pelo `.github/scripts/front.sh`, na imagem do Node do `Dockerfile`, e o resumo das rodadas sai do `.github/scripts/resumo-do-front.mjs`;
  - o Dependabot ganhou as ações, num PR por mês, com as versões principais;
  - o `WorkflowsTests` cobra a permissão só de leitura, o SHA das ações, o `persist-credentials: false` e a falta de segredos e do `pull_request_target`;
  - conferido pelo Claude:
    - o `actionlint` 1.7.12, com o `shellcheck`, sem erros nos três workflows, e o `shellcheck` sem avisos no `front.sh`;
    - três regras quebradas de propósito (o `persist-credentials`, uma tag no lugar do SHA e o `pull_request_target`) deram 4 falhas no `WorkflowsTests`, e só elas;
    - o `front.sh` no contêiner de 2 CPUs: o `completo` em 259 s, com a suíte em 176 s, e o `suite` em 184 s, com o resumo. O `node_modules` e o `dist` do Windows não mudaram, e nenhum volume ficou;
    - o `dotnet format` levou 77 s, com o restore pronto, e o build da imagem sem cache, 128 s, com as imagens base já baixadas;
  - no primeiro push (`4e6872c`) e no PR #3 do Dependabot, o `Testes` falhou na suíte .NET, com `Read-only file system : '/src/TestResults'` e o código 134. A formatação e o front passaram:
    - o `dotnet test` cria a pasta dos resultados na pasta de trabalho, que no contêiner é o `/src`, só para leitura. Aqui passava porque a pasta do projeto tinha uma `TestResults` vazia, apagada no fim do lote (Armadilhas conhecidas);
    - reproduzido pelo Claude num clone limpo, com volumes novos, e corrigido com o `--results-directory /artefatos/TestResults` (`662d61d`). Num clone novo da correção, também com volumes novos, a suíte deu 749 de 749, e a formatação passou;
  - o Dependabot fechou o PR #1 e abriu o #3, com os mesmos 12 pacotes do NuGet. É ele que passa pelo CI, com o `@dependabot rebase` depois da correção no GitHub;
  - validado por mim no GitHub, em 20/10/2026:
    - com a correção (`40133cc`), o `Testes` ficou verde em 4 min 37 s, e o `Imagem de produção`, em 2 min 24 s. O `Imagem de produção` também passou nas rodadas anteriores, em 2 min 22 s e 2 min 14 s;
    - no PR #3, cada workflow rodou uma vez só, pelo `pull_request`, sem rodada a mais pelo push do branch do Dependabot: o `push` dos workflows vale só para a `master`;
    - o vermelho já apareceu numa falha de verdade, a da `TestResults`, com o e-mail de aviso do GitHub;
    - as 5 rodadas do front passaram, com 260 testes e nenhuma falha em cada uma, e o disparo terminou em 2 min 40 s:
      - a suíte levou de 73 a 121 s (mediana de 91 s), contra 170 a 193 s no contêiner de 2 CPUs daqui, pela mesma medida do resumo: cerca de 2 vezes mais rápido, de 1,4 a 2,6 vezes nos extremos;
      - o processador muda de uma rodada para outra (AMD EPYC 9V45, 9V74 e 7763), e a 7763 foi a mais lenta;
      - o "Perdido pede o motivo" levou de 397 a 644 ms, contra 684 a 1.350 ms aqui;
      - o mais lento em todas foi o "recarrega quando um lead ou o funil muda, pelo tempo real", de 1,09 a 1,15 s, que espera o relógio de verdade e tem prazo próprio. Fora ele, nenhum teste passou de 903 ms;
      - com isso, o critério das 5 rodadas está cumprido, e a folga no runner é maior que no contêiner daqui;
  - o push só de `.md` (`e4cfd71`) não rodou nenhum workflow, nem o Automatic Dependency Submission, que é do próprio GitHub;
  - o PR #3, depois do `@dependabot rebase`, ficou verde: o `Testes` #4 em 6 min 58 s e o `Imagem de produção` #4 em 2 min 23 s. No rebase, o Dependabot trocou o título para "Bump FluentValidation and 11 others";
  - a demonstração no branch do PR #3 valeu como a de fim de lote:
    - o `preparar-demonstracao` compilou com os 12 pacotes novos e gravou 60 leads;
    - a API subiu com eles (FluentValidation 12.2.0, Identity 10.0.13, Scalar 2.18.2 e libphonenumber 9.0.41);
    - o `site.json` criou o lead 450, que foi para a Adriana, com o `lead.criado` entregue na primeira tentativa, 5,7 s depois;
    - validado por mim: o e-mail do lead 450 chegou às 13:34, no endereço da Adriana, com os dados certos e o link da ficha;
  - o merge foi pelo "Squash and merge", com o título em português e o corpo do Dependabot (`b25ade7`). Na `master`, o `Testes` #5 ficou verde em 6 min 47 s, e o `Imagem de produção` #5, em 2 min 2 s;
  - por passo, do `Testes` #3 (pacotes antigos) para o #5 (pacotes novos):
    - a suíte .NET, de 2 min 6 s para 2 min 50 s (1,35 vez);
    - a formatação, de 38 s para 1 min (1,58 vez);
    - o front, de 1 min 45 s para 2 min 46 s (1,58 vez);
    - o job inteiro, de 4 min 33 s para 6 min 41 s (1,47 vez);
    - o front não usa nenhum pacote do NuGet e ficou mais lento na mesma proporção, e a suíte .NET até menos: a diferença é da máquina do runner, e não dos pacotes;
  - depois do merge, conferido pelo Claude:
    - a pasta do projeto na `master`, igual à `origin/master`, e a API recriada a partir dela, com os pacotes novos;
    - a árvore igual à do branch do #3, fora os `.md` do `e4cfd71`;
    - o branch local apagado, e o `git fetch --prune` tirou as referências do #1 e do #3;
  - na limpeza, em 20/10/2026, saíram o volume `crm-medicao-node-modules` e a pasta `TestResults` vazia da raiz da pasta do projeto.
- Lote 5 (dependências do front), antecipado para antes do 2A e feito em 21/10/2026:
  - o motivo foram 3 alertas novos do Dependabot no vite. Todos são do `launch-editor`, que vem dentro do vite e só roda no servidor de desenvolvimento, na função de abrir um arquivo no editor. O vite de 8.3.0 a 8.3.2 é afetado nos três, e a 8.3.3 corrige:
    - #6, GHSA-587h-7wmj-h5pm, alto (7,5): só no Windows, com execução de comando por um caminho montado de propósito;
    - #4, GHSA-698v-4r3x-3vcv, moderado (6,3): só no Windows, com um arquivo com `&` ou `%` no nome;
    - #5, GHSA-r825-w8mm-5pxw, moderado (6,3): no WSL, com o Vim, o Emacs ou o Notepad++ com plugin;
  - o servidor do Vite escuta só no próprio computador: sem `host` no `vite.config.ts` nem `--host` nos scripts, o `npm run dev` ficou só no `::1:5173`;
  - a classificação dos alertas:
    - o vite só roda no servidor de desenvolvimento. O npm o conta como de produção, porque ele é peer do `@tailwindcss/vite`, mas a imagem só leva o `web/dist`;
    - o `source-map-js` vem pelo `postcss`, pelo `css-tree` e pelo `@tailwindcss/node`, que rodam no Node, e não vai para o pacote do navegador;
    - o `npm audit` achou 10 pacotes vulneráveis, 9 altos e 1 moderado. Fora o vite e o source-map-js, os 8 só chegavam pela CLI do `shadcn`: o próprio `shadcn`, o `hono`, o `@modelcontextprotocol/sdk` e a cadeia que leva ao `braces` (`ts-morph`, `@ts-morph/common`, `fast-glob` e `micromatch`);
    - o NuGet não tinha nenhum pacote vulnerável;
  - os commits:
    - `a8d8c9f`: o lockfile como o npm 11.17.0 o escreve, com as 6 `bundleDependencies` do `@tailwindcss/oxide-wasm32-wasi` (Armadilhas conhecidas);
    - `041bad6`: o vite da 8.3.1 para a 8.3.3 (o lockfile tinha a 8.3.1, e não a 8.3.2) e o source-map-js da 1.2.1 para a 1.2.2. A 8.3.4 troca o rolldown, e a 8.3.5 tinha menos de 3 dias;
    - `4d64e35`: o CSS do shadcn em `web/src/shadcn.css` e o pacote `shadcn` fora do `package.json`. Saíram 245 entradas do lockfile, com o `hono`, o `braces` e o `@modelcontextprotocol/sdk`, e o `npm ci` no Linux foi de 673 para 428 pacotes. O build deu o mesmo `web/dist`, arquivo por arquivo;
    - `630b329`: 3 marcas `devOptional` que o `uninstall` deixou velhas, recalculadas como `dev`;
    - `ea9883c`: o roteiro "Um pacote do npm com alerta" no README e o `.github/scripts/nativos-do-lock.mjs`;
    - `436a72b`: o `front.sh` esconde o `web/dist` e o `web/coverage` (Armadilhas conhecidas);
    - `82d6b67`: a `api` de desenvolvimento, que não volta sozinha depois de atualizar o Docker Desktop, no README e nas Armadilhas conhecidas;
  - o `cn` 0.4.0 fica. Conferido por mim no npm: o mantenedor é o shadcn, o repositório é o shadcn-ui/cn, e a 0.4.0 foi publicada com proveniência. O nome era de outro pacote em 2013 e passou para o shadcn em 09/2026;
  - conferido pelo Claude:
    - o npm 11.17.0 escreve o mesmo lockfile, byte a byte, no contêiner, no contêiner com `--os=win32 --cpu=x64` e no Windows. No Windows, o `--package-lock-only` regravou o `node_modules/.package-lock.json`, com o mesmo conteúdo, e por isso o lockfile é escrito no contêiner;
    - a cada commit do lockfile, a conferência por entrada mostrou só as mudanças esperadas, e as entradas com binário nativo ficaram iguais. Das 51, só saiu o `systeminformation`, que é JavaScript;
    - o `front.sh completo`, no contêiner de 2 CPUs, passou com 260 de 260 testes depois do vite novo, depois da saída do shadcn e com as máscaras do `436a72b`. A suíte .NET, no contêiner, deu 749 de 749;
    - no Windows, o `npm.cmd install --dry-run --ignore-scripts` não mudou nada e mostrou só o vite, o source-map-js e as 245 saídas. O `npm.cmd install --ignore-scripts` fez o mesmo, sem mudar o lockfile. Depois, o lint, o `format:check`, o build e a suíte (260 de 260) passaram, e o `npm.cmd audit` deu 0;
    - a demonstração, antes do push: o `preparar-demonstracao` gravou 60 leads, e o `site.json` criou o lead 511, que foi para a Adriana, com o `lead.criado` entregue na primeira tentativa, 9,3 s depois;
  - validado por mim em 21/10/2026: o e-mail do lead 511 chegou às 12:46, no endereço da Adriana, com os dados certos e o link da ficha, que abriu no front com o vite 8.3.3. No push dos 5 primeiros commits (`ea9883c`), o `Testes` #6 ficou verde em 6 min 39 s, e o `Imagem de produção` #6, em 2 min 41 s;
  - os alertas: o lockfile da `master` não tem mais as versões afetadas, e o `npm audit` dá 0. O `braces`, com o alerta descartado em 20/10/2026, saiu do lockfile com o pacote `shadcn`. O fechamento dos 5 alertas abertos ficou pendente no GitHub:
    - às 14:01, o Dependabot continuava com os 5 abertos e o "checked yesterday", e o Dependency graph, com o vite 8.3.1, o source-map-js 1.2.1 e o hono 4.13.10;
    - o GitHub estava com um incidente desde as 11:52, com atraso na fila de eventos, e o push das 12:50 caiu dentro dele;
    - o fechamento entra no registro do próximo lote.
  - no push dos 3 últimos commits (`ea9883c..57791e5`), em 21/10/2026, o `Testes` #7 ficou verde em 6 min 56 s, e o lote fechou. Em 22/10/2026, o GitHub mostrava 7 alertas, todos de versões que não estão mais na `master`, e os 2 novos eram do `@modelcontextprotocol/sdk`, que saiu com o `shadcn`. O Dependency graph continuava no lockfile de antes: a releitura do push das 12:50 se perdeu no incidente, e o GitHub só relê o lockfile quando um push o muda. O `globals` do Lote 2A faz essa mudança.
- Lote 2A (n8n: avisos agrupados e execuções sem chave nem dado pessoal), feito de 22 a 26/10/2026:
  - antes de mexer no n8n, o volume `crm-leads_dados-n8n` foi copiado para o `crm-dados-n8n-copia`, pelo roteiro do README, e a conferência deu "OK";
  - os commits:
    - `bd0c58f`: o corpo acima de 64 KB na integração respondia 500, e não 413 (Armadilhas conhecidas). Agora responde 413, "Requisição grande demais";
    - `b8738da`: as quatro configurações em todos os fluxos, e o `CRM - Erros` sem o link, que não abriria nada. Um fluxo temporário (`rXzmedNmwLqAqu7A`, arquivado) provou que o `CRM - Erros` dispara sem a execução salva;
    - `0a17be7`: a `destinoDaFalha`, com o mesmo texto nos 4 fluxos que enviam ao CRM. O 400 e o 413 vão para a `crm_entradas_recusadas`, sem e-mail; o 408, o 429, o 5xx, o tempo esgotado e a falha de conexão, para a fila, sem e-mail; o 401, o 403, o 404 e qualquer outro código, para a fila, com um e-mail na hora. A chave errada do Google e a Meta sem campanha contam nas recusadas. Os Stop and Error saíram;
    - `87cec05`: o reenvio, com no máximo 20 linhas por rodada, para caber nos 5 minutos do Starter, as recusas fora da fila e um e-mail só por rodada na falha de configuração;
    - `280b6a3`: o `CRM - Resumo diário`, às 8h, só com números, que também apaga as recusadas com mais de 7 dias;
    - `27fde39`, pedido por mim: a entrada acima de 64 KB nem vai ao CRM nem entra na fila, e fica nas recusadas sem o corpo. As outras recusadas guardam no máximo os primeiros 8 KB, e a do 413, nada. O limite de 64 KB existe agora no CRM e no n8n;
    - `253f516`, pedido por mim: o envio ao CRM sem o Retry On Fail, que repetia o 400 (a recusa foi de 10,30 s para 0,15 s), e o reenvio com as linhas de menos tentativas primeiro. O `maxTries` e o `waitBetweenTries` ficaram no "Enviar ao CRM" dos três fluxos de entrada, sem efeito sem o `retryOnFail`;
    - `a1865f1`: o `globals` 17.13.0, pelo roteiro do README, só para o GitHub reler o lockfile (veja o Lote 5). O `npm audit` achou 4 alertas novos, de pacotes que não mudaram (Lote 5B);
  - o teste do 401 foi pulado, por decisão minha: ele cai no mesmo ramo do 404, testado no site e no reenvio com um `urlCrm` errado;
  - conferido pelo Claude no n8n local, pelos efeitos, sem ler nenhuma execução:
    - o site sem nome: 400 e a recusada, com os 390 bytes; um de 21.913 bytes ficou com 8.192, sem caractere quebrado; o de 70 KB foi recusado antes do CRM;
    - o Google sem a chave: 403, a contagem e nenhum e-mail;
    - o `urlCrm` errado (404): a fila e o e-mail na hora; no reenvio, um e-mail só para 2 linhas;
    - a API parada: a fila, sem e-mail, e o reenvio criou o lead depois;
    - 21 linhas sem nome na fila: 20 foram para as recusadas, e 1 ficou para a rodada seguinte; com 2 linhas por rodada, as sem tentativas saíram antes da que tinha 5;
    - a Meta, pelo `test_workflow`, com a Graph API falhando: a linha `meta_sem_campanha`, só com o número do lead;
    - o Resumo, à mão: o e-mail com as contagens e a linha de mais de 7 dias apagada;
  - validado por mim:
    - em 22/10/2026: os e-mails do `CRM - Erros`, sem o link, o da configuração, do site e do reenvio, o do resumo e nenhum pelo 400, pelo de 70 KB e pelo 403;
    - em 26/10/2026, o Google sem nome com a chave certa, pelo meu comando: a linha `google_ads_recusada`, sem o `google_key` no payload, 1 s depois do envio, e nenhum e-mail;
  - em 26/10/2026, o Resumo aparecia como alterado em 22/10, às 13:31: o editor do n8n, aberto por mim, salvou sozinho uma versão normalizada (Armadilhas conhecidas). O JSON do repositório foi reimportado pelo CLI, sem publicar, e os 8 fluxos bateram com os JSONs;
  - o fim do lote, conferido pelo Claude em 26/10/2026:
    - as 45 linhas de teste das recusadas saíram pelo Resumo, com o `diasRecusadas` em 0 só para isso; a fila já estava vazia;
    - a varredura, só contando: nenhuma marca (`X-Crm-Chave`, `google_key`, `X-Api-Key` e os e-mails dos exemplos) nos logs de eventos do n8n nem no log do contêiner. No banco, só as definições dos fluxos têm o `google_key` e o `X-Api-Key`: o "Conferir a chave" e uma nota;
    - o achado da varredura: as execuções da demonstração, do site e do Alertas, ficaram no banco, apagadas, com o e-mail do lead e o `X-Crm-Chave`, até a limpeza das apagadas, que as tirou 6 minutos depois. A manual do Resumo espera 1 hora (Armadilhas conhecidas). A especificação (3.8) e o README passaram a dizer isso;
    - a suíte .NET, no contêiner, com 793 de 793, e a formatação; a do front, no `front.sh completo` depois do `globals`, com 260 de 260;
    - nenhuma credencial de teste no n8n;
    - a demonstração: o `preparar-demonstracao` gravou 60 leads, e o `site.json` criou o lead 572, que foi para a Adriana, com o `lead.criado` entregue na primeira tentativa, 8,4 s depois;
  - decisão pendente, para quando o plano do n8n Cloud for escolhido: o horário do Reenvio. Hoje ele roda de hora em hora, das 8h às 20h, de segunda a sexta (`0 8-20 * * 1-5`), e cada rodada agendada conta como execução. Sem as repetições, uma queda curta do CRM no fim de semana espera a rodada de segunda às 8h;
  - o fechamento, registrado no Lote 5B:
    - validado por mim em 26/10/2026: o e-mail do lead 572 chegou às 09:00, no endereço da Adriana, com UF RS, origem Site e o link da ficha;
    - depois do e-mail, o Claude conferiu o n8n e apagou a cópia `crm-dados-n8n-copia`;
    - no push de `57791e5..fb3849f`, o `Testes` #8 ficou verde em 7 min 20 s, e o `Imagem de produção` #7, em 2 min 17 s, no `fb3849f`;
    - o GitHub releu o lockfile às 09:19, com o `globals`. Os 7 alertas antigos fecharam sozinhos, e abriram 6, os mesmos do `npm audit`: os do Lote 5B.
- Lote 5B (os alertas do npm de 26/10/2026), feito em 26/10/2026, logo depois do push do 2A:
  - os alertas, com as gravidades do GitHub:
    - altos: o #16, do `zod` 4.6.5, direto, que vai para o navegador, e o #19, do `undici` 8.11.2, que vem pelo `jsdom`;
    - moderados: o #14 e o #15, do `@vitest/mocker` 5.0.2, que vem pelo `vitest`, e o #17 e o #18, do `undici`;
    - o risco era baixo: a API é .NET e não usa o zod, e os outros só rodam nos testes;
  - os commits:
    - `487ba92`: o `zod` 4.6.6, de 11/10/2026, a corrigida mais antiga;
    - `e611fad`: o `vitest` 5.0.3, de 30/09/2026, a mais antiga com o `@vitest/mocker` 5.0.3. Junto, vieram o `@vitest/spy` 5.0.3 e o `why-is-node-running` de volta à 3.2.1, que o `vitest` 5.0.3 fixa. O `undici` foi para a 8.12.3, de 19/10/2026, a mais nova da faixa do `jsdom` (`^8.10.2`), pelo `update` do roteiro. A corrigida mais antiga era a 8.12.2, e fixá-la pediria um `overrides`;
  - o caso do `magic-string`:
    - o `install -D vitest@5.0.3` puxava o `magic-string` da 1.4.2 para a 1.4.4, publicada no mesmo dia, 26/10/2026, e a regra dos 3 dias a barra. O npm resolve de novo as dependências do pacote instalado e pega a mais nova da faixa (`^1.2.3`);
    - o `--prefer-dedupe` não segurou;
    - num ensaio numa cópia do lockfile, o `magic-string@1.4.2` instalado junto e tirado do `package.json` logo depois segurou a 1.4.2. No repositório, o resultado saiu igual ao do ensaio, byte a byte;
    - o README ganhou esse jeito, no roteiro "Um pacote do npm com alerta";
  - conferido pelo Claude, pelo roteiro, nos dois commits:
    - o `nativos-do-lock.mjs` só com as entradas esperadas, e as 50 nativas IGUAIS;
    - o `front.sh completo` com 260 de 260. O `zod.test.ts` confere que os esquemas não usam o `new Function`. No commit do `vitest`, a suíte levou 202 s, com o Chrome ocupando a máquina;
    - o dry-run no Windows só com as trocas esperadas, o install sem mudar o lockfile e, no fim, o `npm audit` em 0;
  - a suíte .NET, no contêiner, com 793 de 793;
  - a demonstração: o `preparar-demonstracao` gravou 60 leads, e o `site.json` criou o lead 633, que foi para a Adriana, com o `lead.criado` entregue na primeira tentativa, 7,8 s depois;
  - o fechamento, registrado no Lote 1C:
    - validado por mim: o e-mail do lead 633 chegou às 10:13, no endereço da Adriana. No navegador, a ficha 633 abriu certa, e o "Novo lead" em branco mostrou "Informe o nome." e "Informe o e-mail ou o telefone.";
    - no push de `fb3849f..783c002`, o `Testes` #9 ficou verde em 5 min 38 s, e o `Imagem de produção` #8, em 2 min 20 s, no `783c002`;
    - os 6 alertas fecharam com a releitura do lockfile: 0 abertos e 18 fechados.
- Lote 1C (as portas só no próprio computador), feito em 26/10/2026:
  - o motivo: o Caddy do `docker-compose.prod.yml` publicava as portas sem IP. Com o plano B ou um Compose descartável no ar, o Wi-Fi e a Radmin VPN chegavam nele, pela regra "Docker Desktop Backend" do firewall (Armadilhas conhecidas), e o 4A e o 4B sobem Compose descartáveis com o Caddy;
  - os commits:
    - `66e60db`: o teste das portas do `docker-compose.yml`, todas no `127.0.0.1`, com o `db`, a `api` e o `n8n` obrigados a ter portas, para o teste não passar vazio;
    - `24761e6`: o `PORTAS_IP` nas três portas do Caddy, vazio no servidor e `127.0.0.1` no plano B e nos Compose descartáveis. Junto, o teste de que, em produção, só o Caddy publica portas, todas pelo `PORTAS_IP`, o `.env.example`, a seção 9, o README e a armadilha;
  - conferido pelo Claude:
    - o jeito de escrever, no Compose v5.6.1, com o Engine 29.9.2: o `${PORTAS_IP:-}:` também funcionava, mas, vazio, ele vira `:80:80`, que só passa porque o parser tolera. O `${PORTAS_IP:+${PORTAS_IP}:}` dá a sintaxe documentada nos dois casos;
    - o `docker compose config`, com um `.env` fictício: sem a variável e com ela vazia, igual ao de antes, byte a byte; com o `127.0.0.1`, só os três `host_ip` a mais;
    - num Compose descartável só com o Caddy (`-p crm-portas`, portas 8098 e 8469, rede `172.31.98.0/24`), sem a variável, o `com.docker.backend` escutou no `::`, e o Caddy respondeu pelo IP do Wi-Fi e pelo da Radmin. Com o `127.0.0.1`, ele escutou só no `127.0.0.1`, em TCP e UDP: o `https://localhost:8469` chegou, e os dois IPs da rede e o `::1` recusaram. No fim, o `down -v` não deixou nada;
    - os dois testes, cada um com a porta errada de propósito (a do n8n sem o IP e a UDP do Caddy sem o prefixo), falharam só nela;
    - a suíte .NET, no contêiner, com 795 de 795, e a formatação; a do front, no `front.sh completo`, com 260 de 260;
    - durante a suíte .NET, o PostgreSQL dos testes, o Ryuk e o Caddy do `LogDoCaddyTests` ficaram publicados em `0.0.0.0` e `[::]`, e o `com.docker.backend` escutou no `::` (Armadilhas conhecidas);
    - a demonstração: o `preparar-demonstracao` gravou 60 leads, e o `site.json` criou o lead 694, que foi para a Adriana, com o `lead.criado` entregue na primeira tentativa, 8,5 s depois;
  - o meu `PORTAS_IP` no `../crm-teste.env`, o plano B com a variável, o e-mail do lead 694, o push e o CI entram no registro do próximo lote.
- O e-mail do lead com acento, como `testenotificação@exemplo.com`, achado na validação de 13/10/2026, foi corrigido no mesmo dia.
  - A API (`Emails.Formato`) e a tela (`FORMATO_EMAIL`, em `web/src/features/leads/esquema.ts`) conferem o mesmo formato: antes do @, só ASCII (letras, números e os símbolos do RFC 5322, com pontos só entre eles); o domínio, em rótulos de letras, números e hífen.
  - Antes, os dois só pediam algo@algo.algo sem espaços.
  - Os casos de teste são os mesmos nos dois lados (`EmailsUfsTextosTests` e `esquema.test.ts`).
  - Na tela, o e-mail com acento dá "E-mail inválido.". A integração e a importação continuam aceitando, com "E-mail em formato inválido" na ficha.
  - A deduplicação dos e-mails válidos não muda.
  - Um lead já salvo com um e-mail assim passa a mostrar o aviso, perde o botão E-mail e deixa de contar na busca por pessoa.
  - Os 60 e-mails da massa da demonstração continuam válidos (`MassaDemonstracaoTests`), sem o aviso.
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
- Web: React + TypeScript + Vite, React Router, TanStack Query, React Hook Form + Zod, Tailwind + shadcn/ui, dnd-kit, @microsoft/signalr. Os gráficos do painel são barras em HTML e CSS, sem biblioteca; o Recharts só entra quando um gráfico precisar dele, como uma linha por dia. O PWA é feito à mão, sem o vite-plugin-pwa: o manifesto, os ícones e o service worker ficam em `web/public`, e o service worker não guarda cache
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
- No celular (abaixo do `md`), o botão, o campo e a seleção têm 40 px de altura (`h-10 md:h-8` no `Button`, no `Input` e na `Selecao`). Não passe uma altura própria a eles sem conferir as duas larguras.
- Para revisar as telas no celular ou seguir um roteiro pela tela, use o Playwright num contêiner Linux, nada no Windows:
  - a imagem é a `mcr.microsoft.com/playwright`, com o pacote `playwright` da mesma versão instalado num volume do Docker;
  - o contêiner fica ligado à rede da API de um Compose de produção descartável (`--network container:<api>`, `http://localhost:8080`, onde o cookie `Secure` vale) ou à do Caddy (`https://localhost`, com `ignoreHTTPSErrors`), para a CSP;
  - a faixa da rede do Compose descartável não pode colidir com a do `crm-local` (`172.30.0.0/24`): use `REDE_SUBNET` e `CADDY_IP`;
  - a notificação só funciona com o Chromium completo (`chromium.launch({ channel: 'chromium' })`), porque o "headless shell" padrão nega a permissão;
  - depois de um clique num link `tel:` ou `mailto:`, o Chromium sem janela deixa a página sem receber os cliques seguintes. Nos roteiros, impeça a navegação do link (`addInitScript`), como o `naoNavegar` dos testes do front;
  - o arraste pelo teclado com as setas apertadas sem pausa anda menos colunas: use pausas ou o mouse.
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
  - chamadas na mesma mensagem rodam juntas: insira a linha na Data Table e só depois, em outra mensagem, execute o fluxo que a lê.
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
- Para encurtar nos testes do front um intervalo que espera o relógio de verdade, como o da consulta de andamento da importação, ponha a constante num módulo próprio e troque o valor por `vi.mock`, sem relógio falso (`importacao/andamento.ts`).
- Toda imagem do `Dockerfile` e dos Compose tem versão exata, e o `ImagensTests` cobra o formato de cada uma: uma imagem nova precisa entrar nele. O SDK do .NET aparece em quatro lugares e sobe junto nos quatro, como no PR mensal do Dependabot.
- Um `docker compose -p <descartável> -f docker-compose.prod.yml up --build` troca a imagem `crm-api`, a mesma do plano B. Para não mexer nela, dê outro nome à imagem por um override (`image:` dos serviços `api` e `migracoes`).
- O Docker Desktop cria no firewall do Windows a regra de entrada "Docker Desktop Backend", em TCP e UDP, que libera o `com.docker.backend` no perfil Público, em qualquer porta e de qualquer origem. As duas redes desta máquina, o Wi-Fi e a Radmin VPN, estão nesse perfil. Uma porta publicada sem IP fica aberta para elas enquanto o contêiner está no ar: o `com.docker.backend` escuta no `::`, que aceita também o IPv4.
  - Toda porta do `docker-compose.yml` fica no `127.0.0.1`, e o `PublicacaoTests` cobra isso.
  - O Compose de produção no próprio computador sobe com o `PORTAS_IP=127.0.0.1`: o plano B, pelo `../crm-teste.env`, e todo Compose descartável. No servidor, ele fica vazio, e o Docker publica a 80 e a 443 no IPv4 e no IPv6; um `0.0.0.0` deixaria o IPv6 de fora.
  - Com o `127.0.0.1`, o `localhost` chega pelo IPv4: o `::1` recusa, e o navegador e o `curl` passam para o `127.0.0.1`. Pelo IP (`https://127.0.0.1:8443`), o TLS falha, porque o certificado do Caddy é do nome `localhost`.
  - O Testcontainers publica sem IP. Enquanto a suíte .NET roda, a rede chega na porta do PostgreSQL dos testes, com a senha padrão `postgres`, na do Ryuk, que apaga os contêineres dos rótulos que recebe por ela, e na do Caddy do `LogDoCaddyTests`. Os dados são fictícios. Prender essas portas no `127.0.0.1` pelo código não serve: no CI, os testes chegam ao PostgreSQL pelo `host.docker.internal`, que no Linux é o gateway da rede.
- Os workflows do CI só leem o repositório, e o `WorkflowsTests` cobra isso em todo arquivo de `.github/workflows`:
  - a permissão `contents: read`;
  - toda ação de terceiros fixada pelo SHA, com a versão num comentário, que o Dependabot atualiza;
  - todo checkout com `persist-credentials: false`;
  - nada de `secrets`, `github.token` ou `pull_request_target`.
- No GitHub Free, repositório privado não tem proteção de branch: o check vermelho não impede o merge. A conferência de um PR é manual (README › Versões fixadas).
- O `front.sh` monta o `web/` só para leitura, com o `node_modules`, o `dist` e o `coverage` em volumes anônimos, e o build sai em `/tmp/dist`, dentro do contêiner. O Docker não cria um ponto de montagem dentro de uma montagem só de leitura: por isso o script cria as três pastas antes, o que no runner são pastas vazias.
  - O `dist` e o `coverage` ficam escondidos desde 21/10/2026 (`436a72b`). Só com o `web/` montado, sem o `.git` e o `.gitignore` da raiz, o Tailwind procura classes também no `web/dist`. Com o build velho do Windows na pasta, o CSS do build no contêiner saía com 3,5 KB a mais; com os volumes, o build no contêiner é igual ao do Windows, arquivo por arquivo.
  - A imagem de produção (o `.dockerignore` tira o `web/dist` e o `web/coverage`) e o CI (um checkout novo) não têm isso. Um contêiner novo que monte só o `web/` precisa do mesmo cuidado.
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
- Em 13/10/2026, o Passo 11 foi adiantado e feito. Fica para a terceira fase só o que depende do servidor, como o celular de verdade, e para o Lote 4B, a divisão do pacote por tela. As etapas definitivas esperam a Gama.
- Em 14/10/2026, o Passo 6 foi adiantado no n8n local, com a tela de campanhas. Fica para a terceira fase o que depende do n8n Cloud, do app da Meta, do Google Ads e da Gama.
- Em 16/10/2026, com a reunião sem data, o trabalho passou a seguir um plano de lotes, aprovado em 19/10/2026, com tudo o que não depende da empresa:
  - 1: infraestrutura (as imagens com versão fixa e o Dependabot), fechado em 19/10/2026;
  - a causa do teste instável do front, trazida do 4A em 19/10/2026 e fechada em 20/10/2026;
  - 1B: CI no GitHub Actions, fechado em 21/10/2026;
  - 5: dependências, antecipado para antes do 2A por causa de 3 alertas do vite e feito em 21/10/2026;
  - 2A: n8n (avisos agrupados e execuções sem chave nem dado pessoal), feito em 26/10/2026;
  - 5B: os alertas do npm de 26/10/2026 (o `zod`, o `undici` e o `@vitest/mocker`), logo depois do push do 2A, feito em 26/10/2026;
  - 1C: as portas só no próprio computador, decidido em 21/10/2026 para logo depois do 2A e antes do 2B, porque o 4A e o 4B sobem Compose descartáveis com o Caddy, feito em 26/10/2026;
  - 2B: n8n (o limite do site);
  - 3: troca de chaves sem parar;
  - 4A e 4B: front (celular, a Caddy 2.11.7 e as rotas sob demanda);
  - 6: documentação.
  - Cada lote só começa com o meu ok e fecha com a demonstração conferida.
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
