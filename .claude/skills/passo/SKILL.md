---
name: passo
description: Executa um passo de entrega do CRM de Leads (seção 10 de docs/especificacao.md), do plano ao commit.
argument-hint: "[número do passo, de 0 a 11]"
disable-model-invocation: true
---

Execute o Passo $ARGUMENTS da seção 10 de `docs/especificacao.md`.

1. **Contexto.** Leia o `CLAUDE.md`, o Passo $ARGUMENTS e todas as seções da especificação que ele cita. Confira pelo `git log` e pelo código se os passos anteriores estão concluídos. Se faltar algo de um passo anterior, pare e me diga o quê.
2. **Plano.** Antes de alterar qualquer arquivo, apresente o plano:
   - arquivos que vai criar ou mudar;
   - migrations, endpoints e telas;
   - pacotes novos;
   - fluxos do n8n;
   - o teste que prova cada critério de aceite.
   Espere a minha aprovação antes de seguir.
3. **Implementação.** Depois de aprovado, implemente em partes pequenas, escrevendo os testes junto com o código.
4. **Verificação.** Rode `dotnet build` e `dotnet test`. Dentro de `web/`, rode `npm run lint` e `npm test` (sem modo watch). Corrija até tudo passar.
5. **Critérios de aceite.** Confira um por um e mostre como cada um foi verificado. Os que dependem de algo externo (acesso à Meta, ao Google Ads, ao servidor, credencial de e-mail), deixe prontos e liste o que eu preciso providenciar.
6. **n8n.** Se o passo envolver fluxos, crie e valide pelo MCP do n8n e salve o JSON de cada fluxo em `n8n/`. Não publique, não ative e não execute nenhum fluxo sem me perguntar.
7. **Revisão.** Antes de fechar, confira no código e nos testes:
   - cada item de "Regras que não podem quebrar" do `CLAUDE.md`;
   - as decisões da especificação que tocam o passo: as regras das seções que ele cita, as decisões tomadas neste passo e nos anteriores, as respostas que eu dei às suas dúvidas (nesta conversa ou já registradas na especificação) e o padrão da seção 11.

   Mostre como cada uma foi respeitada: o trecho que a cumpre (arquivo e linha) e o teste que a prova. Se uma regra não puder ser provada por teste (por exemplo, comparação em tempo constante), mostre o trecho que a garante. Regra sem teste ganha o teste; regra quebrada é corrigida, com teste, antes do fechamento. Regra que não se aplica ao passo, diga em uma linha por quê. Se a revisão mudar código, rode de novo a verificação da etapa 4.
8. **Fechamento.** Atualize o README e a especificação se algo mudou. Faça os commits em português, um assunto por commit, e não faça push. Termine com um resumo curto: o que foi feito, como eu testo e o que ficou pendente.
