# CRM de Leads

CRM interno que recebe os leads das campanhas da Gama Marketing (Facebook, Instagram e Google), distribui entre as atendentes e mede quantos viram clientes em cada campanha.

- Regras, contrato com o n8n, modelo de dados e passos de entrega: [docs/especificacao.md](docs/especificacao.md)
- Como desenvolver com o Claude Code: [docs/guia-claude-code.md](docs/guia-claude-code.md)
- Guia de uma página para as atendentes: [docs/guia-das-atendentes.md](docs/guia-das-atendentes.md)
- Operação: [Publicação](#publicação) (com backup, restauração e troca de chaves e senhas), [Plano B da demonstração](#plano-b-o-compose-de-produção-no-próprio-computador) e [Pedido do titular (LGPD)](#pedido-do-titular-lgpd)

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
   cr=$(printf '\r')
   sed -b -i -e "s/^Admin__Email=[^$cr]*\($cr*\)\$/Admin__Email=voce@exemplo.com\1/" \
             -e "s/^Admin__SenhaInicial=[^$cr]*\($cr*\)\$/Admin__SenhaInicial=$senha\1/" .env
   echo "Senha inicial do admin: $senha"
   ```

   Nos comandos do Bash, o `sed -b` e o `cr` mantêm o fim de cada linha do `.env`, LF ou CRLF, e só o valor muda. Sem o `-b`, o `sed` do Git Bash tira o CR de todas as linhas; com o `-b` e o `.*`, ele levaria o CR da linha trocada.

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
   cr=$(printf '\r')
   sed -b -i "s/^Integracao__Chave=[^$cr]*\($cr*\)\$/Integracao__Chave=$(openssl rand -hex 24)\1/" .env
   ```

   A mesma chave vai para a credencial Header Auth do n8n no Passo 6. Para trocá-la, rode o comando de novo e atualize a credencial.

4. Alertas por e-mail (pode ficar para depois). O CRM avisa o fluxo `CRM - Alertas` do n8n, que manda o e-mail. Em desenvolvimento, o n8n roda no próprio Compose (veja "n8n local", em Dia a dia). Primeiro, gere a chave que o n8n confere no cabeçalho `X-Crm-Chave` (`N8n__WebhookChave`). Os comandos a gravam no `.env` e a deixam pronta para colar no n8n.

   PowerShell (a chave vai para a área de transferência):

   ```powershell
   $bytes = New-Object byte[] 24; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
   $chave = -join ($bytes | ForEach-Object { $_.ToString('x2') })
   $linhas = (Get-Content .env -Encoding UTF8) -replace '^N8n__WebhookChave=.*$', "N8n__WebhookChave=$chave"
   [IO.File]::WriteAllText("$PWD\.env", ($linhas -join "`n") + "`n")
   Set-Clipboard -Value $chave
   ```

   Bash (a chave aparece no terminal):

   ```bash
   chave=$(openssl rand -hex 24)
   cr=$(printf '\r')
   sed -b -i "s/^N8n__WebhookChave=[^$cr]*\($cr*\)\$/N8n__WebhookChave=$chave\1/" .env
   echo "$chave"
   ```

   No n8n, a chave vai na credencial Header Auth `CRM - Chave dos eventos`, com o nome do cabeçalho `X-Crm-Chave` (veja "n8n local"). Com o fluxo `CRM - Alertas` publicado, preencha no `.env` a URL de produção do Webhook dele. Com a API no contêiner, o n8n do Compose é `n8n:5678`; com o `dotnet run` no Windows, `localhost:5679`:

   ```bash
   N8n__WebhookUrl=http://n8n:5678/webhook/crm-alertas
   ```

   Sem `N8n__WebhookUrl`, o CRM não envia nada, e os eventos ficam pendentes. Quando a URL for configurada, os pendentes acumulados saem todos de uma vez, e cada um vira um e-mail. Para conferir e descartá-los antes, em desenvolvimento (só a fila de eventos, sem mexer em lead):

   ```bash
   docker compose exec db psql -U crm -d crm -c "SELECT situacao, count(*) FROM eventos_saida GROUP BY situacao;"
   docker compose exec db psql -U crm -d crm -c "DELETE FROM eventos_saida WHERE situacao = 'pendente';"
   ```

   A API não mostra o `N8n__WebhookUrl` no `printenv` do contêiner, porque ela mesma lê o `.env`. Para saber se ela leu a URL, procure no log da subida a linha "N8n__WebhookUrl não está configurada": ela só aparece quando a URL está vazia.

5. Instale as ferramentas e as dependências:

   ```bash
   dotnet tool restore      # dotnet ef, para criar migrations (num Windows que bloqueia as DLLs, use o serviço ef)
   cd web && npm ci
   ```

6. Num Windows com o Docker Desktop, feche para a rede as portas que ele publica. O Docker Desktop cria no firewall a regra "Docker Desktop Backend", que libera a entrada no perfil Público em qualquer porta. Assim, uma porta publicada sem IP, como as do Testcontainers durante a suíte .NET, fica aberta para o Wi-Fi enquanto o contêiner está no ar. A regra abaixo bloqueia a entrada pela rede em todas essas portas, em qualquer perfil, e vence a do Docker; o próprio computador continua chegando nelas. Num PowerShell de administrador:

   ```powershell
   New-NetFirewallRule -DisplayName 'Docker Desktop: sem acesso pela rede' -Description 'CRM, Lote 1C (26/10/2026): bloqueia a entrada pela rede em todas as portas que o Docker Desktop publica, em qualquer perfil. Só o próprio computador chega nelas. Vence a regra Docker Desktop Backend, que libera a entrada no perfil Público.' -Direction Inbound -Action Block -Profile Any -Program 'C:\Program Files\Docker\Docker\resources\com.docker.backend.exe'
   ```

   - Depois de cada atualização do Docker Desktop, confira que ela continua ativa: `Get-NetFirewallRule -DisplayName 'Docker Desktop: sem acesso pela rede'`.
   - Para desfazer: `Remove-NetFirewallRule -DisplayName 'Docker Desktop: sem acesso pela rede'`.
   - Só outro aparelho, como um celular no mesmo Wi-Fi, prova que ela funciona: o Windows não filtra o que o computador manda para ele mesmo.

## Dia a dia

Num Windows com o Controle Inteligente de Aplicativos ligado, como o deste projeto, a API e o `dotnet ef` rodam num contêiner Linux, e o front continua no Windows (veja "API e dotnet ef num contêiner Linux", logo abaixo). No PowerShell 5.1, use `npm.cmd` e rode os comandos em linhas separadas:

```powershell
docker compose up -d api                 # PostgreSQL 17 e a API em http://localhost:5080
cd web
npm.cmd run dev                          # front em http://localhost:5173
```

Onde o Windows deixa carregar as DLLs do projeto, a API também roda direto:

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
| http://localhost:5679 | n8n local, com os fluxos de [n8n/](n8n/) (`docker compose up -d n8n`; veja "n8n local") |

Em desenvolvimento, a API lê o `.env` da raiz. Uma variável que já exista no ambiente tem prioridade sobre o arquivo. As migrations são aplicadas quando a API sobe, só em desenvolvimento. O arquivo [src/Crm.Api/Crm.Api.http](src/Crm.Api/Crm.Api.http) tem requisições prontas para testar a API pelo VS Code.

Com o `N8n__WebhookUrl` preenchido, a API manda ao n8n os eventos pendentes assim que sobe. Para subir sem mandar nada (para gerar os tipos do front, por exemplo), pelo contêiner (veja abaixo) ou, onde ele funciona, pelo `dotnet run`:

```bash
dotnet run --project src/Crm.Api -- --N8n:WebhookUrl=
```

### API e dotnet ef num contêiner Linux

Com o Controle Inteligente de Aplicativos do Windows ligado, o `dotnet run` e o `dotnet ef` falham quando precisam carregar uma DLL que não é assinada, como a `EFCore.NamingConventions.dll`, com "Uma política de Controle de Aplicativo bloqueou este arquivo". O [docker-compose.yml](docker-compose.yml) tem a API de desenvolvimento e o `dotnet ef` num contêiner Linux, sem mexer na segurança do Windows. O front continua no Windows (`npm run dev`), e o proxy do Vite chega à API no mesmo endereço, `http://localhost:5080`.

```powershell
docker compose up -d api          # compila e sobe a API (o banco sobe junto)
docker compose logs -f api        # acompanha o log; o Ctrl+C sai do log, e a API continua no ar
docker compose restart api        # depois de mudar o código da API: recompila e sobe de novo
docker compose stop api           # para a API
```

- A configuração é a mesma do `dotnet run`: a API lê o mesmo `.env` e o `appsettings.Development.json`. O Compose só troca a conexão com o banco, que vai pelo nome do serviço (`db`) com a senha do `POSTGRES_PASSWORD`, e guarda as chaves da sessão num volume, para o login continuar valendo quando o contêiner é recriado.
- O código entra só para leitura, e o build fica num volume do Docker: as pastas `bin/` e `obj/` do Windows não mudam.
- A primeira subida baixa a imagem do SDK e os pacotes, o que leva alguns minutos. Nas seguintes, o build só recompila o que mudou.
- Depois de mudar o `.env`, use o `restart`: a API lê o arquivo quando sobe. Depois de mudar o `docker-compose.yml`, use `docker compose up -d api`, que recria o contêiner com a configuração nova.
- Depois de atualizar ou reiniciar o Docker Desktop, o banco e o n8n voltam sozinhos (`restart: unless-stopped`), e a API não: rode `docker compose up -d api`. Ela fica sem `restart` de propósito. Com o código sem compilar, o Docker a subiria de novo em laço, recompilando a cada uns 30 s com quase 2 CPUs, sem ninguém ver.
- O log mostra a hora de Brasília, como no Windows.
- Na auditoria, o IP de quem acessa aparece como o do Docker (o gateway da rede do Compose, como `172.18.0.1`), e não como `127.0.0.1`.
- O que vier depois do nome do serviço num `docker compose run` vai para a API, como o `preparar-demonstracao` (veja "Demonstração"). Pare o `api` antes, porque os dois compilam no mesmo volume e usam a porta 5080. Para subir sem mandar eventos ao n8n:

  ```powershell
  docker compose stop api
  docker compose run --rm --service-ports api --N8n:WebhookUrl=
  ```

  Para voltar à de sempre, pare essa com o Ctrl+C e rode `docker compose start api`.

O `dotnet ef` roda pelo serviço `ef`, na pasta da API e com a mesma configuração dela. Só a pasta das migrations aceita escrita:

```powershell
docker compose run --rm ef migrations add <NomeDescritivo>
docker compose run --rm ef migrations list
```

### Primeiro acesso

1. Abra http://localhost:5173 e entre com o `Admin__Email` e a senha inicial que o comando da primeira vez mostrou.
2. O sistema pede uma senha nova. Depois disso, a senha do `.env` não vale mais.
3. Em Configurações › Usuários, crie as atendentes. Para testar, use aliases do seu Gmail, como `matheusbordinhao11+adriana@gmail.com`. A tela gera a senha provisória para você copiar e enviar.

Se o banco não tiver nenhum admin (por exemplo, depois de `docker compose down -v`), a API só sobe com `Admin__Email` e `Admin__SenhaInicial` preenchidos no `.env`: rode de novo o passo 2 da primeira vez. Sem eles, a API para com a mensagem "Nenhum admin cadastrado".

Depois de 5 senhas erradas, o login fica bloqueado por 15 minutos. Um admin libera antes redefinindo a senha na tela de usuários.

O cookie da sessão é `Secure`. Chrome, Edge e Firefox o aceitam em `http://localhost`, mas não pelo IP da máquina: para abrir no celular, é preciso HTTPS, que vem com a [publicação](#publicação). Em desenvolvimento, confira as telas de celular com a visão de dispositivo do DevTools.

### Funil e ficha

O funil é a tela inicial da atendente: uma coluna por etapa aberta, e Ganho e Perdido como áreas de soltar, com quantos leads foram para lá nos últimos 30 dias.

- Arraste o cartão com o mouse, ou pelo teclado: foque a alça do cartão (⋮⋮), aperte espaço, escolha a etapa com as setas para a direita e para a esquerda e aperte espaço de novo. Esc cancela.
- Soltar em Perdido pede o motivo; cancelar devolve o cartão. Ganho exige CPF/CNPJ válido na ficha, a não ser que o admin desligue a exigência.
- No celular (ou na visão de dispositivo do DevTools), o funil vira uma lista por etapa, com um "Mover para" em cada cartão.
- Na ficha, os botões WhatsApp, Ligar e E-mail abrem o contato e, em seguida, o registro da interação com o tipo já escolhido. A primeira ligação, WhatsApp, e-mail ou reunião grava o primeiro contato, e a linha do tempo mostra quem fez o quê e quando.
- As etapas, os motivos de perda e a exigência de CPF/CNPJ para Ganho ficam em Configurações › Etapas e motivos (só admin).
- O admin passa o lead para outra pessoa no cartão Responsável da ficha. A troca aparece na linha do tempo e avisa a nova responsável.
- O quadro, a lista e a ficha se atualizam sozinhos quando um lead chega ou muda, sem recarregar a página. Durante o arraste, o quadro espera você soltar o cartão. Se outra pessoa mudar os dados do lead enquanto você edita a ficha, o que você digitou continua lá, com o aviso "Outra pessoa alterou os dados deste lead".

### Painel

O painel é a tela inicial do admin. A atendente o abre pelo menu e vê só os próprios números. As definições estão na seção 8 da especificação.

- Os filtros ficam no endereço:
  - o período, que pode ser os últimos 7, 30 ou 90 dias, este mês, o mês passado ou um personalizado, em dias do horário de Brasília (sem filtro, valem os últimos 30 dias, com hoje);
  - a origem e a campanha;
  - para o admin, a responsável.
- Leads novos são os criados no período. A situação atual, a conversão, o primeiro contato e as quebras por origem, campanha e atendente contam só esses leads. A origem e a campanha são as da primeira entrada, e a responsável é a atual.
- As entradas contam também as de quem já era lead, cada uma pela própria origem e campanha.
- Os motivos de perda contam os leads que foram para Perdido no período, mesmo os que entraram antes ou foram reabertos depois.
- Os leads de teste ficam de fora de tudo, e os anonimizados continuam contando.
- O painel se atualiza sozinho quando um lead muda, como o funil.

### Relatório por campanha

Em Relatório (só admin) fica o relatório que a Gama recebe, sem dados pessoais. As definições estão na seção 8 da especificação.

- Cada campanha tem uma linha com:
  - os leads novos e as entradas;
  - quantos estão em andamento, ganhos e perdidos, ou seja, onde os leads novos estão hoje (os três somam os leads novos);
  - a conversão e a mediana do primeiro contato.
- Os leads sem campanha (cadastro manual, site, indicação e outros) ficam no fim, numa linha por origem. A última linha é o total.
- Os números são os do painel no mesmo período: cada campanha bate com o painel filtrado por ela, e o total, com o painel sem filtros.
- O período funciona como no painel e fica no endereço. O botão Baixar CSV baixa o mesmo relatório, no período da tela, pronto para o Excel: ponto e vírgula, acentos certos e a conversão como porcentagem.
- Uma campanha cujo nome começa com `=`, `+`, `-` ou `@` sai no CSV com um apóstrofo na frente, que o Excel mostra na célula (`'+Leads outubro`). Sem ele, o Excel leria o nome como fórmula e mostraria `#NOME?`.
- A tela se atualiza sozinha quando um lead muda, como o painel.
- O n8n busca o mesmo relatório com a chave de integração, em `GET /api/integracoes/relatorios/campanhas` (JSON) ou `.../csv`.
  - Sem datas, vem a semana anterior inteira, de segunda a domingo.
  - É o que o fluxo `CRM - Relatório semanal Gama` manda toda segunda às 8h, com o CSV anexo. No n8n local, vai para o e-mail do admin, até a Gama indicar o dela.
  - Para testar pela API, use as requisições do relatório no [src/Crm.Api/Crm.Api.http](src/Crm.Api/Crm.Api.http).

### Campanhas

Em Configurações › Campanhas (só admin) ficam as campanhas dos anúncios, criadas quando chega o primeiro lead de cada uma.

- O Google manda só o número da campanha, que chega com um nome provisório, como "Google Ads · campanha 123". Essas vêm primeiro, com a marca "Nome provisório", a origem e o id na origem.
- O lápis renomeia qualquer campanha. O nome novo aparece no painel, no relatório e nas fichas, e um nome que chegue depois, pela integração ou pela importação, não o substitui.
- A renomeação entra na auditoria.

### Rodízio, notificações e alertas

- Em Configurações › Usuários, o cartão Rodízio liga e desliga a distribuição automática e mostra para quem vai o próximo lead. A coluna Rodízio da lista escolhe quem participa: nas férias ou folgas, desligue a da pessoa.
- Os leads da integração, e os que um admin cadastra com "Rodízio" no campo Responsável, vão um de cada vez para quem está no rodízio. Só o rodízio passa a vez: a escolha do admin, a troca de responsável e o lead que a atendente cadastra para si não mexem na ordem.
- Sem ninguém no rodízio, ou com a distribuição desligada, o lead fica sem responsável, e os admins recebem um aviso no sino, com o motivo.
- O sino no topo mostra as notificações e se atualiza na hora, pelo tempo real (o hub `/hubs/notificacoes` do SignalR). Quem fez a ação não é avisado dela, e lead de teste não gera aviso.
- Com a aba em segundo plano, o navegador mostra a notificação nativa, só com o título; o clique abre a ficha. Depois do login, um aviso pergunta uma vez se a pessoa quer ativá-la; quem respondeu "Agora não" ativa pelo sino.
  - No build de produção, ela sai pelo service worker do PWA, que também a leva ao celular (veja "Instalar como app"). Em desenvolvimento, sai pelo `new Notification`, só no computador.
  - Ela chega enquanto o CRM está aberto, mesmo em segundo plano. Com ele fechado, não chega: o push com o app fechado está fora da primeira versão.
- Os e-mails saem pelo n8n, em segundo plano. Se o n8n estiver fora do ar, o CRM tenta de novo depois de 1 min, 5 min, 30 min, 2 h, 6 h e 12 h; depois disso, os admins recebem um aviso no sino.

### Instalar como app (PWA)

O CRM se instala como um app, com o nome "CRM de Leads" e o ícone da marca, numa janela própria.

- **No computador:** no Chrome, pelo menu ⋮ › Transmitir, salvar e compartilhar › o item de instalar (o nome dele muda com a versão do Chrome), ou pelo ícone de instalar na barra de endereço, quando aparecer. No Edge, pelo menu … › Aplicativos › Instalar este site como um aplicativo. Funciona no `http://localhost:5173` e, depois da publicação, no domínio.
- **No celular, só depois da publicação**, porque precisa de HTTPS: no Chrome do Android, pelo menu ⋮ › Instalar app (ou Adicionar à tela inicial); no Safari do iPhone, por Compartilhar › Adicionar à Tela de Início. No iPhone, a notificação nativa só funciona no app instalado.
- **Service worker** (`web/public/sw.js`):
  - só existe no build de produção;
  - não tem evento `fetch` nem cache: não intercepta a API, o tempo real nem os arquivos da tela, e nunca serve uma versão velha;
  - serve para a instalação e para a notificação nativa, que no celular só sai por ele;
  - não existe no `localhost:5173` nem no Compose de produção no próprio computador (`https://localhost`), cujo certificado local o Chrome recusa para o service worker. Nos dois, a notificação sai pelo `new Notification`, como antes.
- **Para testar o service worker no computador**, com a API no ar: em `web/`, `npm.cmd run build` e `npm.cmd run preview`, e abra `http://localhost:4173`, que repassa `/api` e `/hubs` para a API, como o `npm run dev`. Ele fica registrado só no `localhost:4173`; para tirá-lo, use DevTools › Application › Service workers › Unregister.
- **Ícones:** os PNGs de `web/public` saem dos SVGs (o `favicon.svg` e os de `web/icones`) por um script que roda num contêiner Linux, sem instalar nada no Windows. Rode de novo quando o logo da empresa chegar:

  ```powershell
  docker run --rm -v "${PWD}/web:/web" -w /web alpine:3.22 sh icones/gerar.sh
  ```

### Leads pela integração

O n8n envia os leads para `POST /api/integracoes/leads` (seção 4.2 da especificação). Para simular um envio sem o n8n, com a API rodando, use a requisição "Lead pela integração" do [src/Crm.Api/Crm.Api.http](src/Crm.Api/Crm.Api.http) e informe o valor de `Integracao__Chave` quando o VS Code pedir.

- Reenviar a mesma requisição responde `ja_recebido` e não duplica o lead. Troque o `idExterno` para simular um lead novo.
- O exemplo vai com `"teste": true`: lead de teste fica sem responsável, só aparece para admins e não gera aviso. Para ver o rodízio e o alerta por e-mail, use a requisição "Lead real pela integração", que gera o `idExterno` e o e-mail a cada envio: cada clique é um lead novo.
- Os leads da integração vão para o rodízio e aparecem na hora no funil e no sino de quem os recebe, e no painel. Os que ficam sem responsável aparecem em Leads, com o filtro Responsável › Sem responsável.
- Telefone, e-mail ou CPF/CNPJ em formato inválido não recusam o lead: a ficha mostra o aviso embaixo do campo.

### n8n local

O [docker-compose.yml](docker-compose.yml) tem um n8n Community (2.43.7) só para desenvolvimento, para montar e testar os fluxos de [n8n/](n8n/) com dados de teste até o n8n Cloud de produção (veja "n8n Cloud: importar os fluxos", em Publicação).

```powershell
docker compose up -d n8n         # http://localhost:5679
docker compose stop n8n
```

- A interface fica em `http://localhost:5679`, só no próprio computador. A 5678, a padrão, é de outro n8n desta máquina; dentro da rede do Compose, ele continua em `http://n8n:5678`.
- Os fluxos chamam a API em `http://api:5080`, o `urlCrm` do nó "Configuração" de cada um. Com a API no `dotnet run` do Windows, troque por `http://host.docker.internal:5080`.
- A conta de dono, as credenciais e as Data Tables ficam no volume `dados-n8n`.
- A API não depende dele. Com o n8n parado, o CRM funciona, e os eventos ficam tentando de novo; depois de cerca de 20 horas, falham, com o aviso no sino. Se ele vai ficar parado por muito tempo, esvazie o `N8n__WebhookUrl`.

**Primeira vez.**

1. Abra `http://localhost:5679` e crie a conta de dono.
2. Crie as credenciais (Credentials › Add), com estes nomes:

   | Credencial | Tipo | Valor |
   | --- | --- | --- |
   | `CRM - Chave de integração` | Header Auth, nome `X-Api-Key` | o `Integracao__Chave` do `.env` |
   | `CRM - Chave dos eventos` | Header Auth, nome `X-Crm-Chave` | o `N8n__WebhookChave` do `.env` |
   | `CRM - E-mail` | SMTP: `smtp.gmail.com`, porta 465, SSL/TLS | a conta que envia (em desenvolvimento, `testecrmfs@gmail.com`) e a senha de app dela |
   | `CRM - Meta Lead Ads` | Facebook Lead Ads OAuth2 | só com o app da Meta em modo Live |

   Para levar um valor do `.env` para a área de transferência, sem mostrá-lo:

   ```powershell
   function Copiar-DoEnv($nome) { $l = Select-String -Path .env -Pattern "^$nome=" | Select-Object -First 1; ($l.Line -replace "^$nome=", '' -replace '\s#.*$', '').Trim().Trim('"', "'") | Set-Clipboard }
   Copiar-DoEnv Integracao__Chave
   ```
3. Crie as Data Tables (Overview › Data tables), que os fluxos acham pelo nome:
   - `crm_entradas_pendentes`, com as colunas `payload` (texto), `tentativas` (número), `ultimo_erro` (texto) e `criado_em` (data);
   - `crm_entradas_recusadas`, com as colunas `tipo`, `payload` e `detalhe` (texto) e `criado_em` (data);
   - `crm_envios_site`, só com a coluna `criado_em` (data).
4. Importe os fluxos (Workflows › Import from File), o `crm-erros.json` primeiro. Em cada um, confira a credencial dos nós e, em Settings, o Error Workflow (`CRM - Erros`) e as quatro opções de salvar em "Do not save": "Save failed production executions", "Save successful production executions", "Save manual executions" e "Save execution progress". O `CRM - Erros` precisa estar publicado antes de virar o fluxo de erro dos outros.
5. Gere a chave do Google de desenvolvimento. Ela é aleatória e fica num arquivo fora do repositório; o SHA-256 dela vai para o `googleKeySha256` do nó "Configuração" do `CRM - Entrada Google`:

   ```powershell
   $bytes = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
   $chave = -join ($bytes | ForEach-Object { $_.ToString('x2') })
   [IO.File]::WriteAllText("$PWD\..\crm-google-chave-dev.txt", $chave)
   $hash = -join ([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($chave)) | ForEach-Object { $_.ToString('x2') })
   "SHA-256 da chave do Google: $hash"
   Remove-Variable bytes, chave
   ```

**MCP da instância.** É por ele que o Claude Code cria e confere os fluxos.

- Em Settings › Instance-level MCP, ligue o acesso e a exposição automática dos fluxos novos.
- Em Connect a client › API key, copie o token e rode, na pasta do projeto:

  ```powershell
  $claude = (Get-ChildItem "$env:USERPROFILE\.vscode\extensions\anthropic.claude-code-*\resources\native-binary\claude.exe" | Sort-Object FullName | Select-Object -Last 1).FullName
  $token = Get-Clipboard
  & $claude mcp add --transport http --scope user n8n-local http://localhost:5679/mcp-server/http --header "Authorization: Bearer $token"
  Remove-Variable token
  ```

- A configuração fica no `~/.claude.json`, fora do Git. É no escopo `user` porque o escopo `local` não aparece na extensão do VS Code.
- O servidor `n8n` do `.mcp.json` é o do n8n Cloud de teste, que acabou, e aparece como Failed até o n8n de produção existir.

**Os fluxos.** Os exemplos de [n8n/exemplos/](n8n/exemplos/) têm dados fictícios.

| Fluxo | Gatilho no n8n local | Publicado | Como testar |
| --- | --- | --- | --- |
| `CRM - Erros` | Error Trigger | Sim | Qualquer falha dos outros fluxos manda o e-mail ao admin |
| `CRM - Alertas` | `POST /webhook/crm-alertas`, com o `X-Crm-Chave` | Sim | Um lead pelo rodízio: o e-mail chega no alias da atendente |
| `CRM - Entrada Site` | `POST /webhook/crm-entrada-site` | Sim | `site.json` cria um lead real; `site-robo.json` recebe `{"message":"Webhook call received"}`, sem criar execução; `site-sem-nome.json` vai para as recusadas |
| `CRM - Entrada Google` | `POST /webhook/crm-entrada-google` | Sim | `google-teste.json`, com a chave do arquivo: lead de teste; `google-sem-nome.json`, com a chave: vai para as recusadas |
| `CRM - Reenvio de pendentes` | De hora em hora, das 8h às 20h, de segunda a sexta | Não | Execução manual, depois de um envio com a API parada |
| `CRM - Relatório semanal Gama` | Segunda às 8h | Não | Execução manual: o CSV da semana anterior vai para o `emailGama` |
| `CRM - Resumo diário` | Todo dia às 8h | Não | Execução manual: o e-mail com as contagens do dia anterior vai para o `emailAdmin` se houver algo a contar e, no dia do `diaDoResumoSemanal` (1 é segunda), sempre; as recusadas com mais de 7 dias e os envios do site com mais de 31 saem |
| `CRM - Entrada Meta` | Gatilho de Lead Ads | Não, sem o app Live | `test_workflow` do MCP, com `meta-gatilho.json` e `meta-graph.json` |

```powershell
# Site: responde {"recebido":true}, e o lead vai para o rodízio, com o alerta
curl.exe -s -H "Content-Type: application/json" --data-binary "@n8n/exemplos/site.json" http://localhost:5679/webhook/crm-entrada-site

# Google: a chave sai do arquivo e não aparece na tela. Responde 200 com {}; o segundo envio é "já recebido" no CRM.
# Sem a chave (o exemplo como está), responde 403 e só conta a tentativa na crm_entradas_recusadas, sem e-mail.
$chave = (Get-Content ..\crm-google-chave-dev.txt -Raw).Trim()
$corpo = (Get-Content n8n\exemplos\google-teste.json -Raw -Encoding UTF8).Replace('CHAVE_DO_GOOGLE', $chave)
$resposta = Invoke-WebRequest -UseBasicParsing -Method Post -Uri http://localhost:5679/webhook/crm-entrada-google -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($corpo))
"HTTP $($resposta.StatusCode) $($resposta.Content)"
Remove-Variable chave, corpo, resposta
```

Para o CRM fora do ar: `docker compose stop api`, um envio do site, que vai para a `crm_entradas_pendentes`; depois `docker compose start api` e uma execução manual do `CRM - Reenvio de pendentes`, que manda o lead e apaga a linha.

O campo isca fica no "Only Run If" do Webhook. Ele barra, sem criar execução, o envio em JSON e o como formulário; no multipart, que o n8n só lê depois, quem barra é o "É um robô?", e a execução é gasta:

```powershell
curl.exe -s --data-urlencode "website=http://spam.example.com" http://localhost:5679/webhook/crm-entrada-site   # {"message":"Webhook call received"}, sem execução
curl.exe -s -F "website=http://spam.example.com" http://localhost:5679/webhook/crm-entrada-site                 # multipart: {"recebido":true}, com execução
```

**O limite do site.** Cada envio que passa pelo campo isca vira uma linha na `crm_envios_site`, só com a hora. Acima de 20 envios na última hora ou de 60 nas últimas 24 horas (`limiteSitePorHora` e `limiteSitePorDia`, no "Configuração"), o site recebe a resposta de sempre, e o envio não vai ao CRM:

- ele fica 7 dias na `crm_entradas_recusadas`, com o tipo `site_acima_do_limite`, no máximo os primeiros 8 KB do lead e, no detalhe, as contagens;
- o primeiro barrado de cada janela manda um e-mail ao admin. Se algum envio barrado for de uma pessoa de verdade, cadastre o lead à mão no CRM (Leads › Novo lead) e apague a linha. Se o tráfego for de verdade, suba o limite no "Configuração" e publique o fluxo de novo;
- o lado ruim: um robô que preenche tudo certo pode esgotar o limite, e os leads de verdade que chegarem com a janela cheia ficam nas recusadas até o cadastro à mão. O aviso na hora e os 7 dias nas recusadas cobrem isso até o CAPTCHA no formulário (seção 11 da especificação).

Se falhar a gravação ou a busca na `crm_envios_site`, como com a tabela sem a coluna `criado_em`, o envio vai ao CRM sem o limite. A falha fica nas recusadas como `site_sem_limite`, sem o corpo, e o admin recebe no máximo um aviso por dia.

**Cuidados.**

- Nunca publique nem execute um fluxo sem querer: publicado, o `CRM - Alertas` manda um e-mail de verdade a cada evento do CRM.
- Nenhuma execução fica salva, nem as com erro: o gatilho guardaria o que recebeu, com o lead, o `google_key` do Google e o `X-Crm-Chave` do Alertas. Para ver o motivo de uma falha, siga "Investigar uma falha no n8n", abaixo. A execução que não fica salva ainda passa pelo banco do n8n: ela fica nele, apagada e fora da tela, até a limpeza das apagadas, a cada 15 minutos (a manual, só depois de 1 hora).
- Não baixe um arquivo (formato de resposta "File") num HTTP Request com credencial. Nesse modo, o n8n guarda nos dados da execução a requisição inteira, com o cabeçalho da credencial. Por isso, o relatório semanal baixa o CSV como texto e refaz o anexo.
- Depois de mudar um fluxo, exporte o JSON pelo CLI e deixe só `id`, `name`, `nodes`, `connections` e `settings` (sem `pinData`, `active`, `versionId`, `meta` e as datas). Depois, formate com o Prettier do `web/`, rodando no `web/`: `npx.cmd prettier --config .prettierrc.json --write "../n8n/*.json"`. O `FluxosN8nTests` confere as regras dos JSONs.

  ```powershell
  docker compose exec n8n n8n export:workflow --id=<id do fluxo> --output=/tmp/fluxo.json
  docker compose cp n8n:/tmp/fluxo.json n8n/fluxo-exportado.json
  ```

**Investigar uma falha no n8n.** Como nenhuma execução fica salva, o e-mail do `CRM - Erros` diz só o fluxo e o nó. Para ver o motivo:

1. No fluxo, em Settings, ponha "Save failed production executions" em "Save". Para um teste pelo editor, ponha também "Save manual executions". A mudança vale na hora, sem publicar o fluxo de novo.
2. Reproduza a falha e abra a execução em Executions. Uma execução manual não dispara o `CRM - Erros`.
3. Apague as execuções salvas (Executions › Delete): elas guardam o que o gatilho recebeu, como os dados do lead, o `X-Crm-Chave` no Alertas e o `google_key` no Google.
4. Volte as duas opções para "Do not save".

- Não exporte o JSON do fluxo com o salvamento ligado: o `FluxosN8nTests` falha.
- No n8n local, a mensagem do erro também sai no log do contêiner (`docker compose logs n8n`), sem salvar a execução. Ela pode trazer um dado do lead: não copie o log para fora do computador.

### Importação de leads

Em Importação (só admin), escolha um CSV de até 5 MB e 5.000 linhas: o exportado pela Meta (UTF-16 com tab) ou uma planilha salva pelo Excel (Arquivo › Salvar como › CSV, que sai com ponto e vírgula e Windows-1252). O sistema reconhece a codificação e o separador, mostra a prévia e sugere o campo de cada coluna pelo cabeçalho.

- Confira o campo de cada coluna e as opções: a origem (o arquivo da Meta já vem como Meta), a campanha padrão (para as linhas sem campanha: uma que já existe ou uma nova), a coluna de id externo e a distribuição (rodízio ou uma pessoa).
- As linhas são processadas em segundo plano. A tela mostra o andamento, e o sino avisa quando a importação termina; quem recebeu leads também é avisado, com o total. A importação não manda alerta por e-mail lead a lead.
- Reimportar o mesmo arquivo não duplica: as linhas que já entraram aparecem como "já importadas", mesmo numa planilha sem coluna de id. Uma pessoa que já é lead é vinculada ao lead dela.
- Uma linha sem nome, ou sem e-mail e telefone, não entra e vai para o relatório de erros, com o número da linha na planilha (o cabeçalho é a linha 1). Telefone, e-mail ou CPF/CNPJ em formato inválido entram, com o aviso na ficha.
- No Excel, formate as colunas de telefone e de CPF/CNPJ como texto antes de salvar: como número, o Excel apaga os zeros do começo.
- Para testar, use os arquivos de [docs/exemplos/](docs/exemplos/): `leads-meta.csv` imita a exportação da Meta, e `leads-excel.csv`, a planilha do Excel. Os dois têm acentos, para conferir que nada sai trocado.

### LGPD e auditoria

Na ficha, o admin tem o cartão Privacidade (seção 3.8 da especificação):

- **Exportar dados** baixa um JSON com tudo o que o CRM guarda do lead, para entregar ao titular:
  - os dados;
  - as entradas, com o que chegou de cada origem;
  - as interações e o histórico de etapas;
  - os avisos enviados ao n8n.
- **Anonimizar** tira os dados pessoais e não tem volta.
  - O que muda:
    - o nome vira "Titular removido";
    - saem o e-mail, o telefone, o CPF/CNPJ, a empresa, as respostas do formulário, o que chegou de cada origem, as descrições das interações e o detalhe da perda;
    - o id do lead na Meta ou no Google vira um hash;
    - os avisos ao n8n do lead são apagados.
  - Ficam a etapa, as datas, a origem, a campanha e a responsável, e por isso o painel e o relatório não mudam.
  - O lead anonimizado não aceita edição nem interação nova, mas ainda muda de etapa e de responsável.
  - Um reenvio do mesmo lead pelo n8n, ou a reimportação do mesmo CSV, responde "já recebido" e não traz a pessoa de volta. Se ela voltar por um formulário novo, vira um lead novo.
  - Com uma importação processando, a anonimização espera, porque o arquivo da importação ainda tem os dados das pessoas: tente de novo quando ela terminar.
- **Excluir lead de teste** aparece só nos leads de teste e apaga o lead com tudo o que depende dele.

Em Leads, o botão **Exportar CSV** (só admin) baixa a lista com os filtros e a ordem da tela, pronta para o Excel:

- o telefone no formato nacional;
- o CPF/CNPJ com a máscara, porque sem ela o Excel tira os zeros do começo;
- as datas no horário de Brasília.

Em **Auditoria** (só admin) ficam, do mais recente para o mais antigo, os logins, as mudanças nos leads, nos usuários e nas configurações, as exportações e a LGPD.

- Os filtros de ação, de quem fez e de período ficam no endereço.
- O "Ver na auditoria" da ficha abre a tela só com os registros do lead.
- Os registros não têm dados pessoais: os leads aparecem pelo número.

No n8n, os fluxos não salvam nenhuma execução, nem as com erro (os JSONs de `n8n/` levam o ajuste): o gatilho guardaria o que recebeu, inclusive os dados do lead. Para investigar uma falha, veja "n8n local", em Dia a dia. Conferir o ajuste no n8n de produção fica para quando ele existir.

O passo a passo de um pedido do titular, com o que fica fora do CRM, está em "Pedido do titular (LGPD)", no fim deste README.

## Demonstração

Para a demonstração do painel, o comando `preparar-demonstracao` troca os leads por uma massa fictícia:

- 60 leads nos últimos 30 dias, contados a partir do dia em que o comando roda;
- duas campanhas da Meta, duas do Google e algumas outras origens;
- os leads divididos entre as atendentes ativas e espalhados por todas as etapas, com ganhos, perdas com motivos diferentes, primeiros contatos em tempos variados e alguns ainda sem contato.

Nenhum lead é de teste, porque lead de teste fica fora do painel, e o comando não gera notificação nem evento para o n8n.

**O comando apaga todos os leads** e o que depende deles: entradas, interações, histórico, notificações e eventos para o n8n. Apaga também as campanhas e as importações. Ficam os usuários, as etapas, os motivos de perda, as configurações e a auditoria. A vez do rodízio volta para a primeira atendente em ordem alfabética, então o primeiro lead da demonstração vai sempre para a mesma pessoa.

Ele só roda com `Demo__Permitir=true` no ambiente do próprio comando, nunca no `.env`, e com o `Crm__UrlPublica` em `localhost`. Sem uma dessas condições, ou sem nenhuma atendente ativa, ele se recusa e não mexe em nada.

No banco de desenvolvimento, com a API no contêiner (veja "API e dotnet ef num contêiner Linux"), pare a API, rode o comando pelo serviço dela e suba a API de novo. Ela precisa estar parada porque o comando compila no mesmo volume:

```powershell
docker compose stop api
docker compose run --rm -e Demo__Permitir=true api preparar-demonstracao
docker compose start api
```

Com o `dotnet run`, com o banco no ar (`docker compose up -d db`) e a API parada, porque o build do Windows não sobrescreve o `Crm.Api.exe` em uso:

```powershell
$env:Demo__Permitir = 'true'; dotnet run --project src/Crm.Api -- preparar-demonstracao; Remove-Item Env:Demo__Permitir
```

```bash
Demo__Permitir=true dotnet run --project src/Crm.Api -- preparar-demonstracao
```

Depois, suba a API de novo.

No Compose de produção no próprio computador (o plano B), com ele no ar:

```bash
docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env run --rm -e Demo__Permitir=true api preparar-demonstracao
```

- Depois de um ensaio, rode o comando de novo: tudo volta ao estado inicial.
- O comando roda em outro processo, então recarregue as telas que estiverem abertas.
- Os nomes das campanhas, das pessoas e das empresas ficam em [src/Crm.Api/Infra/Demonstracao/MassaDemonstracao.cs](src/Crm.Api/Infra/Demonstracao/MassaDemonstracao.cs).
- A massa é gravada direto no banco: não gera evento para o n8n nem e-mail, e o comando esvazia a fila de eventos.

### O alerta no celular

Para mostrar o e-mail de alerta chegando no celular quando um lead novo cai para a Adriana:

1. Rode o `preparar-demonstracao`: com a massa nova, a Adriana é a próxima do rodízio.
2. Deixe no ar a API, o n8n e o front: `docker compose up -d api n8n` e, em `web/`, `npm.cmd run dev`. Confira com o `docker compose ps` que a `api` está no ar: depois de atualizar ou reiniciar o Docker Desktop, ela não volta sozinha (veja "API e dotnet ef num contêiner Linux", em Dia a dia).
3. No n8n, deixe o `CRM - Erros` e o `CRM - Alertas` publicados. No `.env`, deixe `N8n__WebhookUrl=http://n8n:5678/webhook/crm-alertas`.
4. Os e-mails saem do `testecrmfs@gmail.com` para os aliases das atendentes (`+adriana` e `+pietra`) e para o seu Gmail.
   - O remetente precisa ser outra conta. Um e-mail mandado de uma conta para um alias dela mesma chega como enviado, sem notificar o celular.
   - No Gmail do admin, um filtro para esse remetente: nunca enviar para spam, sempre marcar como importante e categoria Principal.
5. O lead entra pelo site, o que mostra a cadeia toda (site, n8n, CRM, rodízio, n8n e e-mail):

   ```powershell
   curl.exe -s -H "Content-Type: application/json" --data-binary "@n8n/exemplos/site.json" http://localhost:5679/webhook/crm-entrada-site
   ```

   Ou por Leads › Novo lead, com Responsável "Rodízio". No ensaio de 14/10, o evento saiu de 5 a 7 segundos depois do lead.
6. O `site.json` cria a Carolina Mendes. Enviado de novo, ele vira uma entrada nova do mesmo lead. Para outro lead novo, troque o nome e o e-mail no arquivo, ou rode o `preparar-demonstracao` de novo.
7. O link do e-mail abre a ficha só no computador (`localhost:5173`).
8. Se o e-mail falhar na hora, o lead aparece do mesmo jeito no funil e no sino da Adriana, com a notificação do navegador.

### Plano B: o Compose de produção no próprio computador

Se o ambiente de desenvolvimento falhar no dia, a demonstração roda no Compose de produção no próprio computador, o `crm-local`. Ele tem banco, usuários e massa próprios, separados do banco de desenvolvimento.

No dia da reunião, depois do congelamento:

1. Reconstrua com o código congelado. O `.env` do teste (`../crm-teste.env`) tem o `POSTGRES_PASSWORD`, o `Admin__Email`, o `Admin__SenhaInicial`, `DOMINIO=localhost`, `PORTA_HTTP=8088`, `PORTA_HTTPS=8443` e `PORTAS_IP=127.0.0.1`, sem o `N8n__WebhookUrl`, para não mandar e-mail. O `PORTAS_IP` deixa o Caddy só no próprio computador. Sem ele, o Docker publica as portas em todas as interfaces, e a regra que o Docker Desktop cria no firewall do Windows deixa o Wi-Fi chegar nelas:

   ```powershell
   docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env up -d --build
   docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env ps -a
   ```

   O build leva alguns minutos. O `migracoes` aplica as migrations novas e termina com `Exited (0)`.
2. Abra `https://localhost:8443`. O Chrome avisa que o certificado não é confiável: siga por Avançado › Ir para localhost (não seguro), sem instalar a autoridade do Caddy.
3. Entre como admin e confira, em Configurações › Usuários, a Adriana e a Pietra ativas e no rodízio. Num `crm-local` novo, crie as duas e entre uma vez com cada uma, para trocar a senha provisória.
4. Rode a massa da demonstração, com o comando do Compose de produção acima, e confira o painel: 60 leads novos.

No dia, se o `crm-local` estiver parado, suba sem o `--build`: `docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env up -d`.

O que muda em relação ao desenvolvimento:

- o aviso do certificado, na primeira vez;
- sem service worker, porque o Chrome o recusa com o certificado local: a notificação sai pelo `new Notification`, como em desenvolvimento, e a instalação como app pode não aparecer;
- a auditoria mostra o IP do Docker (`172.30.0.1`) para todo mundo;
- os dados são os do `crm-local`, e não os do banco de desenvolvimento;
- sem o n8n: o `crm-teste.env` não tem o `N8n__WebhookUrl`, então não sai e-mail de alerta.

## Tipos da API no front

Os tipos das chamadas à API, em `web/src/api/schema.d.ts`, são gerados a partir do OpenAPI; não os edite à mão. Depois de mudar um endpoint ou um DTO, com a API rodando:

```bash
cd web && npm run api:tipos
```

Com a API no contêiner, rode antes o `docker compose restart api`, para ela compilar o código novo.

O arquivo gerado vai para o Git, para o build do front não depender da API. O cliente `api` de [web/src/api/cliente.ts](web/src/api/cliente.ts) usa esses tipos.

## Testes e qualidade

```bash
dotnet test                              # unitários e de integração (estes sobem um PostgreSQL pelo Testcontainers)
docker compose -f docker-compose.testes.yml run --rm testes   # os mesmos testes .NET, num contêiner Linux
cd web && npm test                       # Vitest, sem modo watch (npm run test:watch para o modo watch)

dotnet format                            # formata o C#; --verify-no-changes só confere
MSYS_NO_PATHCONV=1 docker compose -f docker-compose.testes.yml run --rm -e ArtifactsPath=/artefatos --entrypoint dotnet testes format Crm.sln --verify-no-changes   # confere a formatação pelo contêiner
cd web && npm run lint                   # ESLint
cd web && npm run format                 # Prettier; format:check só confere
```

O `dotnet test` usa o Microsoft.Testing.Platform, ligado no `global.json`. O build trata avisos como erros. Os testes de integração que mudam o que vale para todos (configurações, ordem das etapas, quem está no rodízio) ficam na coleção `EstadoGlobal`, que roda sozinha depois das outras. Os testes do despachante de eventos usam um banco só deles, no mesmo contêiner, e um relógio falso. Os da anonimização também usam bancos só deles, porque uma importação processando, de qualquer teste, faz a anonimização responder 409. Os testes do tempo real conectam ao hub pelo cliente do SignalR para .NET, com o cookie da sessão; nos testes do front, o hub é falso (`web/src/test/hubFalso.ts`). O teste do log do Caddy (`LogDoCaddyTests`) sobe, também pelo Testcontainers, a imagem do Caddy do Compose de produção com o Caddyfile de verdade.

A suíte do front usa no máximo 2 workers (`maxWorkers: '25%'`, no [web/vite.config.ts](web/vite.config.ts)), e 1 numa máquina de 2 CPUs, como o runner do CI. Com mais workers, os testes disputam a CPU e estouram os tempos de espera: 1 s no `findBy` e no `waitFor` e 5 s por teste. Pelo mesmo motivo, rode-a sem carga pesada na máquina, como um vídeo no navegador, e nunca ao mesmo tempo que a suíte .NET.

No Windows PowerShell 5.1, use `npm.cmd` no lugar de `npm` (a política de execução bloqueia o `npm.ps1`) e rode os comandos em linhas separadas, porque o `&&` não existe nele: `cd web` e, depois, `npm.cmd test`.

### Testes .NET num contêiner Linux

Com o Controle Inteligente de Aplicativos do Windows ligado, o `dotnet test` não carrega os assemblies que não são assinados: o `Testcontainers.dll` e, às vezes, até os dos próprios projetos de teste, recém-compilados. Os testes falham com "Uma política de Controle de Aplicativo bloqueou este arquivo". O [docker-compose.testes.yml](docker-compose.testes.yml) roda os mesmos testes num contêiner Linux, sem mexer na segurança do Windows:

```powershell
docker compose -f docker-compose.testes.yml run --rm testes
```

- O código entra só para leitura: o build fica num volume do Docker, e nada muda nas pastas `bin/` e `obj/` do Windows. Por isso, ele roda mesmo com a API de desenvolvimento aberta, que trava o `Crm.Api.exe` do build do Windows. Os resultados dos testes (`TestResults`) também vão para esse volume: no `/src`, só para leitura, o `dotnet test` não consegue criá-los.
- O PostgreSQL dos testes sobe no Docker do computador, como no `dotnet test` normal.
- A primeira vez baixa a imagem do SDK e os pacotes, o que leva alguns minutos; as seguintes reaproveitam os volumes. Para apagá-los: `docker compose -f docker-compose.testes.yml down -v`.
- O que vem depois de `testes` vai para o `dotnet test`. Para rodar uma classe só:

  ```powershell
  docker compose -f docker-compose.testes.yml run --rm testes --project tests/Crm.IntegrationTests/Crm.IntegrationTests.csproj --filter-class "Crm.IntegrationTests.ImportacaoTests"
  ```

### CI no GitHub Actions

Três workflows, em [.github/workflows](.github/workflows), rodam no GitHub, em runners Ubuntu de 2 vCPUs:

| Workflow | Quando roda | O que faz |
| --- | --- | --- |
| `Testes` ([testes.yml](.github/workflows/testes.yml)) | Em todo push na `master` e em todo PR, inclusive os do Dependabot, menos quando só mudam arquivos `.md` | A suíte .NET pelo `docker-compose.testes.yml`, o `dotnet format --verify-no-changes` e o front: o `npm ci`, o lint, o `format:check`, a suíte e o build |
| `Imagem de produção` ([imagem.yml](.github/workflows/imagem.yml)) | Quando muda o que entra na imagem ou nos Compose: o `Dockerfile`, os Compose, os `.props`, o `global.json`, o `dotnet-tools.json`, o `package.json` e o lockfile do front ou o Caddyfile | O build da imagem e o Compose de produção num projeto só do CI, com dados sorteados: as migrations terminam com 0, o `/health` responde 200 pelo Caddy, e a API roda o ASP.NET Core 10.0.13 ou mais novo |
| `Front: 5 rodadas em paralelo` ([front-5-rodadas.yml](.github/workflows/front-5-rodadas.yml)) | Só quando alguém dispara, pela tela do Actions | A suíte do front 5 vezes ao mesmo tempo, cada uma num runner próprio, para medir a folga das esperas |

- **O front roda na imagem do Node do `Dockerfile`**, a mesma do build de produção, pelo [.github/scripts/front.sh](.github/scripts/front.sh). Assim, o CI pega um problema do build em Alpine e acompanha sozinho a versão que o Dependabot subir. O script roda igual aqui, pelo Git Bash, sem tocar no `node_modules` nem no `dist` do Windows:

  ```bash
  sh .github/scripts/front.sh completo
  FRONT_DOCKER_OPCOES="--cpuset-cpus=0,1 --memory=7g" sh .github/scripts/front.sh completo   # com 2 CPUs, como o runner
  ```

- **As 5 rodadas do front:** em Actions › "Front: 5 rodadas em paralelo" › Run workflow, na `master`. As rodadas não disputam CPU entre si, e cada uma escreve na página da rodada o processador do runner, o tempo da suíte, as falhas e os 10 testes mais lentos, com o "Perdido pede o motivo" em destaque. Rode depois de uma mudança grande no front ou quando o `Testes` falhar por tempo: uma espera acima de 1 s mostra que a folga do runner acabou.
- **Os workflows só leem o repositório:** a permissão é só de leitura, o checkout não guarda o token do GitHub no `.git` (`persist-credentials: false`), nenhum usa segredos nem o `pull_request_target`, e as ações de terceiros ficam fixadas pelo commit (SHA), com a versão num comentário. O `WorkflowsTests` cobra isso. Para conferir os workflows antes do push:

  ```powershell
  docker run --rm -v "${PWD}:/repo:ro" -w /repo rhysd/actionlint:1.7.12
  ```

- **Minutos:** o repositório é privado, e o GitHub Free dá 2.000 minutos por mês, com cada job arredondado para o minuto de cima. Nas três primeiras rodadas verdes, em 20/10/2026, o `Testes` levou 4 min 37 s, 6 min 58 s e 6 min 47 s, conforme a máquina do runner: de 5 a 7 minutos cobrados, o que dá mais de 250 rodadas por mês. O `Imagem de produção` leva de 2 a 3 minutos, o que dá 3 minutos cobrados, e roda poucas vezes por mês. As 5 rodadas do front terminaram em 2 min 40 s, o que dá até 3 minutos cobrados por rodada, 15 por disparo. Sem forma de pagamento cadastrada, o Actions para quando a cota acaba, sem cobrança.
- **O check vermelho não impede o merge:** no GitHub Free, repositório privado não tem proteção de branch. A conferência antes do merge é manual (veja "Versões fixadas"). Na conta da empresa, a `master` ganha a proteção (veja "O que depende da empresa", em Publicação).

## Banco de dados

```bash
dotnet ef migrations add <NomeDescritivo> --project src/Crm.Api   # nova migration, em src/Crm.Api/Infra/Dados/Migrations
docker compose run --rm ef migrations add <NomeDescritivo>        # o mesmo, pelo contêiner (veja Dia a dia)
docker compose down                                               # para o banco e a API do contêiner e mantém os dados
docker compose down -v                                            # para os dois e APAGA os dados, as chaves da sessão e o build do contêiner
```

- Uma migration por mudança, com nome descritivo. Nunca edite uma migration já aplicada.
- A senha do banco é gravada quando o volume é criado. Se você trocar `POSTGRES_PASSWORD` depois disso, o banco continua com a senha antiga. Em desenvolvimento, é mais simples recriar o volume com `docker compose down -v`.

## Estrutura

```
src/Crm.Api/                 API (ASP.NET Core, .NET 10), organizada por módulo em Features/: Usuarios/ (acesso e cadastro),
                             Leads/ (leads, entrada, rodízio, normalização, campanhas, interações, linha do tempo,
                             exportação em CSV e LGPD: dados do titular, anonimização e exclusão de leads de teste),
                             Funil/ (etapas, motivos de perda, mudança de etapa com histórico e quadro),
                             Notificacoes/ (sino e quem é avisado de cada acontecimento), Integracoes/ (chave e rotas do n8n),
                             Painel/ (métricas da seção 8 e o relatório por campanha)
src/Crm.Api/Infra/           DbContext e migrations (Dados/), auditoria e a consulta dela (Auditoria/), .env e configurações do sistema
                             (Configuracao/), eventos para o n8n e o despachante (Eventos/), horário de Brasília (Datas/),
                             importação CSV, com a leitura do arquivo e o processamento em segundo plano (Importacoes/),
                             hub do tempo real, com a conferência das conexões e os avisos depois do commit (TempoReal/),
                             ProblemDetails, validação, CSV para o Excel, proxy, chaves da sessão e o front servido pela
                             API (Web/),
                             o comando que prepara a demonstração do painel (Demonstracao/)
tests/Crm.UnitTests/         regras de negócio, sem banco
tests/Crm.IntegrationTests/  API + PostgreSQL real (Testcontainers)
web/                         React + TypeScript + Vite: features/ por módulo (acesso, painel, relatorio, auditoria, funil, leads,
                             importacao, notificacoes, tempo-real, configuracoes, usuarios), components/ui/ do shadcn/ui,
                             api/ com o cliente e os tipos gerados, pwa/ com o registro do service worker; public/ com o
                             manifesto, os ícones e o service worker do PWA; icones/ com as fontes dos ícones e o script que
                             gera os PNGs
n8n/                         JSON dos 8 fluxos do n8n da seção 4.5 e, em exemplos/, os payloads fictícios dos testes
deploy/                      Caddyfile e scripts de backup e de teste da restauração, usados pelo docker-compose.prod.yml
docs/                        especificação, guia das atendentes, guia de uso do Claude Code e CSVs de exemplo para a
                             importação (exemplos/)
Dockerfile                   imagem de produção: a API com o build do React e o migration bundle
.github/                     o CI, com os workflows e os scripts deles (veja "CI no GitHub Actions"), a conferência dos
                             binários nativos do lockfile do front (veja "Um pacote do npm com alerta") e o Dependabot: um
                             PR por mês com as imagens, outro com o NuGet e outro com as ações (veja "Versões fixadas")
```

## Publicação

O [docker-compose.prod.yml](docker-compose.prod.yml) sobe tudo num servidor com Docker: o Caddy, com HTTPS automático na frente da API; a API, que também serve o front; o PostgreSQL; as migrations, que rodam antes da API; e o backup diário. Só o Caddy expõe portas (80 e 443).

Nesta seção, **[servidor]** marca o que depende do servidor, e **[domínio]**, o que depende do domínio. Nenhum dos dois existe ainda.

### O que está pronto

Pronto e testado no Compose de produção no próprio computador (veja "Plano B" em Demonstração):

- o Compose, com o Caddy, a API, o PostgreSQL, as migrations e o backup;
- o HTTPS, com a autoridade local do Caddy, os cabeçalhos de segurança e a CSP;
- a sessão, que continua depois de recriar o contêiner da API;
- o IP de quem acessa, atrás do Caddy;
- o log do Caddy, que grava as falhas do proxy sem a query string, o `Referer` e o `X-Api-Key` (veja "Cuidados");
- o backup e o teste da restauração;
- a volta de uma atualização que deu errado, com e sem migration nova (veja "Se a atualização der errado");
- a importação e o PWA no build de produção;
- as imagens com versão fixa, com o Dependabot avisando das novas, e a rotação de log em todo serviço, cobrada por um teste (veja "Versões fixadas");
- o build da imagem e a subida do Compose com dados fictícios, também no CI, a cada mudança nas imagens ou nos pacotes (veja "CI no GitHub Actions").

### O que depende da empresa

O resto depende do servidor e do domínio, do n8n Cloud pago, do e-mail da empresa, dos acessos da Meta e do Google Ads e de decisões da Gama, do chefe e das pessoas:

- **Servidor e domínio:**
  - **[servidor]** a máquina, com o Docker e as portas 80 e 443 abertas, e a pasta dos backups (`BACKUP_PASTA`);
  - **[servidor]** o destino da cópia criptografada dos backups e o cron dela (veja "Backup");
  - **[servidor]** repetir lá o teste da restauração e a conferência do log do Caddy, e conferir na auditoria o IP de um cliente IPv4 e o de um IPv6, como um celular no 4G (veja "Cuidados");
  - **[domínio]** o domínio, com o registro DNS do tipo A apontando para o servidor. Ele vira o `DOMINIO` do `.env`, que dá o certificado do Let's Encrypt, os links dos e-mails (`Crm__UrlPublica`) e o `wss://` da CSP;
  - **[domínio]** o PWA e a notificação num celular de verdade, que exigem HTTPS.
- **n8n Cloud, numa conta da empresa:**
  - os 8 fluxos de [n8n/](n8n/), já testados no n8n local, as credenciais, as 3 Data Tables e o `N8n__WebhookUrl` (veja "n8n Cloud: importar os fluxos");
  - o horário do `CRM - Reenvio de pendentes`, pelo plano contratado: cada rodada agendada conta como execução (veja "Cuidados");
  - conferir lá as configurações de execução, que o envio barrado pelo campo isca não conta no plano e por quanto tempo o plano guarda uma execução "crashed";
  - apontar o `.mcp.json` para o MCP do n8n Cloud e remover o local.
- **E-mail da empresa:**
  - a credencial SMTP de produção e o remetente dos alertas, do resumo diário e do relatório;
  - com ela no ar, revogar a senha de app da `testecrmfs@gmail.com` (Conta do Google › Segurança › Senhas de app), que está na credencial `CRM - E-mail` do n8n local.
- **Meta:**
  - o app em modo Live, que exige a URL da política de privacidade, a verificação da empresa e o acesso avançado;
  - a credencial `CRM - Meta Lead Ads`, a página e o formulário, e um lead da Ferramenta de Teste;
  - um CSV exportado de verdade, para conferir a importação.
- **Google Ads:** o "Enviar dados de teste" de verdade e a chave de produção, que a Gama cola no formulário (passo 5 de "n8n Cloud: importar os fluxos").
- **Gama e chefe:**
  - as etapas definitivas do funil;
  - o e-mail da Gama para o relatório semanal;
  - quem faz o site, com o CAPTCHA no formulário (seção 11 da especificação). O campo isca e o limite de envios protegem o CRM e as atendentes, mas um robô que preencha tudo certo gasta uma execução do n8n por envio, e, com a cota esgotada, todos os fluxos param;
  - a URL da política de privacidade;
  - o prazo para anonimizar os leads perdidos e as outras decisões da seção 11 da especificação.
- **Pessoas:**
  - a validação com a Adriana e a Pietra, pelo [guia das atendentes](docs/guia-das-atendentes.md), que é o aceite do Passo 11 e pode ser no ambiente local;
  - a data da reunião com o chefe.
- **GitHub:** quando o repositório for para a conta da empresa, proteger a `master` com uma regra que bloqueie o force push e a exclusão. Em repositório privado, isso exige um plano com regras de branch, que o GitHub Free não tem (veja "CI no GitHub Actions").

### O que o servidor precisa

- **[servidor]** Linux com Docker Engine e o plugin Compose. Um Ubuntu 24.04 LTS com 2 GB de RAM basta.
- **[servidor]** Portas 80 e 443 abertas para a internet: o Caddy usa a 80 para obter o certificado do Let's Encrypt.
- **[domínio]** Um domínio ou subdomínio, como `crm.empresa.com.br`, com um registro DNS do tipo A apontando para o IP do servidor. Não crie o registro AAAA (IPv6), a não ser que o IPv6 esteja ligado no Docker: sem isso, quem acessa por IPv6 aparece com o IP interno do Docker na auditoria e no limite de logins.

### Primeira publicação

Tudo no servidor **[servidor]**, com o domínio já apontando para ele **[domínio]**:

1. Clone o repositório no servidor (por exemplo, em `/opt/crm`) e entre na pasta.
2. Crie o `.env` de produção. Os comandos geram as senhas e as chaves sem mostrá-las; troque o domínio e o e-mail:

   ```bash
   cp .env.example .env && chmod 600 .env
   cr=$(printf '\r')
   sed -b -i \
     -e "s/^POSTGRES_PASSWORD=[^$cr]*\($cr*\)\$/POSTGRES_PASSWORD=$(openssl rand -hex 16)\1/" \
     -e "s/^DOMINIO=[^$cr]*\($cr*\)\$/DOMINIO=crm.empresa.com.br\1/" \
     -e "s/^Admin__Email=[^$cr]*\($cr*\)\$/Admin__Email=voce@empresa.com.br\1/" \
     -e "s/^Admin__SenhaInicial=[^$cr]*\($cr*\)\$/Admin__SenhaInicial=$(openssl rand -hex 8)\1/" \
     -e "s/^Integracao__Chave=[^$cr]*\($cr*\)\$/Integracao__Chave=$(openssl rand -hex 24)\1/" \
     -e "s/^N8n__WebhookChave=[^$cr]*\($cr*\)\$/N8n__WebhookChave=$(openssl rand -hex 24)\1/" .env
   grep '^Admin__SenhaInicial=' .env   # a senha do primeiro acesso
   ```

   O Compose de produção monta a connection string e o `Crm__UrlPublica` (`https://` + `DOMINIO`) sozinho; as linhas deles no `.env` não são usadas. O `N8n__WebhookUrl` fica vazio até o n8n de produção existir: até lá, os eventos ficam pendentes.

3. Suba tudo. A primeira vez leva alguns minutos, por causa do build do front e da API:

   ```bash
   docker compose -f docker-compose.prod.yml up -d --build
   ```

4. Confira: `curl -I https://crm.empresa.com.br/health` responde 200, e `docker compose -f docker-compose.prod.yml ps -a` mostra `db`, `api`, `caddy` e `backup` rodando, e `migracoes` terminado com código 0.
5. Entre com o `Admin__Email` e a senha inicial: o sistema pede a senha nova. Depois, cadastre as atendentes. As linhas `Admin__*` do `.env` podem ser apagadas.

### n8n Cloud: importar os fluxos

A produção vai ser o n8n Cloud, numa conta da empresa. Os JSONs de [n8n/](n8n/) vão como estão: no Cloud, muda só o que é da instância (as credenciais, a Data Table, os valores do nó "Configuração", o fluxo de erro e, na Meta, a página e o formulário). **[n8n Cloud]** marca o que depende dele.

1. **[n8n Cloud]** Crie as credenciais com os mesmos nomes do n8n local (veja "n8n local", em Dia a dia), com os valores de produção:
   - `CRM - Chave de integração` e `CRM - Chave dos eventos`: o `Integracao__Chave` e o `N8n__WebhookChave` do `.env` do servidor;
   - `CRM - E-mail`: o SMTP do e-mail da empresa;
   - `CRM - Meta Lead Ads`: com o app da Meta em modo Live (seção 4.5 da especificação).
2. **[n8n Cloud]** Crie as Data Tables `crm_entradas_pendentes`, `crm_entradas_recusadas` e `crm_envios_site`, com as colunas de "n8n local", em Dia a dia, no projeto dos fluxos.
3. **[n8n Cloud]** Importe pela tela (Workflows › Import from File), primeiro o `crm-erros.json`, depois os outros. A importação dá ids novos aos fluxos.
4. Em cada fluxo, troque os valores do nó "Configuração":

   | Fluxo | Campos |
   | --- | --- |
   | `CRM - Erros`, `CRM - Alertas` | `emailAdmin` e `remetente` (o e-mail da credencial SMTP) |
   | `CRM - Entrada Site` | `urlCrm` (`https://` e o `DOMINIO`), `emailAdmin` e `remetente` (o `limiteSitePorHora` e o `limiteSitePorDia` ficam) |
   | `CRM - Reenvio de pendentes` | `urlCrm`, `emailAdmin` e `remetente` (o `linhasPorRodada` fica) |
   | `CRM - Entrada Google` | `urlCrm`, `googleKeySha256` (passo 5), `emailAdmin` e `remetente` (o `googleKeySha256Anterior` fica vazio: ele só vale durante a troca da chave) |
   | `CRM - Entrada Meta` | `urlCrm` (o `urlGraph` fica), `emailAdmin` e `remetente` |
   | `CRM - Relatório semanal Gama` | `urlCrm`, `emailGama` (o endereço que a Gama indicar) e `remetente` |
   | `CRM - Resumo diário` | `emailAdmin` e `remetente` (o `diasRecusadas`, o `diasEnviosSite` e o `diaDoResumoSemanal` ficam) |

   E também:
   - escolha a credencial nos nós que a pedem;
   - em Settings, deixe o Error Workflow no `CRM - Erros` (depois de publicá-lo), as quatro opções de salvar em "Do not save" e o fuso em America/Sao_Paulo;
   - no `CRM - Entrada Site`, ponha o domínio do site em "Allowed Origins (CORS)" do Webhook;
   - na `CRM - Entrada Meta`, escolha a página e o formulário.
5. Gere a chave do Google de produção. A chave vai para a área de transferência, para a Gama colar no formulário de lead do Google Ads, e o SHA-256 aparece na tela, para o `googleKeySha256`. Antes, desative o "Histórico da área de transferência em seus dispositivos" (Configurações › Sistema › Área de Transferência), que pode mandar o texto copiado para a nuvem da Microsoft:

   ```powershell
   $bytes = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
   $chave = -join ($bytes | ForEach-Object { $_.ToString('x2') })
   Set-Clipboard -Value $chave
   $hash = -join ([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($chave)) | ForEach-Object { $_.ToString('x2') })
   "SHA-256 da chave do Google: $hash"
   Remove-Variable bytes, chave
   ```

   Depois que a chave chegar à Gama, limpe a área de transferência com o comando de "Trocar chaves e senhas" e, com o histórico ligado (Win+V), apague o item da chave.
6. **[n8n Cloud]** Publique o `CRM - Erros`, depois os outros. A Meta só pode ser publicada com o app Live. Antes de publicar o `CRM - Reenvio de pendentes`, decida o horário dele pelo plano contratado (veja "Cuidados").
7. **[servidor]** No `.env` do servidor, ponha `N8n__WebhookUrl=https://<instância>.app.n8n.cloud/webhook/crm-alertas` e rode `docker compose -f docker-compose.prod.yml up -d api`.
8. A Gama cola no formulário de lead do Google Ads a URL do `crm-entrada-google` e a chave. O formulário do site manda os campos da seção 4.5 da especificação para a URL do `crm-entrada-site`, em JSON ou como formulário, e não como multipart: no multipart, o envio do robô gasta uma execução.
9. Confira:
   - um "Enviar dados de teste" do Google, um lead da Ferramenta de Teste da Meta, um envio do site e uma execução manual do relatório e do resumo diário;
   - um envio do site com o campo isca, que não pode aparecer no contador de execuções do plano;
   - no fim, confira em Executions que nenhuma execução ficou salva e veja por quanto tempo o plano guarda uma execução "crashed", a interrompida por uma queda do n8n.
10. No Claude Code, aponte o `.mcp.json` para o MCP do n8n Cloud e remova o local: `claude mcp remove --scope user n8n-local`.

### Atualizar

Antes de mudar, anote o commit de agora, que é para onde voltar se der errado, e tire um backup:

```bash
git rev-parse --short HEAD                                                         # anote: o commit de antes
docker compose -f docker-compose.prod.yml exec backup sh /backup/backup.sh agora   # um backup antes de mudar
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

O serviço `migracoes` aplica as migrations novas antes de a API subir. As sessões continuam valendo, porque as chaves que protegem o cookie ficam no volume `chaves-sessao`.

Quem está com o CRM aberto continua na versão anterior até recarregar a página. Cada tela é um arquivo próprio, baixado na primeira vez que ela abre. Se a pessoa abrir uma tela cujo arquivo antigo já saiu do servidor, a página recarrega sozinha uma vez, já na versão nova e na tela pedida. Se falhar de novo em menos de um minuto, como sem conexão, a tela mostra "Não foi possível abrir esta tela", com o botão "Recarregar a página", sem recarregar em laço.

### Se a atualização der errado

Se as migrations falharem, se a API não subir ou se uma tela quebrar, volte ao commit anotado em "Atualizar". O caminho depende de a atualização ter trazido uma migration nova:

```bash
anterior=<o commit anotado>
git diff --stat "$anterior" HEAD -- src/Crm.Api/Infra/Dados/Migrations   # vazio: nenhuma migration nova
```

**Sem migration nova,** o banco continua no formato do commit anterior. Volte o código e suba de novo; nada se perde:

```bash
git switch --detach "$anterior"
docker compose -f docker-compose.prod.yml up -d --build
```

**Com migration nova,** o banco pode já estar no formato novo, que o código anterior não conhece. Volte o código e restaure o backup tirado antes da atualização:

```bash
git switch --detach "$anterior"
docker compose -f docker-compose.prod.yml stop api
docker compose -f docker-compose.prod.yml exec backup sh -c 'dropdb crm && createdb crm && pg_restore --no-owner --exit-on-error --dbname=crm /backups/crm-AAAA-MM-DD-HHMMSS.dump'
docker compose -f docker-compose.prod.yml up -d --build
```

- O backup é o de logo antes da atualização: o nome do arquivo tem a data e a hora (`ls -t` na pasta dos backups mostra o mais recente primeiro). Um backup diário das 03:00 que saiu depois da atualização já tem a migration nova.
- O que entrou no CRM depois desse backup se perde. Como em "Restaurar de verdade", a auditoria volta junto: antes de restaurar, anote as anonimizações e as exclusões de leads de teste feitas depois do backup, e refaça-as depois.
- O `up -d --build` refaz a imagem do commit anterior, e o serviço `migracoes` não tem o que aplicar.

**Os fluxos do n8n.** Se a atualização também mudou fluxos de [n8n/](n8n/) já importados no n8n Cloud, volte cada um ao JSON do commit anterior, que está no repositório:

```bash
git show "$anterior":n8n/crm-entrada-site.json > crm-entrada-site.json   # um por fluxo que mudou
```

Importe-o como na primeira vez (passos 3 e 4 de "n8n Cloud: importar os fluxos"). Ele entra como um fluxo novo, com id novo: refaça nele o que é da instância, despublique o fluxo da versão nova, publique o importado e arquive o outro. Se for o `CRM - Erros`, aponte para ele o Error Workflow dos outros. O histórico de versões do n8n Cloud, que mantém o id, é a segunda opção, se o plano contratado tiver: no Starter, ele pode não existir ou durar pouco.

Com a correção publicada, volte para a `master` (`git switch master`) e siga "Atualizar".

### Versões fixadas

Toda imagem tem a versão exata, e não uma tag que muda sozinha, como `10.0`, `2` ou `17`. Assim, o servidor, este computador e os testes rodam a mesma versão. O número fica só nos arquivos, e não aqui, para o PR mensal do Dependabot não deixar esta tabela velha.

| Imagem | Onde |
| --- | --- |
| `mcr.microsoft.com/dotnet/sdk` | `Dockerfile` (build), `docker-compose.yml` (`api` e `ef`) e `docker-compose.testes.yml`, a mesma nos quatro lugares. O runtime que ela traz é o da imagem `aspnet` |
| `mcr.microsoft.com/dotnet/aspnet` | `Dockerfile` (a imagem final), na 10.0.13 ou mais nova, com a correção da CVE-2026-70159 |
| `node` | `Dockerfile` (build do front). O CI roda o front nela |
| `postgres` | `docker-compose.yml` e `docker-compose.prod.yml` (`db` e `backup`), a mesma nos dois. Os testes de integração usam a do Compose de produção |
| `caddy` | `docker-compose.prod.yml`. O teste do log do Caddy usa a do Compose |
| `n8nio/n8n` | `docker-compose.yml`, só em desenvolvimento. Acompanha a do n8n Cloud. Antes de uma versão nova, veja "O n8n numa versão nova", abaixo |

O `ImagensTests` cobra a versão exata de cada imagem, o mesmo SDK nos quatro lugares, o mesmo PostgreSQL em desenvolvimento e em produção e o runtime com a correção. Uma imagem nova precisa entrar nele.

**Quando subir.**
- O Dependabot ([.github/dependabot.yml](.github/dependabot.yml)) abre, uma vez por mês, um PR com as imagens novas, com o `Dockerfile` e os Compose juntos, um com os pacotes do NuGet e um com as ações dos workflows.
- Nas imagens e no NuGet, a versão principal (.NET 11, PostgreSQL 18, Node 24) fica de fora, porque é uma decisão nossa. Nas ações, ela entra: ficar parado numa versão principal antiga acaba em aviso de descontinuação do GitHub, e o próprio CI testa o PR.
- O npm também fica de fora, por causa do Controle Inteligente de Aplicativos do Windows, e o "Create Dependabot security update" dos alertas dele não é usado. No mesmo dia do PR, rode o `npm.cmd audit` em `web/`, que só lê. Um pacote com alerta sobe pelo roteiro "Um pacote do npm com alerta", abaixo.
- O Dependabot só funciona com o arquivo no GitHub e os alertas ligados no repositório (Settings › Code security).

**Como conferir um PR do Dependabot.** No GitHub Free, repositório privado não tem proteção de branch: nada impede o merge com o check vermelho. A conferência é manual:

1. O check do PR está verde: o `Testes` e, quando o PR muda as imagens, os pacotes ou as ações, o `Imagem de produção` (veja "CI no GitHub Actions"). O `Testes` roda as duas suítes, com o teste do log do Caddy na imagem do Compose, e o `Imagem de produção` sobe o Compose de produção.
2. O check vale para o que vai entrar:
   - se a `master` andou depois do último rebase do PR só com arquivos `.md`, ele continua valendo;
   - com qualquer outro arquivo, comente `@dependabot rebase` no PR e espere o check de novo;
   - um PR sem nenhum check, como um aberto antes de os workflows existirem, também ganha os checks com o rebase;
   - se o rebase não refizer o PR, traga a `master` para o branch dele por merge, num branch local, e dê o push para o branch do PR, sem `--force`. Foi o caso do #2: o `@dependabot rebase` falhou em silêncio em 01/11 e em 04/11/2026, e o PR ficou no commit antigo, sem checks. Antes do push, confira o branch local contra o do PR (`git log --oneline origin/<branch do PR>..pr-<número>` e `git diff master pr-<número>`). O squash do merge do PR apaga esse merge. Depois do push, o Dependabot não refaz mais o PR: faça o merge dele antes dos outros abertos.

     ```powershell
     git fetch origin
     git switch -c pr-<número> origin/<branch do PR>
     git merge --no-edit origin/master
     git push origin pr-<número>:<branch do PR>
     ```

3. As versões:
   - em todo PR, a versão nova saiu há pelo menos 3 dias. O Dependabot espera 3 dias por padrão, mas não dá para contar com isso. Nas imagens, ele avisa "Cooldown was not applied", porque o registro não informa a data de publicação. No NuGet, sem aviso, o #5 de 01/11/2026 trouxe o Scalar.AspNetCore 2.18.7 com 2 dias e 3 horas. Confira a data no Docker Hub, no registro da Microsoft, na nuget.org ou nas versões da ação;
   - num PR de imagens, o runtime que a imagem nova do SDK traz é o mesmo da imagem `aspnet` do PR:

     ```powershell
     (docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:<versão> --format '{{json .Image}}') -split ',' | Select-String -Pattern '"DOTNET_VERSION=' | Select-Object -First 1
     ```

   - num PR de imagens, o SDK sobe nos quatro lugares. Se ele subir só no `Dockerfile`, o `ImagensTests` fica vermelho, como no #4, em 01/11/2026: no branch local do item 2, suba o SDK nos Compose num commit, antes do push.
4. Traga o branch do PR para a pasta do projeto (`git fetch origin` e `git switch <branch do PR>`, ou o branch local do item 2) e suba o ambiente de desenvolvimento com ele:
   - num PR do NuGet, `docker compose up -d --force-recreate api`, que recria só a API, e ela compila com os pacotes novos. Com a API no ar e a configuração igual, o `docker compose up -d api` não faz nada, e ela continua com os pacotes antigos;
   - num PR de imagens, `docker compose up -d`, que recria só os serviços cuja imagem mudou (o `ef`, que tem perfil, fica de fora). Se o PR sobe o n8n, antes copie e confira o volume dele (veja "O n8n numa versão nova", abaixo).
5. Confira a demonstração (veja "O alerta no celular", em Demonstração). Quando o PR sobe o n8n, repita também os testes de "n8n local", em Dia a dia: o envio do site, o do Google com a chave certa e sem ela, e o CRM fora do ar com o reenvio. A versão do n8n local também deve acompanhar a do n8n Cloud, onde os fluxos vão rodar.
6. Só então faça o merge, pelo "Squash and merge": a `master` fica sem merge commits. Troque o título do Dependabot, em inglês, por um em português, como "Pacotes do NuGet: FluentValidation 12.2.0 e outros 11 (#3)", e mantenha o corpo dele, que lista as versões. Confira o título antes de confirmar: em 04/11/2026, o #4 entrou com espaços no começo, e o #5, com o título do Dependabot.
7. Depois do merge, a pasta do projeto volta para a `master`:
   1. `git switch master` e `git pull --ff-only`. O `git status` mostra a `master` igual à `origin/master`, sem nada à frente nem atrás.
   2. Suba o ambiente com a `master`: num PR do NuGet, recrie a API (`docker compose up -d --force-recreate api`); num PR de imagens, o `docker compose up -d` recria os serviços que mudaram.
   3. Confira a árvore inteira: `git diff --stat master <branch do PR>` só pode mostrar arquivos `.md`, os que a `master` ganhou depois do último rebase do PR. Se aparecer qualquer outro arquivo, pare e investigue antes de apagar o branch.
   4. Apague o branch local: `git branch -D <branch do PR>`. Com o squash, o commit do PR não entra no histórico da `master`, e o `git branch -d` recusa. O Dependabot apaga o branch dele no GitHub, e o `git fetch --prune` tira a referência.
   5. Se o PR subiu o n8n, confira o n8n com a `master` (ele entra, os fluxos e as credenciais estão lá, e os publicados continuam publicados) e só então apague a cópia do volume: `docker volume rm crm-dados-n8n-copia`.

**O n8n numa versão nova.** O volume `crm-leads_dados-n8n` guarda o banco do n8n local, com os fluxos, as Data Tables e as credenciais, e o arquivo `config`, com a chave que as criptografa. Uma versão nova pode mudar esse banco de um jeito que a anterior não abre mais. Por isso, antes de o n8n subir na versão nova, copie o volume para o `crm-dados-n8n-copia`. A mesma cópia serve antes de uma mudança grande nos fluxos ou nas Data Tables: ela volta tudo de uma vez, e um fluxo sozinho volta pelo histórico de versões.

- Neste roteiro, "a cópia" é sempre esse volume. A pasta do projeto, quando volta para a `master`, é só o `git switch master`.
- Nenhum passo apaga nada sem conferir a cópia.
- As mensagens dos comandos saem sem acento, para não saírem trocadas no PowerShell 5.1.

Antes de o n8n subir na versão nova:

1. Pare o n8n e copie o volume. O original entra só para leitura. Se o volume da cópia não existir, o `docker run` o cria. Se ele já tiver arquivos, de uma atualização anterior, o comando para com "PAROU" sem copiar nada: confira essa cópia velha e apague-a antes de seguir.

   ```powershell
   docker compose stop n8n
   docker run --rm -v crm-leads_dados-n8n:/n8n:ro -v crm-dados-n8n-copia:/copia alpine:3.22 sh -c 'find /copia -mindepth 1 | grep -q . && { echo PAROU: crm-dados-n8n-copia tem arquivos de antes; exit 1; }; cp -a /n8n/. /copia/'
   ```

2. Confira a cópia contra o original: a mesma lista de arquivos e os mesmos tamanhos, e o `database.sqlite` e o `config` com conteúdo. O comando diz "OK" ou "DIFERENTE".

   ```powershell
   docker run --rm -v crm-leads_dados-n8n:/n8n:ro -v crm-dados-n8n-copia:/copia:ro alpine:3.22 sh -c 'for v in n8n copia; do (cd /$v && { find . -mindepth 1 -type f -exec stat -c %n:%s {} +; find . -mindepth 1 ! -type f; } | sort > /tmp/$v); done; cmp -s /tmp/n8n /tmp/copia && test -s /copia/database.sqlite && test -s /copia/config && echo OK: mesmos arquivos e tamanhos || { echo DIFERENTE: pare aqui; exit 1; }'
   ```

   Com "DIFERENTE", o n8n não sobe na versão nova. A pasta do projeto volta para a `master` (`git switch master`), para um `docker compose up -d` não subir a versão nova por engano, e o `docker compose start n8n` religa o contêiner de antes. Investigue antes de tentar de novo.
3. Com "OK", siga o passo 4 de "Como conferir um PR do Dependabot": o `docker compose up -d` sobe o n8n na versão nova.

Se a versão nova der errado, volte o n8n à versão de antes, com os dados da cópia:

1. Pare o n8n e volte a pasta do projeto para a `master`, que ainda tem a versão de antes.
2. Ponha a cópia de volta. O comando confere antes que a cópia tem o `database.sqlite` e o `config` com conteúdo. Sem eles, ele para com "PAROU" sem apagar nada: sem a cópia, esvaziar o volume apagaria as credenciais, e a senha de app do e-mail só existe lá. Com eles, o comando esvazia o volume do n8n, para não sobrar o `-wal` do SQLite da versão nova, e põe a cópia de volta.

   ```powershell
   docker compose stop n8n
   git switch master
   docker run --rm -v crm-dados-n8n-copia:/copia:ro -v crm-leads_dados-n8n:/n8n alpine:3.22 sh -c 'test -s /copia/database.sqlite && test -s /copia/config || { echo PAROU: sem o database.sqlite e o config, nada foi apagado; exit 1; }; find /n8n -mindepth 1 -delete && cp -a /copia/. /n8n/'
   ```

3. Rode de novo a conferência, a do passo 2 da lista de cima: ela precisa dizer "OK".
4. Suba o ambiente (`docker compose up -d`) e confira o n8n na tela: ele entra, os fluxos e as credenciais estão lá, e os publicados continuam publicados.
5. Só então apague a cópia: `docker volume rm crm-dados-n8n-copia`.

Se a cópia não existir, o `docker run` cria um volume vazio com o nome dela, e os comandos param do mesmo jeito: a conferência diz "DIFERENTE", e a volta diz "PAROU". Apague esse volume vazio depois de investigar.

**Um pacote do npm com alerta.** O Controle Inteligente de Aplicativos do Windows bloqueia um binário nativo novo que não seja assinado. Por isso, o lockfile muda num contêiner, e o Windows só recebe a mudança depois de conferida:

1. Escolha a versão: a corrigida mais antiga do alerta, com pelo menos 3 dias, e que não troque nenhum pacote com binário nativo, como o rolldown, o lightningcss e o oxide do Tailwind. As dependências de cada versão saem do `npm.cmd view <pacote>@<versão> dependencies optionalDependencies`, e as datas, do `npm.cmd view <pacote> time`, que só leem.
2. Na raiz do projeto, escreva o `package.json` e o `package-lock.json` num contêiner com a imagem do Node do `Dockerfile` e o mesmo npm do Windows. O `node_modules` fica num volume que sai com o contêiner, e o `--package-lock-only` não instala nada:

   ```powershell
   $imagem = (Select-String -Path Dockerfile -Pattern '^FROM (node:\S+)').Matches[0].Groups[1].Value
   $npm = npm.cmd --version
   function npmNoConteiner { docker run --rm -v "${PWD}\web:/web" -v /web/node_modules -w /web $imagem npx -y "npm@$npm" @args --package-lock-only --no-audit --no-fund }
   npmNoConteiner install -D vite@8.3.3   # um pacote do package.json
   npmNoConteiner update source-map-js    # um pacote que vem por outro
   npmNoConteiner install                 # sempre no fim, sem pacote
   ```

   - O último `install`, sem pacote, recalcula as marcas `dev` e `devOptional` do lockfile, que podem ficar velhas depois de um `uninstall`. Rodado de novo, ele não pode mudar mais nada.
   - O `install` de um pacote também resolve de novo as dependências dele e pega a mais nova de cada faixa. Se isso trouxer a versão nova de uma dependência que não precisava mudar, segure a do lockfile: instale-a junto e tire-a do `package.json` logo em seguida. Em 26/10/2026, o `install -D vitest@5.0.3` puxava o `magic-string` 1.4.4, publicado naquele dia, e a regra dos 3 dias a barra. O `--prefer-dedupe` não segurou, e o par abaixo segurou a 1.4.2:

     ```powershell
     npmNoConteiner install -D vitest@5.0.3 magic-string@1.4.2
     npmNoConteiner uninstall magic-string
     ```
   - O contêiner escreve o mesmo lockfile que o npm do Windows, byte a byte. A diferença é que o `--package-lock-only` no Windows regrava o `node_modules/.package-lock.json`, e o contêiner não toca no `node_modules` do Windows.
   - O npm 11 avisa que o `msw` tem um script de instalação que não está no `allowScripts`. Nada roda: no contêiner, o `--package-lock-only` não instala nada, e, no passo 5, o `--ignore-scripts` não roda nenhum script.
3. Confira o lockfile entrada por entrada: `node .github/scripts/nativos-do-lock.mjs` compara o do último commit com o da pasta do projeto, lista o que entrou, saiu ou mudou de versão e diz se as entradas com binário nativo (as que têm `os` ou `cpu`) ficaram IGUAIS. Com DIFERENTES, pare: o Windows receberia um binário novo.
4. Valide como no CI, no Git Bash: `sh .github/scripts/front.sh completo`.
5. Só com as nativas iguais, atualize o Windows, em `web/`: `npm.cmd install --ignore-scripts`. Ele só baixa os pacotes de JavaScript que mudaram. Depois, o `git status` não pode mostrar mudança no `package.json` nem no `package-lock.json`, e o `npm.cmd audit` não acha mais o alerta.

**Um componente novo do shadcn.** O pacote `shadcn` saiu do `package.json` em 21/10/2026: o front só usava dele o CSS, que fica em `web/src/shadcn.css`, copiado do `dist/tailwind.css` da versão 4.21.0, com o aviso de licença. A CLI dele, que copia os componentes, roda pelo `npx`, no contêiner, como no passo 2 (`npx shadcn@<versão> add <componente>`). Esse caminho ainda não foi usado. A CLI escreve os arquivos do componente e pode pôr pacotes no `package.json`, e então o lockfile passa pelos passos 2 a 5. Se ela trouxer de volta o pacote `shadcn` ou o `@import 'shadcn/tailwind.css'`, compare o CSS dela com o `web/src/shadcn.css` antes do commit.

### Backup

- Todo dia às 03:00 (horário de Brasília), o serviço `backup` grava um `pg_dump` em `./backups` (ou na pasta de `BACKUP_PASTA`, no `.env`) e apaga os que têm mais de 14 dias. **[servidor]** Escolha a pasta no servidor.
- Backup na hora: `docker compose -f docker-compose.prod.yml exec backup sh /backup/backup.sh agora`.
- Os backups têm dados pessoais: a pasta fica só no servidor, e a cópia de fora precisa ser criptografada.
- A anonimização não mexe nos backups: os dados de quem foi anonimizado continuam neles até serem apagados, em 14 dias.
- **[servidor]** Cópia fora do servidor: com o [rclone](https://rclone.org/) instalado no servidor e um remoto criptografado configurado (`rclone config`, com um remoto do tipo `crypt` por cima do Google Drive da empresa, do Backblaze B2 ou do S3), um cron diário copia a pasta. O destino ainda precisa ser escolhido. O `rclone sync` apaga do destino o que saiu da pasta, mas um destino com versões (o Backblaze B2 guarda todas por padrão, e o S3, se o versionamento estiver ligado) mantém as antigas: configure a regra de ciclo de vida para apagá-las em 14 dias, como na pasta.

  ```bash
  # crontab -e
  30 4 * * * rclone sync /opt/crm/backups crm-backup-cripto:crm
  ```

### Testar a restauração (uma vez por mês)

```bash
docker compose -f docker-compose.prod.yml exec backup sh /backup/testar-restauracao.sh
```

O script restaura o backup mais recente num banco à parte, mostra a contagem de cada tabela no backup e no banco em uso, e apaga o banco de teste. O banco em uso não é tocado. Para testar um backup específico, passe o arquivo: `.../testar-restauracao.sh /backups/crm-2026-10-01-030000.dump`.

### Restaurar de verdade

Para voltar o banco a um backup (o que entrou depois dele se perde):

```bash
docker compose -f docker-compose.prod.yml stop api
docker compose -f docker-compose.prod.yml exec backup sh -c 'dropdb crm && createdb crm && pg_restore --no-owner --exit-on-error --dbname=crm /backups/crm-AAAA-MM-DD-HHMMSS.dump'
docker compose -f docker-compose.prod.yml start api
```

Restaurar desfaz as anonimizações e as exclusões de leads de teste feitas depois do backup. A auditoria do banco restaurado também volta ao dia do backup, então faça a lista antes:

1. Em Auditoria, filtre as ações "Lead anonimizado" e "Lead de teste excluído" a partir do dia do backup e anote os leads.
2. Restaure.
3. Anonimize de novo, pela ficha, cada lead da lista. Os leads de teste que voltaram podem ser excluídos de novo.

### Trocar chaves e senhas

Os comandos rodam no servidor **[servidor]**, na pasta do CRM. As chaves e senhas novas vão direto para o `.env`, sem aparecer no terminal, a não ser quando é preciso colá-las em outro lugar.

**Chave da integração (`Integracao__Chave`).** O n8n a manda no `X-Api-Key` para enviar os leads e buscar o relatório. A troca vai em duas fases, sem nenhuma entrada recusada no meio: na fase 1, a chave de hoje passa para a `Integracao__ChaveAnterior` e continua valendo junto com a nova; depois de trocar a credencial do n8n, a fase 2 esvazia a anterior.

1. **A fase 1.** O bloco:
   - para com "PAROU", sem mudar nada, se a troca já começou (a anterior preenchida) ou se o `.env` não tem uma linha `Integracao__Chave` só, com valor;
   - passa a chave de hoje para a anterior pelo próprio `sed`, com qualquer caractere que ela tenha, e grava a nova, em hex. O fim das linhas e as permissões do `.env` ficam como estavam, e nenhuma cópia do arquivo fica para trás;
   - confere de novo antes de reiniciar a API: cada linha uma vez só, e a anterior preenchida e diferente da atual. Se algo não bater, ele para com "PAROU" sem reiniciar, e a API continua com as chaves de antes: confira o `.env` à mão;
   - não mostra as chaves: a conferência acontece dentro do `awk`, que só diz o estado.

   ```bash
   # Fase 1: a chave nova vale junto com a de hoje, que passa a ser a anterior.
   (
     # O estado do .env, sem mostrar as chaves: sem-anterior, com-anterior, iguais ou confira.
     estado() { awk -F= 'function v(l){l=substr(l,index(l,"=")+1);sub(/\r$/,"",l);sub(/[ \t]+#.*/,"",l);gsub(/^[ \t]+|[ \t]+$/,"",l);return l}
       $1=="Integracao__Chave"{n++;a=v($0)} $1=="Integracao__ChaveAnterior"{m++;p=v($0)}
       END{print (n!=1||a==""||m>1) ? "confira" : (p=="" ? "sem-anterior" : (p==a ? "iguais" : "com-anterior"))}' .env; }
     [ "$(estado)" = sem-anterior ] || { echo "PAROU: a troca ja comecou (rode a fase 2) ou o .env precisa de conferencia: $(estado)"; exit 1; }
     nova=$(openssl rand -hex 24) && [ ${#nova} -eq 48 ] || { echo "PAROU: o openssl nao gerou a chave"; exit 1; }
     cr=$(printf '\r')
     sed -b -i -e '/^Integracao__ChaveAnterior=/d' \
       -e "s/^Integracao__Chave=\([^$cr]*\)\($cr*\)\$/Integracao__ChaveAnterior=\1\2\nIntegracao__Chave=$nova\2/" .env
     [ "$(estado)" = com-anterior ] || { echo "PAROU: confira o .env a mao; a API nao reiniciou: $(estado)"; exit 1; }
     docker compose -f docker-compose.prod.yml up -d api
   )
   ```

2. **A credencial do n8n.** O comando abaixo mostra a chave nova. Copie-a e, logo depois, limpe a tela com o `clear`; nunca tire um print dela. Se você limpar a tela antes de colar, o mesmo comando mostra a chave de novo.

   ```bash
   # Mostra uma chave do .env, para colar no n8n. Depois de copiar: clear.
   nome=Integracao__Chave; awk -F= -v n="$nome" '$1==n{v=substr($0,index($0,"=")+1);sub(/\r$/,"",v);sub(/[ \t]+#.*/,"",v);gsub(/^[ \t]+|[ \t]+$/,"",v);print v}' .env
   ```

   Troque o valor na credencial `CRM - Chave de integração` do n8n (Header Auth, `X-Api-Key`). Os seis HTTP Request que chamam o CRM usam essa credencial: o envio do site, o do Google e o da Meta, o reenvio e os dois do relatório.

   Se o "Histórico da área de transferência em seus dispositivos" estiver ativado (Configurações › Sistema › Área de Transferência), o texto copiado pode ir para a nuvem da Microsoft: desative-o antes de copiar uma chave. Depois de colar, limpe a área de transferência no seu computador, e não no SSH: a chave copiada do terminal do servidor fica na área de transferência de quem copiou. O comando abaixo, no PowerShell, limpa a área de transferência e diz se ela ficou vazia, sem mostrar o que tinha; com "AINDA TEM TEXTO", rode-o de novo. No Git Bash, rode-o dentro de `powershell -c '…'`. Com o histórico da área de transferência ligado (Win+V), apague também o item da chave: os três pontos ao lado dele › Excluir.

   ```powershell
   # Limpa a area de transferencia e diz se ficou vazia, sem mostrar o que tinha.
   Set-Clipboard -Value $null; if (Get-Clipboard -Raw) { "AINDA TEM TEXTO: rode de novo" } else { "Vazia" }
   ```

3. **A conferência.** O log da API mostra cada uso da anterior e cada subida com a troca em andamento, com a hora em UTC e sem a chave:

   ```bash
   # As linhas da troca no log da API, só com a hora (UTC) e o aviso.
   docker compose -f docker-compose.prod.yml logs -t --no-log-prefix api | grep -E 'Uso da Integracao__ChaveAnterior|Troca da chave em andamento' | sed -E 's/^([^ ]+) .*(Uso da Integracao__ChaveAnterior|Troca da chave em andamento).*/\1 \2/'
   ```

   Depois que algum lead entrar pelo n8n, o último "Uso da Integracao__ChaveAnterior" precisa ser de antes da troca da credencial. Sem leads, espere: até a fase 2, as duas chaves valem.
4. **A fase 2.** O bloco esvazia a anterior e reinicia a API. Ele para com "PAROU", sem mudar nada, se o `.env` não tiver uma linha `Integracao__Chave` só, com valor. Rodado duas vezes, ele não muda o arquivo. Depois dele, a conferência do passo 3 não mostra mais o "Troca da chave em andamento", porque o `up -d` recria a API.

   ```bash
   # Fase 2: só a chave nova vale.
   (
     # O estado do .env, sem mostrar as chaves: sem-anterior, com-anterior, iguais ou confira.
     estado() { awk -F= 'function v(l){l=substr(l,index(l,"=")+1);sub(/\r$/,"",l);sub(/[ \t]+#.*/,"",l);gsub(/^[ \t]+|[ \t]+$/,"",l);return l}
       $1=="Integracao__Chave"{n++;a=v($0)} $1=="Integracao__ChaveAnterior"{m++;p=v($0)}
       END{print (n!=1||a==""||m>1) ? "confira" : (p=="" ? "sem-anterior" : (p==a ? "iguais" : "com-anterior"))}' .env; }
     case "$(estado)" in com-anterior|sem-anterior) ;; *) echo "PAROU: o .env precisa de conferencia: $(estado)"; exit 1 ;; esac
     cr=$(printf '\r')
     sed -b -i "s/^\(Integracao__ChaveAnterior=\)[^$cr]*\($cr*\)\$/\1\2/" .env
     [ "$(estado)" = sem-anterior ] || { echo "PAROU: confira o .env a mao; a API nao reiniciou: $(estado)"; exit 1; }
     docker compose -f docker-compose.prod.yml up -d api
   )
   ```

- A API sobe de novo em alguns segundos, nas duas fases, e as sessões continuam.
- Se a fase 2 vier antes da troca no n8n, as entradas recebem 401: vão para a `crm_entradas_pendentes`, com um e-mail de configuração ao admin, e o `CRM - Reenvio de pendentes` as manda depois da troca da credencial.
- Em desenvolvimento (Git Bash, na pasta do projeto), os blocos são os mesmos, com `docker compose restart api` na última linha. O `-b` do `sed` mantém o CRLF do `.env`: sem ele, o `sed` do Git Bash tira o CR de todas as linhas. O `restart` mantém o log de antes: na conferência, use `logs -t --since 2m`. A chave vai direto para a área de transferência, sem aparecer na tela; para a chave dos eventos, `nome=N8n__WebhookChave`. No PowerShell, a `Copiar-DoEnv` de "n8n local" faz o mesmo:

  ```bash
  # Em desenvolvimento: copia uma chave do .env para a area de transferencia, sem mostra-la.
  nome=Integracao__Chave; awk -F= -v n="$nome" '$1==n{v=substr($0,index($0,"=")+1);sub(/\r$/,"",v);sub(/[ \t]+#.*/,"",v);gsub(/^[ \t]+|[ \t]+$/,"",v);printf "%s",v}' .env | clip
  ```

**Nenhuma chave no log.** Para conferir, conte quantas vezes cada chave da integração aparece nos logs. As chaves saem do `.env` direto para a busca, por texto fixo, e só os números aparecem: todos precisam dar 0. O bloco mostra também quantas linhas leu de cada log. Se o Docker der erro, como sem permissão, ou se o log vier vazio, ele avisa que não conseguiu ler, e a contagem daquele log não vale. Em desenvolvimento, sem o `-f docker-compose.prod.yml` e com `api n8n` no lugar de `api caddy`.

```bash
# Quantas vezes cada chave aparece nos logs: tem que dar 0. A contagem de um log só vale com as linhas lidas.
for servico in api caddy; do
  linhas=$(docker compose -f docker-compose.prod.yml logs --no-log-prefix "$servico" | wc -l; exit "${PIPESTATUS[0]}") && [ "$linhas" -gt 0 ] ||
    { echo "$servico: NAO CONSEGUI LER O LOG (sem permissao no Docker? servico sem log?): a contagem dele nao vale"; continue; }
  echo "$servico: $linhas linhas lidas"
  for nome in Integracao__Chave Integracao__ChaveAnterior; do
    chave=$(awk -F= -v n="$nome" '$1==n{v=substr($0,index($0,"=")+1);sub(/\r$/,"",v);sub(/[ \t]+#.*/,"",v);gsub(/^[ \t]+|[ \t]+$/,"",v);printf "%s",v}' .env)
    [ -n "$chave" ] || { echo "  $nome: vazia"; continue; }
    vezes=$(docker compose -f docker-compose.prod.yml logs --no-log-prefix "$servico" | grep -cF -- "$chave"; exit "${PIPESTATUS[0]}") || vezes="NAO CONSEGUI LER O LOG"
    echo "  $nome: $vezes"
  done
done; unset chave
```

**Chave dos alertas (`N8n__WebhookChave`).** O CRM a manda no `X-Crm-Chave` para o fluxo `CRM - Alertas`. O Webhook do n8n confere uma credencial só, e por isso a troca não tem convivência: a chave nova vai primeiro para o `.env`, sem reiniciar a API, depois para a credencial, e logo em seguida a API reinicia.

1. O bloco grava a chave nova no `.env`, em hex, e confere, dentro do `awk`, que a linha `N8n__WebhookChave` aparece uma vez só e tem a chave nova. A chave chega ao `awk` pelo ambiente, e não pela linha de comando, e nada aparece na tela. Se o `sed` não trocar nada, ele para com "PAROU": não troque a credencial. O fim da linha e as permissões ficam como estavam, e nenhuma cópia do arquivo fica para trás.

   ```bash
   # Chave dos eventos: a nova vai para o .env, sem reiniciar a API.
   (
     [ "$(grep -c '^N8n__WebhookChave=' .env)" = 1 ] || { echo "PAROU: o .env precisa ter uma linha N8n__WebhookChave so"; exit 1; }
     nova=$(openssl rand -hex 24) && [ ${#nova} -eq 48 ] || { echo "PAROU: o openssl nao gerou a chave"; exit 1; }
     cr=$(printf '\r')
     sed -b -i "s/^N8n__WebhookChave=[^$cr]*\($cr*\)\$/N8n__WebhookChave=$nova\1/" .env
     [ "$(NOVA=$nova awk -F= '$1=="N8n__WebhookChave"{n++;v=substr($0,index($0,"=")+1);sub(/\r$/,"",v)} END{print (n==1&&v==ENVIRON["NOVA"]) ? "ok" : "confira"}' .env)" = ok ] || { echo "PAROU: a chave nova nao entrou no .env; confira a mao e nao troque a credencial"; exit 1; }
     echo "A chave nova esta no .env. Troque a credencial do n8n e reinicie a API logo depois."
   )
   ```

2. Mostre a chave pelo comando do passo 2 da integração, com `nome=N8n__WebhookChave`, e troque o valor na credencial `CRM - Chave dos eventos` do n8n, a do Webhook do `CRM - Alertas`. Depois, limpe a tela e a área de transferência, como na integração.
3. Logo em seguida, reinicie a API: `docker compose -f docker-compose.prod.yml up -d api`.

- Entre a troca da credencial e o `up -d`, o n8n recusa os eventos com 401 ou 403. O CRM os guarda no banco e tenta de novo depois de 1 min, 5 min, 30 min, 2 h, 6 h e 12 h, também depois de reiniciar: um evento recusado nesse intervalo sai na tentativa seguinte.
- Nada se perde se a API reiniciar em até cerca de 20 horas. Depois disso, o evento fica como falhou, e os admins recebem o aviso no sino.

**Chave do Google (`google_key`).** A Gama a cola no formulário de lead do Google Ads, e o `CRM - Entrada Google` confere o SHA-256 dela, que fica no `googleKeySha256` do nó "Configuração". Durante a troca, o fluxo aceita também o `googleKeySha256Anterior`, vazio fora dela:

1. Gere a chave nova pelo passo 5 de "n8n Cloud: importar os fluxos".
2. No "Configuração" do `CRM - Entrada Google`, passe o hash de hoje para o `googleKeySha256Anterior`, ponha o novo no `googleKeySha256` e publique o fluxo. As duas chaves valem.
3. A Gama cola a chave nova no formulário do Google Ads. Depois, limpe a área de transferência.
4. Esvazie o `googleKeySha256Anterior`, publique o fluxo de novo e peça à Gama um "Enviar dados de teste".
   - Se o teste falhar, o Google Ads ainda manda a chave antiga: ponha o hash antigo de volta no `googleKeySha256Anterior` e publique o fluxo de novo.
   - Depois que a Gama corrigir a chave no Google Ads, repita este passo.

Uma chave errada recebe 403 e só conta na `crm_entradas_recusadas` (`google_chave_errada`), sem nada do envio, e o resumo diário mostra a contagem.

**Uma chave que vazou.** A troca vai sem convivência, para a chave vazada parar de valer na hora:

- **Integração:** rode a fase 1 e, logo em seguida, a fase 2, antes de trocar a credencial no n8n. Até a troca da credencial, as entradas recebem 401 e vão para a `crm_entradas_pendentes`, com um e-mail de configuração ao admin. Depois da troca, rode o `CRM - Reenvio de pendentes`, que as manda e apaga as linhas.
- **Google:** ponha o hash novo direto no `googleKeySha256`, sem passar pela anterior, e publique o fluxo. Até a Gama colar a chave nova, os leads do Google recebem 403 e não ficam no n8n. Traga os que faltarem pela Importação, com o CSV de leads do Google Ads:
  - em "Origem", escolha Google Ads, e em "Coluna de id externo", a do id do lead. Assim, um lead que já entrou pelo webhook dá "já recebido" e não muda;
  - com outra origem, como a Planilha, quem já entrou é vinculado pelo e-mail ou pelo telefone: não nasce um lead novo, mas o lead ganha uma entrada a mais, e um lead perdido volta para a etapa inicial.
- **Alertas:** o roteiro de cima já é sem convivência.

**Senha do banco (`POSTGRES_PASSWORD`).** O PostgreSQL grava a senha quando o volume é criado: trocar só o `.env` deixa a API sem acesso ao banco. Troque no banco e no `.env`, nessa ordem:

```bash
nova=$(openssl rand -hex 16)
docker compose -f docker-compose.prod.yml exec db psql -U crm -d crm -c "ALTER USER crm WITH PASSWORD '$nova';"
cr=$(printf '\r')
sed -b -i "s/^POSTGRES_PASSWORD=[^$cr]*\($cr*\)\$/POSTGRES_PASSWORD=$nova\1/" .env
docker compose -f docker-compose.prod.yml up -d
```

- O `up -d` recria a API, as migrations, o backup e o banco com a senha nova. O CRM fica fora do ar por alguns segundos, e as sessões continuam.
- O próximo backup já usa a senha nova. Os backups antigos não dependem dela.

**Senha de um admin.** Cada um troca a própria em Trocar senha, no menu com o seu nome, no canto de cima.

- Quem esqueceu a senha pede ao outro admin: em Configurações › Usuários, no menu de ações da pessoa, Redefinir senha gera uma senha provisória.
- A sessão de quem teve a senha redefinida termina em até 1 minuto, e o sistema pede uma senha nova no próximo acesso.
- O `Admin__Email` e o `Admin__SenhaInicial` só valem com o banco sem nenhum admin: não trocam a senha de quem já existe. Por isso, mantenha os dois admins ativos.

**Senha de uma atendente.** O admin usa Redefinir senha, como acima, e passa a senha provisória a ela.

- A sessão dela termina em até 1 minuto, e o sistema pede uma senha nova no próximo acesso.
- Redefinir também libera o login bloqueado pelas 5 tentativas erradas.

**Chaves da sessão (volume `chaves-sessao`).** Elas protegem o cookie da sessão. O ASP.NET Core cria uma chave nova a cada 90 dias, sem derrubar ninguém. Para trocá-las na hora (por exemplo, se uma cópia do volume vazou):

```bash
docker compose -f docker-compose.prod.yml rm -sf api
docker volume rm crm_chaves-sessao
docker compose -f docker-compose.prod.yml up -d api
```

Todas as sessões terminam, e todo mundo entra de novo. Nada mais muda.

### Cuidados

- Nunca rode `docker compose -f docker-compose.prod.yml down -v` no servidor: o `-v` apaga os volumes, com o banco e as chaves das sessões.
- A cota do n8n Cloud: no Starter, são 2.500 execuções por mês, e as agendadas também contam.
  - Fixas: o `CRM - Reenvio de pendentes` (`0 8-20 * * 1-5`, cerca de 290 por mês), o `CRM - Resumo diário` (`0 8 * * *`, cerca de 30) e o `CRM - Relatório semanal Gama` (`0 8 * * 1`, 4 ou 5).
  - Cada lead que entra pela integração gasta uma execução da entrada e uma do `CRM - Alertas`, pelo `lead.criado`. Um `lead.reatribuido` ou um `lead.entrada_repetida` gastam mais uma cada: são os tipos do `N8n__Eventos`.
  - Com 300 leads por mês, o consumo fica perto de 930 (seção 4.5 da especificação). Com a cota esgotada, todos os fluxos param até ela renovar: acompanhe o contador de execuções da conta, sobretudo no primeiro mês.
- Logs, sem dados pessoais: `docker compose -f docker-compose.prod.yml logs -f api`. Cada contêiner guarda até 5 arquivos de 10 MB.
- O log do Caddy (`docker compose -f docker-compose.prod.yml logs caddy`) mostra as falhas do proxy, como um 502 com a API reiniciando. Ele grava a URL sem a query string, que pode levar a busca da lista, e o `Referer`, o `X-Api-Key`, o `Cookie` e o `Authorization` como `REDACTED`; o filtro fica no bloco global do [deploy/Caddyfile](deploy/Caddyfile). Para conferir no servidor, na primeira publicação, porque a API fica parada por alguns segundos:

  ```bash
  docker compose -f docker-compose.prod.yml stop api
  curl -s -o /dev/null -w '%{http_code}\n' -H 'X-Api-Key: conferencia-do-log' 'https://crm.empresa.com.br/api/leads?busca=conferencia-do-log'   # 502
  docker compose -f docker-compose.prod.yml logs caddy | grep -c conferencia-do-log                  # 0: nem a busca nem a chave
  docker compose -f docker-compose.prod.yml logs caddy | grep -c '"uri":"/api/leads?REDACTED"'       # 1 ou mais: o 502 foi gravado
  docker compose -f docker-compose.prod.yml start api
  ```
- A rede do Compose usa a faixa `172.30.0.0/24`, e o Caddy, o IP `172.30.0.10`: a API só aceita o IP de quem acessa (`X-Forwarded-For`) vindo dele. Se a faixa colidir com outra rede do servidor, defina `REDE_SUBNET` e `CADDY_IP` no `.env`.
- O Caddy manda HSTS de um ano: depois do primeiro acesso por HTTPS, o navegador não aceita mais o domínio sem HTTPS.
- Na subida, o log da API avisa que as chaves da sessão ficam sem criptografia (`No XML encryptor configured`). É esperado: elas ficam no volume `chaves-sessao`, que só quem acessa o servidor lê, e quem acessa o servidor já acessa o banco.

### Testar a publicação no próprio computador

É o mesmo Compose do plano B da demonstração (veja "Plano B" em Demonstração). Com `DOMINIO=localhost`, o Caddy usa uma autoridade local, e o navegador avisa que o certificado não é confiável: siga mesmo assim, sem instalar a autoridade (assim o HSTS do teste não vale para o `localhost`). Use outro nome de projeto, um `.env` só do teste e outras portas, para não tocar no banco de desenvolvimento, e o `PORTAS_IP=127.0.0.1`, para a rede não chegar no Caddy. No servidor, o `PORTAS_IP` fica vazio:

```bash
# ../crm-teste.env: POSTGRES_PASSWORD, Admin__Email, Admin__SenhaInicial, DOMINIO=localhost, PORTA_HTTP=8088,
# PORTA_HTTPS=8443 e PORTAS_IP=127.0.0.1, sem N8n__WebhookUrl (para não mandar e-mail)
docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env up -d --build
# abra https://localhost:8443
docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env down -v   # aqui o -v apaga só o teste
```

No Docker Desktop, a auditoria registra o gateway da rede (`172.30.0.1`) como o IP de quem acessa; num servidor Linux, aparece o IP de verdade.

## Pedido do titular (LGPD)

Para o admin, quando uma pessoa pede uma cópia dos dados dela ou para apagá-los. As atendentes passam o pedido ao admin, só com o número do lead ([docs/guia-das-atendentes.md](docs/guia-das-atendentes.md)). A LGPD dá até 15 dias, contados do pedido, para a cópia completa (art. 19, II).

### No CRM

1. **Ache o lead:** pelo número que a atendente passou (`/leads/1024`) ou em Leads, pela busca (nome, e-mail, telefone ou CPF/CNPJ).
2. **Exporte antes de anonimizar:** no cartão Privacidade da ficha, Exportar dados baixa o JSON com tudo o que o CRM guarda da pessoa. Depois da anonimização, não há volta nem o que exportar.
3. **Pedido de cópia:** entregue o JSON por um canal seguro (por exemplo, compactado com senha, e a senha por outro meio) e apague o arquivo do computador.
4. **Pedido de exclusão:** Anonimizar, no mesmo cartão.
   - O nome vira "Titular removido", e os dados pessoais saem do lead, das entradas, das interações e do histórico.
   - O painel e o relatório continuam com os mesmos números.
   - Com uma importação processando, tente de novo quando ela terminar.
5. A auditoria registra a exportação e a anonimização, com quem fez e quando, sem dados pessoais: é o registro de que o pedido foi atendido.

### Fora do CRM

O dado da pessoa também passa por lugares que o CRM não alcança:

| Onde | O que fica | O que fazer |
| --- | --- | --- |
| Execuções do n8n | Nenhuma é salva. Ficam, com o que chegou: a interrompida por uma queda do n8n, como "crashed"; a salva de propósito para investigar uma falha (veja "Investigar uma falha no n8n"); e, até a limpeza das apagadas, a cada 15 minutos, a que acabou de rodar, apagada e fora da tela (a manual, por 1 hora) | Em Executions, apague as do dia em que o lead entrou, se houver |
| Fila de pendentes do n8n (`crm_entradas_pendentes`, do Passo 6) | O que não chegou ao CRM, até o reenvio | Apague a linha da pessoa, se houver |
| Recusadas do n8n (`crm_entradas_recusadas`) | O que o CRM recusou (400) e o envio do site barrado pelo limite, com até 8 KB dos dados da pessoa, por 7 dias. A entrada acima de 64 KB e o 413 ficam sem o corpo | Apague a linha da pessoa, se houver |
| Meta e Google Ads | A Meta guarda o lead do formulário e o deixa baixar por 90 dias; o Google Ads, por 30 dias na tela (60 pela API) | Peça à Gama, que administra as contas, para apagar as cópias que tiver. A pessoa também pode pedir direto à Meta e ao Google |
| E-mails de alerta | O nome, a empresa, a origem e a campanha, na caixa da responsável e nos enviados da conta que manda os alertas | Peça à responsável para apagar os e-mails do lead e apague-os dos enviados |
| Backups | O `pg_dump` diário guarda a pessoa até ser apagado, em 14 dias, no servidor e na cópia de fora | Eles somem sozinhos em 14 dias. Não restaure um backup de antes da anonimização sem anonimizar de novo (veja "Restaurar de verdade") |
| Arquivos baixados | O JSON do titular, os CSVs exportados da lista e os CSVs importados da Meta ou do Excel, no computador de quem baixou | Apague-os depois de usar |
| Celular da atendente | A conversa do WhatsApp e o contato salvo | Peça à atendente para apagar a conversa e o contato |

Os logs do CRM não guardam dados pessoais, e o do Caddy grava as URLs sem a query string, onde fica a busca da lista (veja "Cuidados" em Publicação).
