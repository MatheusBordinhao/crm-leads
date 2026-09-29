using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Crm.Api.Infra.Dados.Migrations
{
    /// <inheritdoc />
    public partial class FunilEConfiguracoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoes",
                columns: table => new
                {
                    chave = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    valor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes", x => x.chave);
                });

            migrationBuilder.CreateTable(
                name: "etapas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_etapas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "motivos_perda",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_perda", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_etapas_tipo",
                table: "etapas",
                column: "tipo",
                unique: true,
                filter: "ativa AND tipo <> 'aberta'");

            // Valores iniciais (seção 3.4). Sem id: a sequence gera os ids e continua certa para os próximos.
            migrationBuilder.InsertData(
                table: "etapas",
                columns: new[] { "nome", "ordem", "tipo", "ativa" },
                values: new object[,]
                {
                    { "Novo", 1, "aberta", true },
                    { "Em contato", 2, "aberta", true },
                    { "Qualificado", 3, "aberta", true },
                    { "Proposta enviada", 4, "aberta", true },
                    { "Ganho", 5, "ganho", true },
                    { "Perdido", 6, "perdido", true },
                });

            migrationBuilder.InsertData(
                table: "motivos_perda",
                columns: new[] { "nome", "ativo" },
                values: new object[,]
                {
                    { "Sem resposta", true },
                    { "Sem interesse", true },
                    { "Preço", true },
                    { "Fechou com concorrente", true },
                    { "Fora do perfil", true },
                    { "Contato inválido", true },
                });

            migrationBuilder.InsertData(
                table: "configuracoes",
                columns: new[] { "chave", "valor" },
                values: new object[,]
                {
                    { "distribuicao_automatica", "true" },
                    { "exigir_documento_para_ganho", "true" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes");

            migrationBuilder.DropTable(
                name: "etapas");

            migrationBuilder.DropTable(
                name: "motivos_perda");
        }
    }
}
