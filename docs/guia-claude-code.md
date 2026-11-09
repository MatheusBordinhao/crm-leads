# Guia: desenvolver o CRM com o Claude Code no VS Code

O CRM é construído em 12 passos (0 a 11), cada um iniciado com o comando `/passo N`. O Claude Code lê o `CLAUDE.md`, segue a `docs/especificacao.md` e usa o n8n local pelo servidor MCP `n8n-local` (README › n8n local).

## 1. Preparar o computador (uma vez)

- **VS Code** 1.94 ou mais recente, com a extensão **Claude Code**: abra Extensões, busque "Claude Code" e instale. É preciso um plano pago do Claude.
- **Git** (no Windows, o Git for Windows), **.NET 10 SDK**, **Node.js 22** ou mais recente e **Docker Desktop**. O Docker precisa ficar aberto enquanto você trabalha.

## 2. Abrir o projeto

1. Clone o repositório (`git clone <endereço do repositório>`) e abra a pasta `crm-leads` no VS Code (Arquivo > Abrir Pasta). Quando o VS Code perguntar, confie na pasta: a extensão não funciona no Modo Restrito.
2. Siga a "Primeira vez" do README: o `.env`, as chaves e as dependências.
3. Abra o Claude Code pelo ícone de faísca, no canto superior direito do editor ou na barra lateral, e entre com a sua conta Claude.
4. Conecte o n8n local:
   - suba o n8n do Compose e crie o servidor MCP `n8n-local`, como em README › n8n local (MCP da instância). Depois, o `/mcp` mostra o `n8n-local` como **Connected**;
   - o `n8n` do `.mcp.json` é o do n8n Cloud de teste, que acabou em 06/10/2026: ele aparece como **Failed** até o n8n Cloud de produção existir. Não mexa nele.

## 3. Primeira conversa: conferir o ambiente

Envie:

```text
Leia o CLAUDE.md e a docs/especificacao.md. Antes do Passo 0, confira o ambiente sem criar nem alterar arquivos:
1. versões do .NET SDK, do Node e do Docker, e se o Docker está rodando;
2. se o servidor MCP n8n-local responde (liste os fluxos e as credenciais, só leitura);
3. se o repositório Git está iniciado.
Depois resuma em até 10 linhas como você entendeu o projeto e liste as dúvidas que tiver sobre a especificação.
```

Responda às dúvidas dele antes de seguir. Se alguma resposta mudar uma regra, peça para ele atualizar a especificação.

## 4. Um passo de cada vez

Para cada passo:

1. Abra uma conversa nova: use o botão de nova conversa no topo do painel do Claude Code.
2. Clique no indicador de modo, embaixo da caixa de texto, e escolha **Plan**.
3. Envie `/passo N`, trocando N pelo número do passo.
4. Leia o plano. Peça ajustes ou aprove.
5. Aprove os comandos que ele pedir. Os de build, teste, Docker e Git (menos o push) já estão liberados em `.claude/settings.json`.
6. No fim, confira você mesmo os critérios de aceite e rode `git push`.

| Passo | Comando | Tenha antes |
| --- | --- | --- |
| 0 | `/passo 0` | Docker Desktop aberto |
| 1 | `/passo 1` | Nada |
| 2 | `/passo 2` | Nada |
| 3 | `/passo 3` | Nada |
| 4 | `/passo 4` | O e-mail que vai mandar os alertas. Para testar, serve o Gmail já conectado no n8n |
| 5 | `/passo 5` | Servidor e domínio para publicar e, se houver, um CSV exportado da Meta |
| 6 | `/passo 6` | App da Meta em modo Live, acesso de admin à página, acesso ao Google Ads e contato de quem faz o site |
| 7 | `/passo 7` | Nada |
| 8 | `/passo 8` | Nada |
| 9 | `/passo 9` | E-mail da Gama para receber o relatório |
| 10 | `/passo 10` | Nada |
| 11 | `/passo 11` | Etapas do funil definidas com a Gama; para o celular, o servidor com HTTPS |

- **Ordem:** a da seção 10 da especificação, que mudou em 05/10/2026. Depois dos passos 0 a 5, vêm os passos 7 e 8, tudo local, com a demonstração para o chefe logo depois do 8; os passos 9 e 10, enquanto o servidor não chega; e, com o servidor, a publicação que falta do Passo 5 e o Passo 6. O Passo 11 foi adiantado em 13/10/2026; dele, ficam as etapas definitivas, com a Gama, e o celular de verdade, com o servidor. A divisão do pacote por tela saiu em 09/11/2026. Desde 16/10/2026, com a reunião sem data, o trabalho seguiu em lotes, com tudo o que não depende da empresa (`CLAUDE.md` › Como trabalhar).
- **Quando faltar algo externo:** o Claude deixa o passo pronto e lista o que falta.

## 5. Prompts úteis

**Retomar um passo interrompido**
```text
Continue o Passo N de onde parou. Veja o git status e o git log para saber o que já foi feito e me diga o que falta antes de continuar.
```

**Corrigir um erro**
```text
Rodei <comando> e deu este erro: <cole o erro>. Descubra a causa antes de corrigir e me explique em duas frases.
```

**Revisar o que foi feito**
```text
Revise as mudanças deste passo como um revisor exigente: acesso de atendente a leads de outra pessoa, dados pessoais em log, testes faltando e desvios da especificação. Liste os problemas antes de corrigir.
```

**Entender o código**
```text
Me explique como funciona <parte do sistema> e onde ela está no código, para eu conseguir manter depois.
```

**Mudou uma regra**
```text
O chefe decidiu: <decisão>. Atualize a especificação (seção 11 e onde mais precisar) e depois o código, com testes.
```

**Conferir os fluxos do n8n**
```text
Mostre os fluxos do n8n criados neste passo, o que cada nó faz e o que eu preciso configurar à mão (credenciais, URLs, chaves).
```

**Conversa muito longa:** use `/compact`, ou abra uma conversa nova e use o prompt de retomar.

## 6. Cuidados

- Senhas e chaves vão no `.env`, nunca no chat. O Claude não tem permissão de ler esse arquivo.
- Revise cada fluxo no n8n antes de ativar: um fluxo ativo manda e-mail de verdade.
- Para desfazer uma alteração, passe o mouse sobre a mensagem, clique no botão de voltar e escolha **Rewind code to here**.
- Não use o modo **Bypass permissions**.
- O n8n de desenvolvimento roda no Compose, neste computador (README › n8n local). A produção vai ser o n8n Cloud, numa conta da empresa (seção 11 da especificação).
