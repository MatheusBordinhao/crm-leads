using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Crm.Api.Infra.Dados.Migrations
{
    /// <inheritdoc />
    public partial class Importacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "importacao_id",
                table: "entradas_recebidas",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "importacoes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    codificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    separador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    campos = table.Column<string[]>(type: "text[]", nullable: false),
                    coluna_id_externo = table.Column<int>(type: "integer", nullable: true),
                    campanha_padrao_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    campanha_padrao_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    arquivo = table.Column<byte[]>(type: "bytea", nullable: true),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_linhas = table.Column<int>(type: "integer", nullable: false),
                    linhas_processadas = table.Column<int>(type: "integer", nullable: false),
                    criados = table.Column<int>(type: "integer", nullable: false),
                    vinculados = table.Column<int>(type: "integer", nullable: false),
                    ja_importados = table.Column<int>(type: "integer", nullable: false),
                    com_erro = table.Column<int>(type: "integer", nullable: false),
                    sem_responsavel = table.Column<int>(type: "integer", nullable: false),
                    reservada_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importacoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_importacoes_usuarios_responsavel_id",
                        column: x => x.responsavel_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_importacoes_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "importacoes_erros",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    importacao_id = table.Column<long>(type: "bigint", nullable: false),
                    linha = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importacoes_erros", x => x.id);
                    table.ForeignKey(
                        name: "fk_importacoes_erros_importacoes_importacao_id",
                        column: x => x.importacao_id,
                        principalTable: "importacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entradas_recebidas_importacao_id",
                table: "entradas_recebidas",
                column: "importacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_importacoes_responsavel_id",
                table: "importacoes",
                column: "responsavel_id");

            migrationBuilder.CreateIndex(
                name: "ix_importacoes_situacao_reservada_ate",
                table: "importacoes",
                columns: new[] { "situacao", "reservada_ate" });

            migrationBuilder.CreateIndex(
                name: "ix_importacoes_usuario_id",
                table: "importacoes",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_importacoes_erros_importacao_id_linha",
                table: "importacoes_erros",
                columns: new[] { "importacao_id", "linha" });

            migrationBuilder.AddForeignKey(
                name: "fk_entradas_recebidas_importacoes_importacao_id",
                table: "entradas_recebidas",
                column: "importacao_id",
                principalTable: "importacoes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_entradas_recebidas_importacoes_importacao_id",
                table: "entradas_recebidas");

            migrationBuilder.DropTable(
                name: "importacoes_erros");

            migrationBuilder.DropTable(
                name: "importacoes");

            migrationBuilder.DropIndex(
                name: "ix_entradas_recebidas_importacao_id",
                table: "entradas_recebidas");

            migrationBuilder.DropColumn(
                name: "importacao_id",
                table: "entradas_recebidas");
        }
    }
}
