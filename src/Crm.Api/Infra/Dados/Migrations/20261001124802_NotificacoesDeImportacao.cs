using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Api.Infra.Dados.Migrations
{
    /// <inheritdoc />
    public partial class NotificacoesDeImportacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "importacao_id",
                table: "notificacoes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_importacao_id",
                table: "notificacoes",
                column: "importacao_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notificacoes_importacoes_importacao_id",
                table: "notificacoes",
                column: "importacao_id",
                principalTable: "importacoes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notificacoes_importacoes_importacao_id",
                table: "notificacoes");

            migrationBuilder.DropIndex(
                name: "ix_notificacoes_importacao_id",
                table: "notificacoes");

            migrationBuilder.DropColumn(
                name: "importacao_id",
                table: "notificacoes");
        }
    }
}
