using System.Globalization;
using System.Text;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Infra.Datas;

namespace Crm.Api.Infra.Demonstracao;

/// <summary>Uma campanha da demonstração, com a chave que a massa usa para achá-la.</summary>
public sealed record CampanhaDemonstracao(string Chave, Origem Origem, string IdExterno, string Nome);

/// <summary>Um lead da demonstração com as entradas, as interações e as mudanças de etapa dele, ainda sem o id do lead.</summary>
public sealed record LeadDemonstracao(
    Lead Lead,
    IReadOnlyList<EntradaRecebida> Entradas,
    IReadOnlyList<Interacao> Interacoes,
    IReadOnlyList<HistoricoEtapa> Historico);

/// <summary>O que a massa usa do banco.</summary>
/// <param name="Abertas">As etapas abertas ativas, na ordem do funil: a primeira é a inicial.</param>
/// <param name="Motivos">Os motivos de perda ativos.</param>
/// <param name="Atendentes">As atendentes ativas, em ordem alfabética.</param>
/// <param name="Campanhas">As campanhas da demonstração já gravadas, pela chave.</param>
public sealed record ContextoDemonstracao(
    DateTimeOffset Agora,
    IReadOnlyList<Etapa> Abertas,
    Etapa Ganho,
    Etapa Perdido,
    IReadOnlyList<MotivoPerda> Motivos,
    IReadOnlyList<Guid> Atendentes,
    IReadOnlyDictionary<string, Campanha> Campanhas);

/// <summary>
/// Massa fictícia da demonstração do painel: 60 leads nos últimos 30 dias, contados a partir de agora, em campanhas da
/// Meta e do Google e em outras origens, divididos por igual entre as atendentes e espalhados pelo funil. É a mesma a
/// cada rodada: nada aqui é sorteado.
/// </summary>
public static class MassaDemonstracao
{
    /// <summary>As campanhas da demonstração. Os nomes podem ser trocados aqui.</summary>
    public static readonly IReadOnlyList<CampanhaDemonstracao> Campanhas =
    [
        new("captacao", Origem.MetaLeadAds, "demo-meta-captacao", "Meta | Captação outubro"),
        new("remarketing", Origem.MetaLeadAds, "demo-meta-remarketing", "Meta | Remarketing"),
        new("pesquisa", Origem.GoogleAds, "demo-google-pesquisa", "Google | Pesquisa"),
        new("marca", Origem.GoogleAds, "demo-google-marca", "Google | Marca"),
    ];

    /// <summary>Onde cada lead está hoje. A ordem é a da montagem: os mais antigos no funil vêm primeiro.</summary>
    private enum Destino
    {
        NovoComContato,
        EmContato,
        Qualificado,
        Proposta,
        Ganho,
        Perdido,
        NovoSemContato,
    }

    /// <summary>
    /// Quantos leads de cada origem terminam em cada etapa, na ordem do <see cref="Destino"/>. As campanhas convertem
    /// de jeitos diferentes, para o painel ter o que mostrar: a captação e a pesquisa vendem mais que o remarketing.
    /// </summary>
    private static readonly (string Origem, int[] PorDestino)[] Distribuicao =
    [
        ("captacao", [1, 3, 2, 2, 4, 2, 1]),
        ("remarketing", [1, 2, 2, 1, 1, 2, 2]),
        ("pesquisa", [0, 2, 2, 1, 3, 1, 1]),
        ("marca", [0, 1, 1, 1, 1, 1, 1]),
        ("site", [0, 2, 1, 1, 1, 1, 2]),
        ("indicacao", [0, 1, 0, 1, 1, 0, 1]),
        ("whatsapp", [1, 0, 1, 0, 0, 1, 0]),
        ("telefone", [2, 1, 0, 0, 0, 0, 0]),
    ];

    /// <summary>Há quantos dias os leads de cada destino entraram: os mais avançados no funil são os mais antigos.</summary>
    private static readonly Dictionary<Destino, (int Menor, int Maior)> Dias = new()
    {
        [Destino.NovoComContato] = (1, 6),
        [Destino.EmContato] = (2, 14),
        [Destino.Qualificado] = (4, 18),
        [Destino.Proposta] = (6, 22),
        [Destino.Ganho] = (8, 29),
        [Destino.Perdido] = (5, 29),
    };

    /// <summary>Os leads sem contato são os mais novos: três de hoje, o último há 20 minutos.</summary>
    private static readonly int[] DiasSemContato = [0, 0, 0, 1, 1, 2, 3, 4];

    private static readonly int[] MinutosAtrasHoje = [20, 95, 190];

    /// <summary>Minutos até o primeiro contato, em rodízio: cada valor sai o mesmo número de vezes, e a mediana é 25.</summary>
    private static readonly int[] MinutosAteOContato = [4, 7, 10, 14, 18, 22, 25, 28, 35, 50, 90, 240, 1500];

    /// <summary>Por que cada perdido foi perdido, e em que etapa aberta ele estava (0 é a inicial).</summary>
    private static readonly (string Motivo, int Nivel, string? Detalhe)[] Perdas =
    [
        ("Sem resposta", 1, "Três tentativas sem retorno."),
        ("Preço", 3, "Achou acima do orçamento."),
        ("Sem resposta", 1, null),
        ("Fechou com concorrente", 3, null),
        ("Preço", 3, null),
        ("Sem interesse", 2, null),
        ("Sem resposta", 1, null),
        ("Fora do perfil", 0, "Procurava outro tipo de serviço."),
    ];

    private static readonly string[] Nomes =
    [
        "Ana Beatriz Moura", "Bruno Lopes", "Camila Fernandes", "Diego Martins", "Eduarda Ribeiro", "Felipe Cardoso",
        "Gabriela Nunes", "Henrique Barros", "Isabela Teixeira", "João Vitor Pacheco", "Karina Duarte", "Leonardo Freitas",
        "Mariana Siqueira", "Natália Campos", "Otávio Rezende", "Paula Andrade", "Rafael Monteiro", "Sabrina Lacerda",
        "Thiago Cunha", "Vanessa Prado", "William Rocha", "Yasmin Correia", "Alexandre Pinto", "Bianca Moraes",
        "Caio Albuquerque", "Débora Fontes", "Eduardo Viana", "Fernanda Xavier", "Gustavo Brandão", "Helena Macedo",
        "Igor Sampaio", "Juliana Peixoto", "Lucas Amaral", "Melissa Torres", "Nicolas Batista", "Olívia Guimarães",
        "Pedro Vidal", "Renata Coelho", "Samuel Figueiredo", "Tatiane Moreira", "Vinícius Assis", "Larissa Quintana",
        "Rodrigo Esteves", "Priscila Tavares", "Marcelo Antunes", "Letícia Bittencourt", "Fábio Cavalcanti", "Aline Medeiros",
        "Rogério Dantas", "Cristiane Paiva", "Leandro Vasconcelos", "Patrícia Nogueira", "Márcio Loureiro", "Simone Arantes",
        "Daniel Fagundes", "Jéssica Valente", "André Carvalho", "Luana Pires", "Ricardo Bastos", "Tânia Rangel",
    ];

    private static readonly string[] Empresas =
    [
        "Padaria Pão Dourado", "Clínica Bem-Estar", "Studio Corpo em Forma", "Lima Contabilidade", "Pet Shop Amigo Fiel",
        "Restaurante Sabor da Serra", "Ótica Visão Clara", "Mercado Bom Preço", "Oficina Motor Forte",
        "Escola Primeiros Passos", "Floricultura Jardim Secreto", "Construtora Base Sólida", "Academia Movimento",
        "Salão Beleza Pura", "Farmácia Saúde Já",
    ];

    private static readonly string[] Servicos = ["Orçamento", "Visita técnica", "Manutenção", "Instalação"];

    private static readonly string[] Horarios = ["Manhã", "Tarde", "Noite"];

    /// <summary>Quantas pessoas voltaram a entrar por outra campanha (entradas vinculadas).</summary>
    public const int Voltas = 5;

    public static IReadOnlyList<LeadDemonstracao> Gerar(ContextoDemonstracao contexto)
    {
        var hoje = HorarioBrasilia.Dia(contexto.Agora);
        var leads = new List<LeadDemonstracao>();
        var contatos = 0;
        var perdas = 0;
        var empresas = 0;
        var voltas = 0;

        foreach (var destino in Enum.GetValues<Destino>())
        {
            var doDestino = Distribuicao.SelectMany(d => Enumerable.Repeat(d.Origem, d.PorDestino[(int)destino])).ToList();
            for (var k = 0; k < doDestino.Count; k++)
            {
                var numero = leads.Count;
                var lead = new LeadEmMontagem(contexto, numero, doDestino[k], contexto.Atendentes[(k + (int)destino) % contexto.Atendentes.Count]);
                int? minutos = destino == Destino.NovoSemContato ? null : MinutosAteOContato[contatos++ % MinutosAteOContato.Length];
                lead.Entrar(CriadoEm(contexto.Agora, hoje, destino, k, doDestino.Count, minutos ?? 0));
                if (minutos is { } ateOContato)
                {
                    lead.Contatar(ateOContato, contatos);
                }

                switch (destino)
                {
                    case Destino.EmContato:
                        lead.Avancar(1);
                        break;
                    case Destino.Qualificado:
                        lead.Avancar(2);
                        break;
                    case Destino.Proposta:
                        lead.Avancar(3);
                        break;
                    case Destino.Ganho:
                        lead.Ganhar(Empresas[empresas++ % Empresas.Length]);
                        break;
                    case Destino.Perdido:
                        var (motivo, nivel, detalhe) = Perdas[perdas++ % Perdas.Length];
                        lead.Perder(motivo, nivel, detalhe);
                        break;
                }

                // Algumas pessoas da Meta que já estavam sendo atendidas voltaram pela pesquisa do Google.
                if (voltas < Voltas
                    && destino is Destino.EmContato or Destino.Qualificado or Destino.Proposta or Destino.Ganho
                    && lead.Origem == Origem.MetaLeadAds
                    && contexto.Agora - lead.Lead.CriadoEm > TimeSpan.FromDays(4))
                {
                    lead.Voltar(voltas++);
                }

                leads.Add(lead.Concluir());
            }
        }

        return leads;
    }

    /// <summary>
    /// Quando o lead entrou: em horário comercial, espalhado pelos dias do destino e cedo o bastante para o primeiro
    /// contato também cair no horário comercial. Os sem contato de hoje entraram há poucos minutos, sempre antes de agora.
    /// </summary>
    private static DateTimeOffset CriadoEm(DateTimeOffset agora, DateOnly hoje, Destino destino, int k, int quantidade, int minutosAteOContato)
    {
        if (destino == Destino.NovoSemContato)
        {
            var dias = DiasSemContato[k % DiasSemContato.Length];
            if (dias == 0)
            {
                return agora.AddMinutes(-MinutosAtrasHoje[k % MinutosAtrasHoje.Length]);
            }

            return HorarioComercial(hoje.AddDays(-dias), k, destino, minutosAteOContato);
        }

        var (menor, maior) = Dias[destino];
        var dia = menor + (int)Math.Round((double)k * (maior - menor) / Math.Max(1, quantidade - 1));
        return HorarioComercial(hoje.AddDays(-dia), k, destino, minutosAteOContato);
    }

    /// <summary>
    /// Uma hora entre 8h e 20h no horário de Brasília, que ainda deixa o primeiro contato antes das 20h: quem entra perto
    /// do fim do dia é atendido no dia seguinte.
    /// </summary>
    private static DateTimeOffset HorarioComercial(DateOnly dia, int k, Destino destino, int minutosAteOContato)
    {
        var horas = Math.Max(1, 12 - (int)Math.Ceiling(minutosAteOContato % (24 * 60) / 60.0));
        return HorarioBrasilia.InicioDoDia(dia)
            .AddHours(8 + ((k * 5) + ((int)destino * 3)) % horas)
            .AddMinutes(((k * 17) + ((int)destino * 11)) % 60);
    }

    /// <summary>Leva para dentro do horário comercial: antes das 8h, para as 8h; depois das 20h, para as 8h do dia seguinte.</summary>
    private static DateTimeOffset EmHorarioComercial(DateTimeOffset quando)
    {
        var local = TimeZoneInfo.ConvertTime(quando, HorarioBrasilia.Fuso);
        if (local.Hour is >= 8 and < 20)
        {
            return quando;
        }

        var dia = DateOnly.FromDateTime(local.DateTime).AddDays(local.Hour >= 20 ? 1 : 0);
        return HorarioBrasilia.InicioDoDia(dia).AddHours(8).AddMinutes(local.Minute);
    }

    /// <summary>CNPJ alfanumérico fictício, que começa com DEMO e passa no mesmo validador da API.</summary>
    public static string Cnpj(int numero)
    {
        var raiz = string.Create(CultureInfo.InvariantCulture, $"DEMO{numero:D8}");
        for (var digitos = 0; digitos < 100; digitos++)
        {
            var cnpj = raiz + digitos.ToString("D2", CultureInfo.InvariantCulture);
            if (Documentos.Valido(cnpj))
            {
                return cnpj;
            }
        }

        throw new InvalidOperationException("O CNPJ da demonstração ficou sem dígitos verificadores.");
    }

    /// <summary>"João Vitor Pacheco" vira "joao.vitor.pacheco@example.com": o domínio é reservado para exemplos.</summary>
    public static string Email(string nome)
    {
        var semAcento = string.Concat(nome.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
        return $"{semAcento.ToLowerInvariant().Replace(' ', '.')}@example.com";
    }

    /// <summary>Celular de Porto Alegre, diferente para cada lead. Está no formato certo e pode existir.</summary>
    public static string Telefone(int numero) =>
        string.Create(CultureInfo.InvariantCulture, $"+555199{(1_234_567 + (numero * 104_729)) % 10_000_000:D7}");

    /// <summary>Um lead sendo montado, do primeiro contato até onde ele está hoje.</summary>
    private sealed class LeadEmMontagem
    {
        private readonly ContextoDemonstracao _contexto;
        private readonly int _numero;
        private readonly string _chave;
        private readonly List<EntradaRecebida> _entradas = [];
        private readonly List<Interacao> _interacoes = [];
        private readonly List<HistoricoEtapa> _historico = [];

        /// <summary>A última hora de algo que aconteceu com o lead: cada passo vem depois dela.</summary>
        private DateTimeOffset _ultima;

        public LeadEmMontagem(ContextoDemonstracao contexto, int numero, string chave, Guid responsavelId)
        {
            _contexto = contexto;
            _numero = numero;
            _chave = chave;
            var nome = Nomes[numero % Nomes.Length];
            Origem = chave switch
            {
                "captacao" or "remarketing" => Origem.MetaLeadAds,
                "pesquisa" or "marca" => Origem.GoogleAds,
                "site" => Origem.Site,
                "indicacao" => Origem.Indicacao,
                "whatsapp" => Origem.Whatsapp,
                _ => Origem.Telefone,
            };
            Lead = new Lead
            {
                Nome = nome,
                Email = Email(nome),
                Telefone = Telefone(numero),
                Uf = (numero % 10) switch { 7 or 8 => "SC", 9 => "PR", _ => "RS" },
                EtapaId = contexto.Abertas[0].Id,
                ResponsavelId = responsavelId,
                Origem = Origem,
                Plataforma = Origem switch
                {
                    Origem.MetaLeadAds => numero % 2 == 0 ? "instagram" : "facebook",
                    Origem.GoogleAds => "google",
                    Origem.Site => "site",
                    _ => null,
                },
                CampanhaId = contexto.Campanhas.GetValueOrDefault(chave)?.Id,
                UtmSource = Origem == Origem.Site ? (numero % 2 == 0 ? "google" : "instagram") : null,
                UtmMedium = Origem == Origem.Site ? (numero % 2 == 0 ? "organic" : "social") : null,
                RespostasExtras = Origem == Origem.MetaLeadAds
                    ? JsonSerializer.SerializeToDocument(new Dictionary<string, string>
                    {
                        ["Qual serviço você procura?"] = Servicos[numero % Servicos.Length],
                        ["Melhor horário para contato"] = Horarios[numero % Horarios.Length],
                    })
                    : null,
            };
        }

        public Lead Lead { get; }

        public Origem Origem { get; }

        private Guid Responsavel => Lead.ResponsavelId!.Value;

        private DateTimeOffset Limite => _contexto.Agora.AddMinutes(-10);

        /// <summary>A entrada que criou o lead, processada na hora em que ele foi criado.</summary>
        public void Entrar(DateTimeOffset criadoEm)
        {
            Lead.CriadoEm = criadoEm;
            _ultima = criadoEm;
            _entradas.Add(Entrada(Origem, Lead.CampanhaId, criadoEm, SituacaoEntrada.Criado, IdExterno()));
        }

        /// <summary>O primeiro contato: a ligação, o WhatsApp ou o e-mail que a atendente registrou.</summary>
        public void Contatar(int minutos, int sequencia)
        {
            var quando = Antes(Lead.CriadoEm.AddMinutes(minutos), _contexto.Agora.AddMinutes(-5));
            var tipo = Origem switch
            {
                Origem.Whatsapp => TipoInteracao.Whatsapp,
                Origem.Telefone => TipoInteracao.Ligacao,
                _ => (sequencia % 3) switch { 0 => TipoInteracao.Ligacao, 1 => TipoInteracao.Whatsapp, _ => TipoInteracao.Email },
            };
            var descricao = tipo switch
            {
                TipoInteracao.Ligacao => "Liguei e expliquei como funciona o serviço.",
                TipoInteracao.Whatsapp => "Mandei mensagem pelo WhatsApp; respondeu pedindo orçamento.",
                _ => "Enviei as informações por e-mail.",
            };
            Interagir(tipo, descricao, quando);
            Lead.PrimeiroContatoEm = quando;
        }

        /// <summary>Leva o lead pelas etapas abertas até o nível pedido (1 é a segunda etapa), sem pular nenhuma.</summary>
        public void Avancar(int nivel)
        {
            var destino = Math.Min(nivel, _contexto.Abertas.Count - 1);
            for (var passo = 1; passo <= destino; passo++)
            {
                var quando = Proxima(passo, destino);
                Mudar(_contexto.Abertas[passo], quando);
                if (passo == 2)
                {
                    Interagir(TipoInteracao.Reuniao, "Reunião para entender a necessidade.", quando);
                }
                else if (passo == 3)
                {
                    Interagir(TipoInteracao.Email, "Proposta enviada por e-mail.", quando);
                }
            }
        }

        public void Ganhar(string empresa)
        {
            Avancar(3);
            Lead.Empresa = empresa;
            Lead.CpfCnpj = Cnpj(_numero);
            var quando = Fechamento();
            Mudar(_contexto.Ganho, quando);
            Lead.ConvertidoEm = quando;
            Interagir(TipoInteracao.Nota, "Proposta aceita. Cliente fechado!", quando);
        }

        public void Perder(string nomeDoMotivo, int nivel, string? detalhe)
        {
            Avancar(nivel);
            var motivo = _contexto.Motivos.FirstOrDefault(m => string.Equals(m.Nome, nomeDoMotivo, StringComparison.OrdinalIgnoreCase))
                ?? _contexto.Motivos[_numero % _contexto.Motivos.Count];
            var quando = Fechamento();
            Mudar(_contexto.Perdido, quando, motivo, detalhe);
            Lead.PerdidoEm = quando;
            Lead.MotivoPerdaId = motivo.Id;
            Lead.MotivoPerdaDetalhe = detalhe;
        }

        /// <summary>A pessoa voltou a entrar pela pesquisa do Google, dois dias depois: a entrada fica vinculada ao lead.</summary>
        public void Voltar(int volta)
        {
            var quando = Antes(Lead.CriadoEm.AddDays(2).AddHours(3), Limite);
            var pesquisa = _contexto.Campanhas.GetValueOrDefault("pesquisa")?.Id;
            _entradas.Add(Entrada(Origem.GoogleAds, pesquisa, quando, SituacaoEntrada.Vinculado, $"demo-volta-{volta:D2}"));
            _ultima = Max(_ultima, quando);
        }

        public LeadDemonstracao Concluir()
        {
            Lead.AtualizadoEm = _ultima;
            return new LeadDemonstracao(Lead, _entradas, _interacoes, _historico);
        }

        private string? IdExterno() =>
            Origem is Origem.MetaLeadAds or Origem.GoogleAds or Origem.Site
                ? string.Create(CultureInfo.InvariantCulture, $"demo-{_numero:D3}")
                : null;

        private EntradaRecebida Entrada(Origem origem, int? campanhaId, DateTimeOffset quando, SituacaoEntrada situacao, string? idExterno) => new()
        {
            Origem = origem,
            IdExterno = idExterno,
            CampanhaId = campanhaId,
            UtmSource = Lead.UtmSource,
            UtmMedium = Lead.UtmMedium,
            RespostasExtras = situacao == SituacaoEntrada.Criado ? Lead.RespostasExtras : null,
            Situacao = situacao,
            RecebidoEm = quando,
            ProcessadoEm = quando,
        };

        /// <summary>
        /// A hora do passo seguinte: a saída da etapa inicial vem logo depois do contato; os outros passos se espalham
        /// pelos dias seguintes, em até 10 dias.
        /// </summary>
        private DateTimeOffset Proxima(int passo, int passos) =>
            passo == 1
                ? Depois(TimeSpan.FromMinutes(2))
                : Depois(Espaco(TimeSpan.FromDays(10)) * (passo - 1) / Math.Max(passos, 3));

        /// <summary>O ganho ou a perda, depois da última etapa aberta, em até meio dia.</summary>
        private DateTimeOffset Fechamento() => Depois(Espaco(TimeSpan.FromDays(1)) / 2);

        /// <summary>O tempo que sobra depois do último acontecimento, dentro da janela e antes do limite.</summary>
        private TimeSpan Espaco(TimeSpan janela) => Antes(_ultima + janela, Limite) - _ultima;

        /// <summary>
        /// Um intervalo depois do último acontecimento, em horário comercial, sem passar do limite e sempre ao menos 1
        /// minuto depois dele.
        /// </summary>
        private DateTimeOffset Depois(TimeSpan intervalo) =>
            Max(Antes(EmHorarioComercial(_ultima + intervalo), Limite), _ultima.AddMinutes(1));

        private void Mudar(Etapa destino, DateTimeOffset quando, MotivoPerda? motivo = null, string? detalhe = null)
        {
            _historico.Add(new HistoricoEtapa
            {
                EtapaAnteriorId = Lead.EtapaId,
                EtapaNovaId = destino.Id,
                MotivoPerdaId = motivo?.Id,
                MotivoPerdaDetalhe = detalhe,
                UsuarioId = Responsavel,
                OcorridoEm = quando,
            });
            Lead.EtapaId = destino.Id;
            _ultima = Max(_ultima, quando);
        }

        private void Interagir(TipoInteracao tipo, string descricao, DateTimeOffset quando)
        {
            _interacoes.Add(new Interacao
            {
                UsuarioId = Responsavel,
                Tipo = tipo,
                Descricao = descricao,
                OcorridoEm = quando,
                CriadoEm = quando,
            });
            _ultima = Max(_ultima, quando);
        }

        private static DateTimeOffset Antes(DateTimeOffset quando, DateTimeOffset limite) => quando < limite ? quando : limite;

        private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
    }
}
