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

4. Alertas por e-mail (pode ficar para depois). O CRM avisa o fluxo `CRM - Alertas` do n8n, que manda o e-mail. Primeiro, gere a chave que o n8n confere no cabeçalho `X-Crm-Chave` (`N8n__WebhookChave`). Os comandos a gravam no `.env` e a deixam pronta para colar no n8n.

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
   sed -i "s/^N8n__WebhookChave=.*/N8n__WebhookChave=$chave/" .env
   echo "$chave"
   ```

   No n8n, crie uma credencial Header Auth com o nome do cabeçalho `X-Crm-Chave` e a chave como valor, e escolha essa credencial no nó "Evento do CRM" do fluxo `CRM - Alertas`. Com o fluxo publicado, preencha no `.env` a URL de produção do Webhook dele:

   ```bash
   N8n__WebhookUrl=https://SUA-INSTANCIA.app.n8n.cloud/webhook/crm-alertas
   ```

   Sem `N8n__WebhookUrl`, o CRM não envia nada, e os eventos ficam pendentes. Quando a URL for configurada, os pendentes acumulados saem todos de uma vez. Para descartá-los antes, em desenvolvimento:

   ```bash
   docker compose exec db psql -U crm -d crm -c "DELETE FROM eventos_saida WHERE situacao = 'pendente';"
   ```

   O CRM chama o n8n Cloud, então não precisa de túnel para os alertas.

5. Instale as ferramentas e as dependências:

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

Com o `N8n__WebhookUrl` preenchido, a API manda ao n8n os eventos pendentes assim que sobe. Para subir sem mandar nada (para gerar os tipos do front, por exemplo):

```bash
dotnet run --project src/Crm.Api -- --N8n:WebhookUrl=
```

### Primeiro acesso

1. Abra http://localhost:5173 e entre com o `Admin__Email` e a senha inicial que o comando da primeira vez mostrou.
2. O sistema pede uma senha nova. Depois disso, a senha do `.env` não vale mais.
3. Em Configurações › Usuários, crie as atendentes. Para testar, use aliases do seu Gmail, como `matheusbordinhao11+adriana@gmail.com`. A tela gera a senha provisória para você copiar e enviar.

Se o banco não tiver nenhum admin (por exemplo, depois de `docker compose down -v`), a API só sobe com `Admin__Email` e `Admin__SenhaInicial` preenchidos no `.env`: rode de novo o passo 2 da primeira vez. Sem eles, a API para com a mensagem "Nenhum admin cadastrado".

Depois de 5 senhas erradas, o login fica bloqueado por 15 minutos. Um admin libera antes redefinindo a senha na tela de usuários.

O cookie da sessão é `Secure`. Chrome, Edge e Firefox o aceitam em `http://localhost`, mas não pelo IP da máquina: para abrir no celular, é preciso HTTPS, que vem com a [publicação](#publicação). Em desenvolvimento, confira as telas de celular com a visão de dispositivo do DevTools.

### Funil e ficha

A tela inicial é o funil: uma coluna por etapa aberta, e Ganho e Perdido como áreas de soltar, com quantos leads foram para lá nos últimos 30 dias.

- Arraste o cartão com o mouse, ou pelo teclado: foque a alça do cartão (⋮⋮), aperte espaço, escolha a etapa com as setas para a direita e para a esquerda e aperte espaço de novo. Esc cancela.
- Soltar em Perdido pede o motivo; cancelar devolve o cartão. Ganho exige CPF/CNPJ válido na ficha, a não ser que o admin desligue a exigência.
- No celular (ou na visão de dispositivo do DevTools), o funil vira uma lista por etapa, com um "Mover para" em cada cartão.
- Na ficha, os botões WhatsApp, Ligar e E-mail abrem o contato e, em seguida, o registro da interação com o tipo já escolhido. A primeira ligação, WhatsApp, e-mail ou reunião grava o primeiro contato, e a linha do tempo mostra quem fez o quê e quando.
- As etapas, os motivos de perda e a exigência de CPF/CNPJ para Ganho ficam em Configurações › Etapas e motivos (só admin).
- O admin passa o lead para outra pessoa no cartão Responsável da ficha. A troca aparece na linha do tempo e avisa a nova responsável.

### Rodízio, notificações e alertas

- Em Configurações › Usuários, o cartão Rodízio liga e desliga a distribuição automática e mostra para quem vai o próximo lead. A coluna Rodízio da lista escolhe quem participa: nas férias ou folgas, desligue a da pessoa.
- Os leads da integração, e os que um admin cadastra com "Rodízio" no campo Responsável, vão um de cada vez para quem está no rodízio. Só o rodízio passa a vez: a escolha do admin, a troca de responsável e o lead que a atendente cadastra para si não mexem na ordem.
- Sem ninguém no rodízio, ou com a distribuição desligada, o lead fica sem responsável, e os admins recebem um aviso no sino, com o motivo.
- O sino no topo mostra as notificações e é consultado a cada 30 segundos. Quem fez a ação não é avisado dela, e lead de teste não gera aviso.
- Os e-mails saem pelo n8n, em segundo plano. Se o n8n estiver fora do ar, o CRM tenta de novo depois de 1 min, 5 min, 30 min, 2 h, 6 h e 12 h; depois disso, os admins recebem um aviso no sino.

### Leads pela integração

O n8n envia os leads para `POST /api/integracoes/leads` (seção 4.2 da especificação). Para simular um envio sem o n8n, com a API rodando, use a requisição "Lead pela integração" do [src/Crm.Api/Crm.Api.http](src/Crm.Api/Crm.Api.http) e informe o valor de `Integracao__Chave` quando o VS Code pedir.

- Reenviar a mesma requisição responde `ja_recebido` e não duplica o lead. Troque o `idExterno` para simular um lead novo.
- O exemplo vai com `"teste": true`: lead de teste fica sem responsável, só aparece para admins e não gera aviso. Para ver o rodízio e o alerta por e-mail, use a requisição "Lead real pela integração".
- Os leads da integração vão para o rodízio. Os que ficam sem responsável aparecem em Leads, com o filtro Responsável › Sem responsável.
- Telefone, e-mail ou CPF/CNPJ em formato inválido não recusam o lead: a ficha mostra o aviso embaixo do campo.

### Importação de leads

Em Importação (só admin), escolha um CSV de até 5 MB e 5.000 linhas: o exportado pela Meta (UTF-16 com tab) ou uma planilha salva pelo Excel (Arquivo › Salvar como › CSV, que sai com ponto e vírgula e Windows-1252). O sistema reconhece a codificação e o separador, mostra a prévia e sugere o campo de cada coluna pelo cabeçalho.

- Confira o campo de cada coluna e as opções: a origem (o arquivo da Meta já vem como Meta), a campanha padrão (para as linhas sem campanha: uma que já existe ou uma nova), a coluna de id externo e a distribuição (rodízio ou uma pessoa).
- As linhas são processadas em segundo plano. A tela mostra o andamento, e o sino avisa quando a importação termina; quem recebeu leads também é avisado, com o total. A importação não manda alerta por e-mail lead a lead.
- Reimportar o mesmo arquivo não duplica: as linhas que já entraram aparecem como "já importadas", mesmo numa planilha sem coluna de id. Uma pessoa que já é lead é vinculada ao lead dela.
- Uma linha sem nome, ou sem e-mail e telefone, não entra e vai para o relatório de erros, com o número da linha na planilha (o cabeçalho é a linha 1). Telefone, e-mail ou CPF/CNPJ em formato inválido entram, com o aviso na ficha.
- No Excel, formate as colunas de telefone e de CPF/CNPJ como texto antes de salvar: como número, o Excel apaga os zeros do começo.
- Para testar, use os arquivos de [docs/exemplos/](docs/exemplos/): `leads-meta.csv` imita a exportação da Meta, e `leads-excel.csv`, a planilha do Excel. Os dois têm acentos, para conferir que nada sai trocado.

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

O `dotnet test` usa o Microsoft.Testing.Platform, ligado no `global.json`. O build trata avisos como erros. Os testes de integração que mudam o que vale para todos (configurações, ordem das etapas, quem está no rodízio) ficam na coleção `EstadoGlobal`, que roda sozinha depois das outras. Os testes do despachante de eventos usam um banco só deles, no mesmo contêiner, e um relógio falso.

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
                             Leads/ (leads, entrada, rodízio, normalização, campanhas, interações e linha do tempo),
                             Funil/ (etapas, motivos de perda, mudança de etapa com histórico e quadro),
                             Notificacoes/ (sino e quem é avisado de cada acontecimento), Integracoes/ (chave e rotas do n8n)
src/Crm.Api/Infra/           DbContext e migrations (Dados/), auditoria (Auditoria/), .env e configurações do sistema
                             (Configuracao/), eventos para o n8n e o despachante (Eventos/), horário de Brasília (Datas/),
                             importação CSV, com a leitura do arquivo e o processamento em segundo plano (Importacoes/),
                             ProblemDetails, validação, proxy, chaves da sessão e o front servido pela API (Web/)
tests/Crm.UnitTests/         regras de negócio, sem banco
tests/Crm.IntegrationTests/  API + PostgreSQL real (Testcontainers)
web/                         React + TypeScript + Vite: features/ por módulo (acesso, funil, leads, importacao,
                             notificacoes, configuracoes, usuarios), components/ui/ do shadcn/ui, api/ com o cliente e os
                             tipos gerados
n8n/                         JSON exportado dos fluxos do n8n (CRM - Alertas e CRM - Erros)
deploy/                      Caddyfile e scripts de backup e de teste da restauração, usados pelo docker-compose.prod.yml
docs/                        especificação, guia de uso do Claude Code e CSVs de exemplo para a importação (exemplos/)
Dockerfile                   imagem de produção: a API com o build do React e o migration bundle
```

## Publicação

O [docker-compose.prod.yml](docker-compose.prod.yml) sobe tudo num servidor com Docker: o Caddy, com HTTPS automático na frente da API; a API, que também serve o front; o PostgreSQL; as migrations, que rodam antes da API; e o backup diário. Só o Caddy expõe portas (80 e 443).

### O que o servidor precisa

- Linux com Docker Engine e o plugin Compose. Um Ubuntu 24.04 LTS com 2 GB de RAM basta.
- Portas 80 e 443 abertas para a internet: o Caddy usa a 80 para obter o certificado do Let's Encrypt.
- Um domínio ou subdomínio, como `crm.empresa.com.br`, com um registro DNS do tipo A apontando para o IP do servidor. Não crie o registro AAAA (IPv6), a não ser que o IPv6 esteja ligado no Docker: sem isso, quem acessa por IPv6 aparece com o IP interno do Docker na auditoria e no limite de logins.

### Primeira publicação

1. Clone o repositório no servidor (por exemplo, em `/opt/crm`) e entre na pasta.
2. Crie o `.env` de produção. Os comandos geram as senhas e as chaves sem mostrá-las; troque o domínio e o e-mail:

   ```bash
   cp .env.example .env && chmod 600 .env
   sed -i \
     -e "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=$(openssl rand -hex 16)/" \
     -e "s/^DOMINIO=.*/DOMINIO=crm.empresa.com.br/" \
     -e "s/^Admin__Email=.*/Admin__Email=voce@empresa.com.br/" \
     -e "s/^Admin__SenhaInicial=.*/Admin__SenhaInicial=$(openssl rand -hex 8)/" \
     -e "s/^Integracao__Chave=.*/Integracao__Chave=$(openssl rand -hex 24)/" \
     -e "s/^N8n__WebhookChave=.*/N8n__WebhookChave=$(openssl rand -hex 24)/" .env
   grep '^Admin__SenhaInicial=' .env   # a senha do primeiro acesso
   ```

   O Compose de produção monta a connection string e o `Crm__UrlPublica` (`https://` + `DOMINIO`) sozinho; as linhas deles no `.env` não são usadas. O `N8n__WebhookUrl` fica vazio até o n8n de produção existir: até lá, os eventos ficam pendentes.

3. Suba tudo. A primeira vez leva alguns minutos, por causa do build do front e da API:

   ```bash
   docker compose -f docker-compose.prod.yml up -d --build
   ```

4. Confira: `curl -I https://crm.empresa.com.br/health` responde 200, e `docker compose -f docker-compose.prod.yml ps -a` mostra `db`, `api`, `caddy` e `backup` rodando, e `migracoes` terminado com código 0.
5. Entre com o `Admin__Email` e a senha inicial: o sistema pede a senha nova. Depois, cadastre as atendentes. As linhas `Admin__*` do `.env` podem ser apagadas.

### Atualizar

```bash
docker compose -f docker-compose.prod.yml exec backup sh /backup/backup.sh agora   # um backup antes de mudar
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

O serviço `migracoes` aplica as migrations novas antes de a API subir. As sessões continuam valendo, porque as chaves que protegem o cookie ficam no volume `chaves-sessao`.

### Backup

- Todo dia às 03:00 (horário de Brasília), o serviço `backup` grava um `pg_dump` em `./backups` (ou na pasta de `BACKUP_PASTA`, no `.env`) e apaga os que têm mais de 14 dias.
- Backup na hora: `docker compose -f docker-compose.prod.yml exec backup sh /backup/backup.sh agora`.
- Os backups têm dados pessoais: a pasta fica só no servidor, e a cópia de fora precisa ser criptografada.
- Cópia fora do servidor: com o [rclone](https://rclone.org/) instalado no servidor e um remoto criptografado configurado (`rclone config`, com um remoto do tipo `crypt` por cima do Google Drive da empresa, do Backblaze B2 ou do S3), um cron diário copia a pasta. O destino ainda precisa ser escolhido.

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

### Cuidados

- Nunca rode `docker compose -f docker-compose.prod.yml down -v` no servidor: o `-v` apaga os volumes, com o banco e as chaves das sessões.
- Logs, sem dados pessoais: `docker compose -f docker-compose.prod.yml logs -f api`. Cada contêiner guarda até 5 arquivos de 10 MB.
- A rede do Compose usa a faixa `172.30.0.0/24`, e o Caddy, o IP `172.30.0.10`: a API só aceita o IP de quem acessa (`X-Forwarded-For`) vindo dele. Se a faixa colidir com outra rede do servidor, defina `REDE_SUBNET` e `CADDY_IP` no `.env`.
- O Caddy manda HSTS de um ano: depois do primeiro acesso por HTTPS, o navegador não aceita mais o domínio sem HTTPS.

### Testar a publicação no próprio computador

Com `DOMINIO=localhost`, o Caddy usa uma autoridade local, e o navegador avisa que o certificado não é confiável: siga mesmo assim, sem instalar a autoridade (assim o HSTS do teste não vale para o `localhost`). Use outro nome de projeto, um `.env` só do teste e outras portas, para não tocar no banco de desenvolvimento:

```bash
# ../crm-teste.env: POSTGRES_PASSWORD, Admin__Email, Admin__SenhaInicial, DOMINIO=localhost, PORTA_HTTP=8088 e
# PORTA_HTTPS=8443, sem N8n__WebhookUrl (para não mandar e-mail)
docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env up -d --build
# abra https://localhost:8443
docker compose -p crm-local -f docker-compose.prod.yml --env-file ../crm-teste.env down -v   # aqui o -v apaga só o teste
```

No Docker Desktop, a auditoria registra o gateway da rede (`172.30.0.1`) como o IP de quem acessa; num servidor Linux, aparece o IP de verdade.
