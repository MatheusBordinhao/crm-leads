# Especificação: CRM de Leads

Versão 1, de 28/09/2026, baseada no plano de projeto de 24/09/2026.

O CRM recebe os leads que a Gama Marketing gerar nas redes sociais e no Google, distribui entre as atendentes e acompanha cada lead no funil até virar cliente. Um painel mostra os números por campanha. O n8n faz a ponte com Meta, Google, site e e-mail; o CRM guarda os dados e aplica todas as regras.

## 1. Escopo

| Perfil | Quem | Papel |
| --- | --- | --- |
| Admin | Chefe e Matheus (a confirmar) | Vê tudo, distribui leads e configura o sistema |
| Atendente | Adriana e Pietra | Atende os próprios leads |
| Integração | n8n | Envia leads e lê relatórios, só pela API |
| Agência | Gama Marketing | Recebe o relatório por campanha, sem acesso ao sistema |

Ficam fora da primeira versão:

- Conversas de WhatsApp dentro do CRM
- Alerta por WhatsApp e notificação push com o app fechado (o n8n facilita as duas depois)
- Lembretes automáticos de retorno
- Módulo de clientes
- Preenchimento automático dos dados da empresa pelo CNPJ
- Informar à Meta quais leads viraram clientes (o evento `lead.etapa_alterada` já carrega o necessário)

## 2. Perfis e permissões

| Ação | Admin | Atendente | Integração |
| --- | --- | --- | --- |
| Ver leads | Todos | Só os seus | Não |
| Criar lead à mão | Sim, escolhe a responsável ou usa o rodízio | Sim, e o lead fica com ela | Não |
| Editar, mudar etapa, registrar interação | Todos | Só os seus | Não |
| Trocar a responsável | Sim | Não | Não |
| Importar e exportar CSV | Sim | Não | Não |
| Painel | Geral, com filtro por atendente | Só os próprios números | Não |
| Usuários, etapas, motivos de perda, campanhas, distribuição | Sim | Não | Não |
| LGPD (exportar dados do titular, anonimizar) e auditoria | Sim | Não | Não |
| Enviar leads (`POST /api/integracoes/leads`) | Não | Não | Sim |
| Relatório por campanha | Sim, pela tela | Não | Sim (`GET /api/integracoes/relatorios/campanhas`) |

- Lead de outra atendente responde 404, nunca 403, para não revelar que existe.
- Rota de admin chamada por atendente responde 403.
- A chave de integração só vale para `/api/integracoes/*`, e o cookie de usuário não vale nessas rotas.

## 3. Regras de negócio

### 3.1 Dados do lead

| Campo | Regra |
| --- | --- |
| nome | Obrigatório, até 150 caracteres |
| email | Minúsculas, sem espaços, formato validado |
| telefone | E.164 com libphonenumber, região padrão BR |
| cpf_cnpj | Só letras e números, em maiúsculas. 11 caracteres é CPF; 14 é CNPJ, numérico ou alfanumérico. Dígitos verificadores validados |
| uf | Uma das 27 siglas |
| empresa | Opcional, até 150 caracteres |

- Todo lead precisa de nome e de pelo menos um entre e-mail e telefone.
- Na tela, a validação é estrita e mostra o erro no campo.
- Na integração e na importação, formato inválido não recusa o lead. O valor é guardado como veio (normalizado quando possível) e a ficha mostra um aviso, calculado na leitura: "E-mail em formato inválido", "Telefone não reconhecido", "CPF/CNPJ inválido" ou "Sem CPF/CNPJ".
- CNPJ alfanumérico, válido desde julho de 2026: os 12 primeiros caracteres podem ser letras de A a Z ou números, e os 2 últimos são dígitos. Cada caractere vale o código ASCII menos 48 (0–9 valem 0–9, A vale 17, Z vale 42). Os pesos são 5,4,3,2,9,8,7,6,5,4,3,2 para o primeiro dígito e 6,5,4,3,2,9,8,7,6,5,4,3,2 para o segundo. Se o resto da divisão por 11 for menor que 2, o dígito é 0; senão, é 11 menos o resto. Entrada em minúsculas é convertida antes.
- Valores de teste: `12ABC34501DE35` e `11222333000181` são CNPJs válidos, `52998224725` é CPF válido, e trocar o último dígito de qualquer um deles invalida.

### 3.2 Entrada de leads

Todo lead entra pelo mesmo serviço, `ReceberEntrada`, usado pela integração, pelo cadastro manual e pela importação:

1. **Idempotência:** se já existe entrada processada com a mesma `(origem, id_externo)`, responde com o lead dela e não faz mais nada.
2. **Registro:** normaliza os dados e grava a entrada em `entradas_recebidas`, com o payload bruto.
3. **Busca:** procura um lead existente e não anonimizado, nesta ordem: CPF/CNPJ válido, e-mail e telefone normalizado. A busca e a criação rodam sob `pg_advisory_xact_lock` pela chave normalizada, para duas entradas simultâneas da mesma pessoa não criarem dois leads.
4. **Pessoa já existe:** vincula a entrada ao lead e preenche só os campos que estavam vazios. Registra a entrada na linha do tempo, notifica a responsável e gera `lead.entrada_repetida`. Se o lead estava numa etapa do tipo perdido, volta para a etapa inicial (reaberto); se estava em ganho, continua em ganho.
5. **Pessoa nova:** cria o lead na etapa inicial, com a origem e a campanha da entrada, distribui (3.3), notifica e gera `lead.criado`.
6. Tudo acontece numa transação só, incluindo o evento em `eventos_saida`.

- Origens possíveis: `meta_lead_ads`, `google_ads`, `site`, `whatsapp`, `telefone`, `indicacao`, `planilha` e `manual`. A integração aceita apenas as três primeiras.
- A origem e a campanha do lead são as da primeira entrada. Cada entrada guarda as suas.
- **Leads de teste** (`teste = true`) ficam sem responsável e visíveis só para admins. Não geram alerta e ficam fora do painel e dos relatórios. Só esses o admin pode excluir de vez.
- **Campanhas:** a tabela `campanhas` é única por `(origem, id_externo)`. Uma campanha desconhecida é criada com o nome recebido. Sem nome (o Google manda só o id), recebe o nome provisório "Google Ads · campanha {id}", que o admin pode renomear. Um nome que chegue depois só substitui o provisório.

### 3.3 Distribuição

- A configuração `distribuicao_automatica` vem ligada por padrão.
- Ligada, o próximo lead vai para o usuário ativo com `participa_rodizio = true` que recebeu lead há mais tempo: menor `ultimo_lead_atribuido_em`, com nulos primeiro e empate decidido pelo id. A escolha roda sob um `pg_advisory_xact_lock` global, para dois leads simultâneos não caírem na mesma pessoa.
- Se ninguém participa do rodízio ou a distribuição está desligada, o lead fica sem responsável e os admins são notificados.
- Lead criado à mão por atendente fica com ela. Lead criado por admin vai para quem o admin escolher, ou para o rodízio.
- Só admin troca a responsável. A troca notifica a nova responsável, gera `lead.reatribuido` e entra na auditoria.
- Nas férias ou folgas, o admin desmarca `participa_rodizio` da pessoa.

### 3.4 Funil

- Cada etapa tem nome, ordem, tipo (`aberta`, `ganho` ou `perdido`) e situação (ativa ou não).
- Etapas iniciais: Novo, Em contato, Qualificado e Proposta enviada (abertas), Ganho (ganho) e Perdido (perdido).
- A etapa inicial é a etapa aberta ativa de menor ordem.
- Existe sempre exatamente uma etapa ativa do tipo ganho e uma do tipo perdido.
- Etapa com leads não pode ser desativada. A tela oferece mover todos os leads para outra etapa antes.
- O lead pode ir para qualquer etapa, não só para a seguinte.
- **Perdido** exige um motivo da lista configurável, com detalhe opcional. Lista inicial: Sem resposta, Sem interesse, Preço, Fechou com concorrente, Fora do perfil e Contato inválido.
- **Ganho** grava `convertido_em`. Com `exigir_documento_para_ganho` ligada (padrão), exige CPF/CNPJ válido.
- Ao sair de Ganho ou Perdido, `convertido_em`, `perdido_em` e o motivo são limpos; o histórico guarda tudo.
- Toda mudança grava `historico_etapas` (de, para, quem e quando) e gera `lead.etapa_alterada`.

### 3.5 Interações e primeiro contato

- Tipos: `nota`, `ligacao`, `whatsapp`, `email` e `reuniao`. Cada interação tem tipo, descrição e `ocorrido_em`, que vem com a hora atual e aceita datas passadas, nunca futuras.
- Interações não são editadas nem excluídas: uma correção vira nova nota. A exceção é a anonimização da LGPD.
- `primeiro_contato_em` é gravado uma única vez: na primeira ligação, WhatsApp, e-mail ou reunião registrada, ou na primeira saída da etapa inicial, o que acontecer antes.
- A ficha tem botões de WhatsApp (`https://wa.me/<número sem o +>`), Ligar (`tel:`) e E-mail (`mailto:`). Depois do clique, abre o registro de interação com o tipo já preenchido.
- A linha do tempo da ficha junta entradas recebidas, interações, mudanças de etapa e trocas de responsável, da mais recente para a mais antiga.

### 3.6 Notificações

- No sistema, pela tabela `notificacoes` e pelo SignalR:
  - novo lead atribuído a você;
  - lead reatribuído para você;
  - um lead seu recebeu nova entrada;
  - (admin) lead ficou sem responsável;
  - (admin) evento para o n8n falhou de vez.
- Um sino no topo mostra o contador e a lista, com opção de marcar uma ou todas como lidas.
- Com a aba aberta em segundo plano, o navegador mostra uma notificação nativa. A permissão é pedida uma vez.
- Até o Passo 7, a lista se atualiza consultando a API a cada 30 segundos.
- Os e-mails de alerta saem do n8n, a partir dos eventos (seção 4).

### 3.7 Importação e exportação

- **Importação (admin):** arquivo CSV de até 5 MB e 5.000 linhas. O sistema detecta a codificação (UTF-8, UTF-16 ou Windows-1252) e o separador (vírgula, ponto e vírgula ou tab).
- O fluxo da tela é:
  1. enviar o arquivo;
  2. ver a prévia das 10 primeiras linhas;
  3. mapear as colunas, com sugestão pelo cabeçalho (nome/full_name, email/e-mail, telefone/phone_number/celular e assim por diante);
  4. escolher a origem, a campanha padrão, a coluna de id externo (opcional) e a distribuição (rodízio ou uma pessoa);
  5. processar.
- Cada linha passa pelo `ReceberEntrada`. Por padrão, a importação não notifica nem gera evento por lead. No fim, o admin recebe um resumo e cada responsável recebe uma notificação com quantos leads ganhou.
- O resultado mostra quantos leads foram criados, vinculados a leads existentes, já importados antes e com erro (com linha e motivo). O relatório de erros pode ser baixado.
- **Exportação (admin):** CSV dos leads com os filtros da lista. Cada exportação entra na auditoria.
- **Relatório por campanha:** CSV sem dados pessoais (seção 8).

### 3.8 LGPD

- São dados pessoais: nome, e-mail, telefone, CPF/CNPJ, empresa quando identifica a pessoa, respostas extras, payload bruto das entradas e descrição das interações.
- O admin pode exportar todos os dados de um lead em JSON e anonimizá-lo. Anonimizar faz o seguinte:
  - troca o nome por "Titular removido";
  - apaga e-mail, telefone, CPF/CNPJ, empresa, respostas extras, payload bruto das entradas e descrições das interações;
  - mantém etapa, datas, origem, campanha e responsável, para as métricas continuarem certas;
  - grava `anonimizado_em` e o registro na auditoria.
- A anonimização é irreversível e pede confirmação.
- A auditoria registra:
  - login, com sucesso ou falha;
  - criação e edição de lead, só com os nomes dos campos alterados, sem os valores;
  - troca de responsável;
  - exportações e anonimizações;
  - exclusão de lead de teste;
  - mudanças em usuários e configurações.
- No n8n, os fluxos `CRM - ` ficam com "salvar execuções de produção com sucesso" desligado, para não guardar dados pessoais sem necessidade.

## 4. Integração com o n8n

### 4.1 Quem faz o quê

```mermaid
flowchart LR
    FB[Facebook e Instagram<br/>Lead Ads] --> E[n8n: fluxos de entrada]
    GA[Google Ads<br/>formulário] --> E
    SITE[Formulário do site] --> E
    E -->|POST /api/integracoes/leads| API[CRM: API]
    API --> DB[(PostgreSQL)]
    API --> OUT[eventos_saida]
    OUT -->|POST com X-Crm-Chave| AL[n8n: CRM - Alertas]
    AL --> MAIL[E-mail da responsável]
```

O n8n recebe dos canais externos e entrega ao CRM já no formato da seção 4.2. Os eventos do CRM voltam para o n8n, que manda os alertas.

| Fica no CRM | Fica no n8n |
| --- | --- |
| Dados, regras, permissões, funil e métricas | Receber leads da Meta, do Google e do site |
| Deduplicação, rodízio e notificações no sistema | Mandar e-mails (e, depois, WhatsApp) a partir dos eventos |
| Fila de eventos com reenvio | Guardar os leads quando o CRM estiver fora do ar e reenviar depois |
| Relatórios com dados agregados | Enviar o relatório para a Gama |

O formulário do site passa pelo n8n porque o navegador não pode guardar a chave do CRM. O n8n recebe o envio público e acrescenta a chave.

### 4.2 Entrada: `POST /api/integracoes/leads`

- Autenticação pelo cabeçalho `X-Api-Key`, comparado em tempo constante (`CryptographicOperations.FixedTimeEquals`).
- Limite de 120 requisições por minuto.

```json
{
  "origem": "meta_lead_ads",
  "idExterno": "1234567890123456",
  "recebidoEm": "2026-10-05T14:03:00Z",
  "teste": false,
  "nome": "Maria Souza",
  "email": "maria@empresax.com.br",
  "telefone": "+5551999998888",
  "cpfCnpj": null,
  "uf": "RS",
  "empresa": "Empresa X",
  "plataforma": "instagram",
  "campanha": { "id": "120210000000001", "nome": "Outubro | Serviço Y" },
  "conjunto": { "id": "120210000000002", "nome": "RS | 25-55" },
  "anuncio": { "id": "120210000000003", "nome": "Vídeo 1" },
  "formulario": { "id": "987654321", "nome": "Orçamento" },
  "utm": { "source": "instagram", "medium": "paid", "campaign": "outubro-servico-y", "content": null, "term": null },
  "gclid": null,
  "respostasExtras": { "Qual serviço você procura?": "Serviço Y" }
}
```

| Campo | Obrigatório | Observação |
| --- | --- | --- |
| origem | Sim | `meta_lead_ads`, `google_ads` ou `site` |
| idExterno | Sim | Id do lead na Meta ou no Google (`lead_id`). No site, o n8n gera um UUID por envio |
| recebidoEm | Sim | ISO 8601 |
| teste | Não | Padrão `false` |
| nome | Sim | |
| email, telefone | Pelo menos um | |
| cpfCnpj, uf, empresa | Não | |
| plataforma | Não | `facebook`, `instagram`, `google` ou `site` |
| campanha, conjunto, anuncio, formulario | Não | Objetos `{ id, nome }`. O Google manda só o id da campanha. `conjunto` é o conjunto de anúncios da Meta ou o grupo de anúncios do Google |
| utm, gclid | Não | |
| respostasExtras | Não | Objeto com pergunta e resposta |

| Resposta | Corpo | O que o n8n faz |
| --- | --- | --- |
| 201 | `{ "leadId": 1024, "situacao": "criado" }` | Segue |
| 200 | `{ "leadId": 1024, "situacao": "vinculado" }`, quando a pessoa já existia | Segue |
| 200 | `{ "leadId": 1024, "situacao": "ja_recebido" }`, quando a mesma origem e o mesmo idExterno já foram processados | Segue |
| 400 | ProblemDetails com os erros | Não reenvia e avisa o admin |
| 401 | Vazio | Não reenvia e avisa o admin |
| 429, 5xx ou tempo esgotado | Vazio | Tenta de novo e, se não der, guarda na fila de pendentes |

A resposta 400 vale só para problemas de estrutura: falta de nome, falta de e-mail e telefone ao mesmo tempo, origem inválida ou falta de idExterno. Formatos inválidos são aceitos com aviso (3.1).

### 4.3 Saída: eventos do CRM para o n8n

- Tipos: `lead.criado`, `lead.entrada_repetida`, `lead.reatribuido` e `lead.etapa_alterada`.
- Só são gravados em `eventos_saida` os tipos listados em `N8n__Eventos`, porque cada envio consome uma execução do plano do n8n. Padrão: `lead.criado,lead.reatribuido,lead.entrada_repetida`.
- A entrega é um `POST` para `N8n__WebhookUrl` com os cabeçalhos `X-Crm-Chave` (valor de `N8n__WebhookChave`, conferido pelo Webhook do n8n com Header Auth), `X-Crm-Evento-Id` e `Content-Type: application/json`. O tempo limite é de 10 segundos, e qualquer resposta 2xx conta como entregue.
- Em caso de falha, o despachante tenta de novo depois de 1 min, 5 min, 30 min, 2 h, 6 h e 12 h. Depois disso o evento fica como `falhou` e os admins são notificados.
- A entrega é "pelo menos uma vez", sem garantia de ordem.
- Sem `N8n__WebhookUrl` configurada, o despachante não envia nada e os eventos ficam pendentes.
- O evento não leva e-mail, telefone nem CPF/CNPJ do lead. Quem precisa desses dados abre a ficha pelo link.

```json
{
  "id": "0f8b1c3e-2d4a-4b7e-9f1a-6c5d8e7f9a0b",
  "tipo": "lead.criado",
  "ocorridoEm": "2026-10-05T14:03:12Z",
  "lead": {
    "id": 1024,
    "nome": "Maria Souza",
    "empresa": "Empresa X",
    "uf": "RS",
    "origem": "meta_lead_ads",
    "plataforma": "instagram",
    "campanha": "Outubro | Serviço Y",
    "idExterno": "1234567890123456",
    "etapa": { "id": 1, "nome": "Novo", "tipo": "aberta" },
    "url": "https://crm.exemplo.com.br/leads/1024"
  },
  "responsavel": { "id": "5b1e…", "nome": "Adriana", "email": "adriana@empresa.com.br" },
  "dados": {}
}
```

O campo `dados` muda conforme o tipo:

| Tipo | `dados` |
| --- | --- |
| `lead.criado` | Vazio. `responsavel` vem `null` quando o lead fica sem responsável |
| `lead.entrada_repetida` | `{ "origem", "campanha" }` da nova entrada |
| `lead.reatribuido` | `{ "responsavelAnterior": { "id", "nome" } }` |
| `lead.etapa_alterada` | `{ "etapaAnterior": { "id", "nome", "tipo" }, "motivoPerda": "..." }` |

### 4.4 Relatório: `GET /api/integracoes/relatorios/campanhas?de=2026-10-01&ate=2026-10-31`

Usa a mesma chave da entrada. Devolve uma linha por campanha com origem, campanha, leads novos, entradas, em andamento, ganhos, perdidos, taxa de conversão e mediana do primeiro contato em minutos, com as definições da seção 8. Não leva dados pessoais.

### 4.5 Fluxos do n8n

| Fluxo | Gatilho | O que faz | Passo |
| --- | --- | --- | --- |
| `CRM - Alertas` | Webhook `POST` com Header Auth | Recebe os eventos e manda e-mail para a responsável com nome, empresa, origem, campanha e o link da ficha. Sem responsável, avisa o e-mail de admin configurado no fluxo | 4 |
| `CRM - Erros` | Error Trigger | Manda e-mail para o admin com o nome do fluxo e o link da execução. É o fluxo de erro de todos os fluxos `CRM - ` | 4 |
| `CRM - Entrada Meta` | Facebook Lead Ads Trigger | Monta o JSON da seção 4.2 (`idExterno` é o id do lead na Meta) com campanha, conjunto, anúncio, formulário e plataforma, e envia ao CRM | 6 |
| `CRM - Entrada Google` | Webhook `POST` | Confere o `google_key` e mapeia `user_column_data` pelo `column_id` (FULL_NAME, ou FIRST_NAME e LAST_NAME, EMAIL, PHONE_NUMBER, COMPANY_NAME, REGION). Usa `lead_id` como idExterno, `campaign_id` como campanha, `gcl_id` como gclid e `is_test` como teste, e responde 200 com `{}` | 6 |
| `CRM - Entrada Site` | Webhook `POST`, com CORS liberado só para o domínio do site | Descarta envios com o campo isca (honeypot) preenchido, gera o idExterno (UUID), leva as UTMs e envia ao CRM | 6 |
| `CRM - Reenvio de pendentes` | Schedule de hora em hora, das 8h às 20h, de segunda a sexta | Reenvia o que está na Data Table `crm_entradas_pendentes` e apaga a linha quando dá certo | 6 |
| `CRM - Relatório semanal Gama` | Schedule toda segunda às 8h | Busca o relatório da semana anterior (4.4) e envia para a Gama por e-mail ou planilha | 9 |

- **Chave do CRM:** fica numa credencial Header Auth (`X-Api-Key`) usada pelo HTTP Request.
- **Falhas no envio ao CRM:** o HTTP Request tenta 3 vezes, a cada 5 segundos. Se ainda falhar com 429, 5xx ou tempo esgotado, o payload vai para `crm_entradas_pendentes` (colunas: payload, tentativas, ultimo_erro, criado_em). Erros 400 e 401 não vão para a fila; disparam o `CRM - Erros`.
- **Meta:**
  - O app da Meta usado pela credencial do n8n precisa estar em modo Live, o que exige a URL da política de privacidade. Também precisa de acesso avançado a `public_profile` e da verificação da empresa concluída.
  - Quem autoriza a credencial precisa ser admin da página.
  - A Meta aceita um único webhook por app, então alternar entre a URL de teste e a de produção do n8n desliga a outra. Teste direto na URL de produção, com a Ferramenta de Teste de Anúncios de Cadastro. Os leads dessa ferramenta devem chegar com `teste: true`.
  - Se o acesso a leads for restrito nas configurações do negócio (Integrações > Acesso a leads), libere o app lá.
- **Google:** a Gama cola a URL do webhook e a chave no formulário de lead do Google Ads. O botão "Enviar dados de teste" manda `is_test: true`.
- **Orçamento de execuções:** com 300 leads por mês, o consumo fica perto de 900 execuções: cerca de 300 de entrada, 300 de alerta, 290 de reenvio e 4 de relatório. Isso cabe no plano Starter, que tem 2.500 por mês.
- **Instâncias:**
  - `https://matheusbordinhao.app.n8n.cloud` é a instância de desenvolvimento, numa conta pessoal. A credencial do Gmail que já existe nela serve para os alertas de teste.
  - Produção precisa rodar numa conta da empresa: n8n Cloud pago ou n8n Community no mesmo servidor do CRM, que é gratuito mas exige manutenção nossa.
  - Os JSONs em `n8n/` levam os fluxos de uma instância para outra; as credenciais são recriadas no destino.

## 5. Modelo de dados

Nomes em snake_case. O banco usa a extensão `unaccent` para a busca sem acento.

| Tabela | Colunas principais | Observações |
| --- | --- | --- |
| usuarios (Identity) | Campos do Identity, mais nome, ativo, participa_rodizio, ultimo_lead_atribuido_em, trocar_senha, criado_em | Id uuid. Admin e Atendente são roles, na tabela `perfis`. As outras tabelas do Identity também têm nomes em português (`usuarios_perfis`, `usuarios_claims`, `usuarios_logins`, `usuarios_tokens`, `perfis_claims`) |
| etapas | id, nome, ordem, tipo, ativa | Tipo: `aberta`, `ganho` ou `perdido` |
| motivos_perda | id, nome, ativo | |
| campanhas | id, origem, id_externo, nome, nome_provisorio | Única por (origem, id_externo) |
| leads | id (bigint), nome, email, telefone, cpf_cnpj, uf, empresa, etapa_id, responsavel_id, origem, plataforma, campanha_id, utm_source, utm_medium, utm_campaign, utm_content, utm_term, gclid, respostas_extras (jsonb), motivo_perda_id, motivo_perda_detalhe, teste, criado_em, atualizado_em, primeiro_contato_em, convertido_em, perdido_em, anonimizado_em | Índices em email, telefone, cpf_cnpj, (responsavel_id, etapa_id), campanha_id e criado_em. Concorrência otimista pelo `xmin` |
| entradas_recebidas | id, lead_id, origem, id_externo, campanha_id, conjunto, anuncio e formulario (jsonb, com id e nome), utm_*, gclid, respostas_extras, payload_bruto (jsonb), situacao, erro, teste, recebido_em, processado_em | Situação: `criado`, `vinculado` ou `erro`. Índice único parcial em (origem, id_externo) onde a situação não é `erro` |
| historico_etapas | id, lead_id, etapa_anterior_id, etapa_nova_id, usuario_id, ocorrido_em | |
| interacoes | id, lead_id, usuario_id, tipo, descricao, ocorrido_em, criado_em | |
| notificacoes | id, usuario_id, tipo, titulo, lead_id, lida_em, criado_em | |
| eventos_saida | id (uuid), tipo, payload (jsonb), situacao, tentativas, proxima_tentativa_em, entregue_em, ultimo_erro, criado_em | Situação: `pendente`, `entregue` ou `falhou`. Índice em (situacao, proxima_tentativa_em) |
| auditoria | id, usuario_id, acao, entidade, entidade_id, detalhes (jsonb, sem dados pessoais), ip, ocorrido_em | As trocas de responsável da linha do tempo vêm daqui. No login com e-mail desconhecido, usuario_id fica nulo e o e-mail digitado não é gravado |
| configuracoes | chave, valor | `distribuicao_automatica`, `exigir_documento_para_ganho` |

## 6. API

Os erros seguem o padrão ProblemDetails, com mensagens em português. A documentação OpenAPI fica disponível em desenvolvimento.

| Método e rota | Quem | O que faz |
| --- | --- | --- |
| `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/eu`, `POST /api/auth/trocar-senha` | Todos | Acesso |
| `GET /api/leads` | Admin e atendente | Lista com busca, filtros (etapa, responsável, origem, campanha, período) e paginação |
| `GET /api/leads/{id}` | Admin e atendente | Ficha, com os avisos da seção 3.1 |
| `POST /api/leads`, `PUT /api/leads/{id}` | Admin e atendente | Criar e editar |
| `POST /api/leads/{id}/etapa` | Admin e atendente | Mudar etapa, com motivo quando for Perdido |
| `POST /api/leads/{id}/responsavel` | Admin | Trocar responsável |
| `GET /api/leads/{id}/linha-do-tempo`, `POST /api/leads/{id}/interacoes` | Admin e atendente | Linha do tempo e interações |
| `POST /api/importacoes`, `GET /api/importacoes/{id}` | Admin | Importação CSV e resultado |
| `GET /api/leads/exportacao` | Admin | CSV com os filtros da lista |
| `GET /api/leads/{id}/dados-titular`, `POST /api/leads/{id}/anonimizar` | Admin | LGPD |
| `DELETE /api/leads/{id}` | Admin | Só para leads de teste |
| `/api/etapas`, `/api/motivos-perda`, `/api/campanhas`, `/api/usuarios`, `/api/configuracoes` | Admin | Cadastros e configurações |
| `GET /api/notificacoes`, `POST /api/notificacoes/{id}/lida`, `POST /api/notificacoes/lidas` | Admin e atendente | Notificações do próprio usuário |
| `GET /api/painel` | Admin e atendente | Métricas da seção 8 |
| `GET /api/relatorios/campanhas` | Admin | Relatório por campanha, também em CSV |
| `GET /api/auditoria` | Admin | Consulta da auditoria |
| `POST /api/integracoes/leads`, `GET /api/integracoes/relatorios/campanhas` | Integração | Seções 4.2 e 4.4 |
| `/hubs/notificacoes` | Admin e atendente | Hub do SignalR, com um grupo por usuário e um grupo de admins |
| `GET /health` | Público | Monitoramento |

## 7. Telas

- **Login e troca de senha.**
- **Funil:** quadro com uma coluna por etapa aberta e Ganho e Perdido como áreas de soltar, mostrando os últimos 30 dias. Cada cartão mostra nome, empresa, origem, tempo desde a entrada e, para o admin, a responsável. Tem filtros por responsável (admin), origem, campanha e busca. No celular, vira uma lista por etapa com seletor, sem arrastar.
- **Leads:** tabela com busca sem acento (nome, e-mail, telefone, empresa, documento), filtros e ordenação. No celular, cartões.
- **Ficha do lead:** dados editáveis, avisos, botões de contato, etapa, responsável (admin), linha do tempo e registro de interação.
- **Novo lead.**
- **Painel** (seção 8).
- **Notificações:** sino com contador no topo de todas as telas.
- **Importação** (admin).
- **Configurações** (admin): usuários, etapas, motivos de perda, campanhas e distribuição.
- **Auditoria** (admin).

Tudo funciona a partir de 360 px de largura. No celular, precisam funcionar bem a lista, a ficha, a mudança de etapa, o registro de interação e as notificações. O visual é neutro, com espaço para o logo da empresa.

## 8. Painel e relatório

- O período padrão são os últimos 30 dias, no horário de Brasília. Os filtros são período, origem, campanha e responsável (só admin).
- Leads de teste ficam fora de todos os números. Leads anonimizados continuam contando.

| Métrica | Definição |
| --- | --- |
| Leads novos | Leads criados no período |
| Entradas | Entradas recebidas no período, incluindo as de pessoas que já eram leads |
| Por origem e por campanha | Leads novos agrupados pela origem e pela campanha da primeira entrada |
| Situação atual | Leads novos do período, por etapa atual |
| Taxa de conversão | Leads novos do período que hoje estão em Ganho, divididos pelos leads novos do período |
| Tempo até o primeiro contato | Mediana de `primeiro_contato_em − criado_em` dos leads novos do período que já tiveram contato, e quantos ainda não tiveram |
| Motivos de perda | Leads que foram para Perdido no período, por motivo |
| Por atendente (admin) | Leads novos, ganhos, taxa de conversão e mediana do primeiro contato de cada atendente |

O relatório por campanha usa as mesmas definições e traz, por campanha: leads novos, entradas, em andamento, ganhos, perdidos, taxa de conversão e mediana do primeiro contato. Não leva dados pessoais.

## 9. Segurança e operação

- **Cookie:** HttpOnly, Secure, SameSite=Strict, com expiração deslizante de 12 horas.
- **Senha:** mínimo de 10 caracteres, bloqueio de 15 minutos após 5 tentativas erradas. O admin cria o usuário com senha provisória, e o sistema exige a troca no primeiro acesso: até lá, só `eu`, `logout` e `trocar-senha` funcionam, e o resto responde 403.
- **Sessão:** conferida no banco a cada minuto. Usuário desativado não entra, e quem já estava logado perde a sessão em até 1 minuto. Senha redefinida pelo admin também encerra a sessão, e um perfil trocado passa a valer na próxima conferência.
- **Primeiro admin:** criado na inicialização a partir de `Admin__Email` e `Admin__SenhaInicial`, se ainda não existir nenhum. Como a senha do `.env` é provisória, ele também precisa trocá-la no primeiro acesso. Sem nenhum admin cadastrado e sem essas variáveis, a API não sobe.
- **Limites de requisição:** 10 logins por minuto por IP e 120 requisições por minuto na integração.
- **Caddy:** HTTPS automático, HSTS e cabeçalhos de segurança.
- **Backup:** `pg_dump` diário, com 14 dias guardados e cópia fora do servidor. A restauração é testada uma vez por mês.
- **Logs e monitoramento:** logs estruturados sem dados pessoais e `/health` para monitorar.

Variáveis de ambiente (`.env.example`):

```bash
ConnectionStrings__Crm=Host=localhost;Port=5432;Database=crm;Username=crm;Password=
POSTGRES_PASSWORD=
Crm__UrlPublica=http://localhost:5173        # base dos links nos eventos
Admin__Email=                                # primeiro admin, criado na inicialização se não houver nenhum
Admin__SenhaInicial=                         # 10+ caracteres; o sistema pede a troca no primeiro acesso
Integracao__Chave=                           # n8n → CRM, 32+ caracteres aleatórios
N8n__WebhookUrl=                             # URL de produção do Webhook do fluxo CRM - Alertas
N8n__WebhookChave=                           # CRM → n8n (Header Auth), 32+ caracteres aleatórios
N8n__Eventos=lead.criado,lead.reatribuido,lead.entrada_repetida
DOMINIO=                                     # só em produção, usado pelo Caddy
```

Em desenvolvimento, o Docker Compose e a API leem o mesmo `.env` da raiz, e uma variável que já exista no ambiente tem prioridade sobre o arquivo. Em produção, as variáveis vêm do ambiente.

## 10. Passos de entrega

As datas seguem o plano de projeto. Cada passo termina com os critérios de aceite passando e com `dotnet test` e `npm test` sem erros.

### Entrega 1, até 09/10/2026: já dá para trabalhar

**Passo 0: base do projeto**

- Solução .NET 10 (`Crm.sln`) com `src/Crm.Api`, `tests/Crm.UnitTests` e `tests/Crm.IntegrationTests`.
- App React em `web/` com Vite, com proxy de `/api` e `/hubs` para a API.
- `docker-compose.yml` com PostgreSQL 17 para desenvolvimento, `.env.example` e `.gitignore`.
- EF Core com snake_case, extensão `unaccent` e primeira migration. As migrations rodam na inicialização só em desenvolvimento.
- `GET /health`, OpenAPI com Scalar em desenvolvimento, ProblemDetails e logs estruturados.
- Base dos testes de integração com Testcontainers.
- `dotnet format`, ESLint, Prettier e um README com os comandos.
- Aceite: num clone limpo, `docker compose up -d db`, `dotnet test`, `npm run dev` e `npm test` funcionam, e `/health` responde 200.

**Passo 1: usuários e acesso**

- Identity com os perfis Admin e Atendente, cookie conforme a seção 9 e primeiro admin pelas variáveis de ambiente.
- Login, logout, `eu`, troca de senha obrigatória no primeiro acesso, bloqueio por tentativas e limite de requisições no login.
- Cadastro de usuários (admin): criar, editar, desativar, redefinir senha e marcar `participa_rodizio`.
- Telas de login, troca de senha e usuários, com o layout base responsivo e o menu.
- Auditoria de login e de mudanças em usuários.
- Aceite:
  - atendente recebe 403 nas rotas de admin;
  - usuário desativado não entra;
  - a 6ª tentativa errada bloqueia o login.

**Passo 2: leads e entrada**

- Tabelas leads, entradas_recebidas, campanhas, etapas, motivos_perda e configuracoes, com os valores iniciais da seção 3.4.
- Normalização e validação da seção 3.1, com testes unitários.
- Serviço `ReceberEntrada` (3.2) e `POST /api/integracoes/leads` com a chave (4.2).
- Criar lead à mão, listar com filtros e paginação, ficha com avisos e edição, sempre com o filtro por responsável.
- Telas de lista (tabela no computador, cartões no celular), novo lead e ficha com os dados.
- Aceite:
  - o mesmo `(origem, idExterno)` enviado duas vezes gera um lead só (`ja_recebido`);
  - o mesmo e-mail vindo de outra origem vincula ao lead existente (`vinculado`), e um lead perdido volta para a etapa inicial;
  - a atendente recebe 404 no lead de outra pessoa, e a lista dela não mostra leads alheios;
  - os valores de teste da seção 3.1 passam na validação;
  - uma entrada da integração com telefone inválido é aceita, e a ficha mostra o aviso.

**Passo 3: funil e ficha completa**

- Mudança de etapa com histórico, motivo de perda e exigência de documento para Ganho (3.4).
- Interações, primeiro contato e linha do tempo (3.5).
- Configuração de etapas (criar, renomear, reordenar, desativar) e de motivos de perda.
- Quadro do funil com arrastar e soltar, filtros, botões de contato e registro de interação. No celular, a lista por etapa descrita na seção 7.
- Aceite:
  - arrastar para Perdido abre o pedido de motivo, e cancelar devolve o cartão;
  - com a configuração ligada, Ganho sem CPF/CNPJ válido é recusado com mensagem clara;
  - o histórico mostra quem mudou e quando;
  - `primeiro_contato_em` é gravado uma única vez;
  - a ficha e o registro de interação funcionam numa tela de 360 px.

**Passo 4: distribuição, notificações e alerta por e-mail**

- Rodízio e troca de responsável (3.3), com a configuração `distribuicao_automatica`.
- Notificações no sistema (3.6), atualizadas a cada 30 segundos.
- Tabela `eventos_saida` e despachante em segundo plano com reenvio (4.3).
- No n8n, os fluxos `CRM - Alertas` e `CRM - Erros`, com os JSONs salvos em `n8n/`.
- Aceite:
  - com duas atendentes no rodízio, três leads seguidos vão para A, B e A;
  - 20 entradas simultâneas não deixam diferença maior que 1 entre as duas;
  - sem ninguém no rodízio, o lead fica sem responsável e os admins são notificados;
  - com o n8n fora do ar, o evento fica pendente e é entregue quando o n8n volta;
  - o e-mail chega à responsável com o link da ficha.

**Passo 5: importação e publicação**

- Importação CSV (3.7) com mapeamento de colunas e relatório de erros.
- `docker-compose.prod.yml` com a API servindo o build do React, PostgreSQL e Caddy.
- Backup diário e guia de publicação no README.
- Publicação no servidor definido (seção 11).
- Aceite:
  - importar um CSV exportado da Meta (UTF-16, tab) e um do Excel (`;`, Windows-1252) sem erro de acentuação;
  - reimportar o mesmo arquivo não cria duplicados;
  - o sistema está no ar com HTTPS, e a restauração do backup foi testada.

### Entrega 2, até 23/10/2026: leads automáticos

**Passo 6: entrada automática pelo n8n**

- Fluxos `CRM - Entrada Meta`, `CRM - Entrada Google`, `CRM - Entrada Site` e `CRM - Reenvio de pendentes` (4.5).
- Credencial Header Auth com a chave do CRM e a Data Table `crm_entradas_pendentes`.
- Tela de campanhas para o admin renomear as provisórias.
- Este passo depende do app da Meta em modo Live. Até lá, a importação CSV cobre os leads da Meta.
- Aceite:
  - um lead da Ferramenta de Teste da Meta e um do "Enviar dados de teste" do Google aparecem no CRM com origem e campanha, marcados como teste;
  - reenviar o mesmo lead não duplica;
  - com o CRM fora do ar, o lead vai para a fila e entra no CRM no reenvio seguinte.

**Passo 7: tempo real**

- Hub SignalR autenticado, com um grupo por usuário e um grupo de admins.
- Notificação nativa do navegador.
- Quadro e lista se atualizam sozinhos, e a consulta a cada 30 segundos sai.
- Aceite:
  - um lead novo aparece para a responsável em até 5 segundos, sem recarregar a página;
  - a atendente não recebe eventos de leads de outra pessoa.

**Passo 8: painel**

- Métricas da seção 8, com filtros. A atendente vê só os próprios números, e o admin filtra por atendente.
- Aceite:
  - um teste de integração com uma massa de dados conhecida confere cada número;
  - os leads de teste ficam de fora.

### Entrega 3, até 06/11/2026: ajustes e relatório

**Passo 9: relatório para a Gama**

- Tela e CSV do relatório por campanha.
- `GET /api/integracoes/relatorios/campanhas` e o fluxo `CRM - Relatório semanal Gama`.
- Aceite:
  - os números do CSV batem com os do painel;
  - o relatório não tem nenhum dado pessoal.

**Passo 10: LGPD e auditoria**

- Exportar os dados do titular, anonimizar e excluir leads de teste.
- Exportação de leads com registro na auditoria e tela de auditoria.
- Revisão das configurações de execução dos fluxos no n8n.
- Aceite:
  - depois de anonimizar, nenhum dado pessoal do lead aparece no banco, incluindo entradas e interações;
  - o painel mantém os totais.

**Passo 11: acabamento**

- Etapas definitivas, combinadas com a Gama.
- Instalação como app no celular (PWA) e revisão da usabilidade no celular.
- README de operação: publicar, fazer e restaurar backup, trocar as chaves.
- Roteiro de treinamento de uma página para Adriana e Pietra.
- Aceite: as atendentes conseguem, sozinhas, receber um lead, registrar o contato e levá-lo até Ganho ou Perdido.

## 11. Decisões em aberto

Cada decisão tem um padrão, para o desenvolvimento não parar enquanto ela não sai.

| Decisão | Padrão adotado | Quem decide |
| --- | --- | --- |
| Rodízio automático ou distribuição pelo admin | Rodízio, configurável | Chefe |
| Exigir CPF/CNPJ para marcar Ganho | Sim, configurável | Chefe |
| Etapas definitivas do funil | As da seção 3.4 | Gama e chefe |
| Quem é admin | Chefe e Matheus | Chefe |
| A Gama acessa o painel? | Não; recebe o relatório | Chefe |
| Onde o CRM roda e qual o domínio | Ambiente local até a definição | Chefe |
| n8n de produção | Conta da empresa: n8n Cloud Starter ou n8n Community no servidor do CRM | Chefe |
| E-mail que envia os alertas | Gmail da conta de desenvolvimento nos testes; e-mail da empresa em produção | Chefe e TI |
| URL da política de privacidade | Nenhum; a Meta exige para o app ficar Live | Empresa |
| Quem faz o site e o formulário | Nenhum | Chefe |
| Prazo para anonimizar leads perdidos | Sugestão: 24 meses sem atividade, com anonimização manual até a decisão | Chefe |

## Fontes

Consultadas em 28/09/2026.

- [Google Ads: formato do webhook de formulário de lead](https://developers.google.com/google-ads/webhook/docs/implementation)
- [n8n: credenciais do Facebook Lead Ads](https://docs.n8n.io/integrations/builtin/credentials/facebookleadads)
- [n8n: Facebook Lead Ads Trigger](https://docs.n8n.io/integrations/builtin/trigger-nodes/n8n-nodes-base.facebookleadadstrigger)
- [n8n: conectar o Claude Code ao servidor MCP](https://docs.n8n.io/connect/connect-to-n8n-mcp-server/mcp-client-examples)
- [n8n: planos e contagem de execuções](https://n8n.io/pricing/)
- [Receita Federal: perguntas e respostas sobre o CNPJ alfanumérico](https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/perguntas-e-respostas/cnpj/cnpj-alfanumerico.pdf)
- [Meta: leads ficam disponíveis por 90 dias](https://en-gb.facebook.com/business/help/1526849577619206)
