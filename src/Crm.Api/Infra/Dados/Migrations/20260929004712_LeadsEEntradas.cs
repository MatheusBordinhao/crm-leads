using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Crm.Api.Infra.Dados.Migrations
{
    /// <inheritdoc />
    public partial class LeadsEEntradas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campanhas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    id_externo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_provisorio = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campanhas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leads",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    telefone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cpf_cnpj = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    empresa = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    etapa_id = table.Column<int>(type: "integer", nullable: false),
                    responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    plataforma = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    campanha_id = table.Column<int>(type: "integer", nullable: true),
                    utm_source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    utm_medium = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    utm_campaign = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    utm_content = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    utm_term = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gclid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    respostas_extras = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    motivo_perda_id = table.Column<int>(type: "integer", nullable: true),
                    motivo_perda_detalhe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    teste = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    primeiro_contato_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    convertido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    perdido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    anonimizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                    table.ForeignKey(
                        name: "fk_leads_campanhas_campanha_id",
                        column: x => x.campanha_id,
                        principalTable: "campanhas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leads_etapas_etapa_id",
                        column: x => x.etapa_id,
                        principalTable: "etapas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leads_motivos_perda_motivo_perda_id",
                        column: x => x.motivo_perda_id,
                        principalTable: "motivos_perda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leads_usuarios_responsavel_id",
                        column: x => x.responsavel_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entradas_recebidas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lead_id = table.Column<long>(type: "bigint", nullable: true),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    id_externo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    campanha_id = table.Column<int>(type: "integer", nullable: true),
                    conjunto = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    anuncio = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    formulario = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    utm_source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    utm_medium = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    utm_campaign = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    utm_content = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    utm_term = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gclid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    respostas_extras = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    payload_bruto = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    teste = table.Column<bool>(type: "boolean", nullable: false),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entradas_recebidas", x => x.id);
                    table.ForeignKey(
                        name: "fk_entradas_recebidas_campanhas_campanha_id",
                        column: x => x.campanha_id,
                        principalTable: "campanhas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entradas_recebidas_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "historico_etapas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lead_id = table.Column<long>(type: "bigint", nullable: false),
                    etapa_anterior_id = table.Column<int>(type: "integer", nullable: false),
                    etapa_nova_id = table.Column<int>(type: "integer", nullable: false),
                    motivo_perda_id = table.Column<int>(type: "integer", nullable: true),
                    motivo_perda_detalhe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historico_etapas", x => x.id);
                    table.ForeignKey(
                        name: "fk_historico_etapas_etapas_etapa_anterior_id",
                        column: x => x.etapa_anterior_id,
                        principalTable: "etapas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historico_etapas_etapas_etapa_nova_id",
                        column: x => x.etapa_nova_id,
                        principalTable: "etapas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historico_etapas_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_historico_etapas_motivos_perda_motivo_perda_id",
                        column: x => x.motivo_perda_id,
                        principalTable: "motivos_perda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historico_etapas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campanhas_origem_id_externo",
                table: "campanhas",
                columns: new[] { "origem", "id_externo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entradas_recebidas_campanha_id",
                table: "entradas_recebidas",
                column: "campanha_id");

            migrationBuilder.CreateIndex(
                name: "ix_entradas_recebidas_lead_id",
                table: "entradas_recebidas",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_entradas_recebidas_origem_id_externo",
                table: "entradas_recebidas",
                columns: new[] { "origem", "id_externo" },
                unique: true,
                filter: "situacao <> 'erro'");

            migrationBuilder.CreateIndex(
                name: "ix_entradas_recebidas_recebido_em",
                table: "entradas_recebidas",
                column: "recebido_em");

            migrationBuilder.CreateIndex(
                name: "ix_historico_etapas_etapa_anterior_id",
                table: "historico_etapas",
                column: "etapa_anterior_id");

            migrationBuilder.CreateIndex(
                name: "ix_historico_etapas_etapa_nova_id",
                table: "historico_etapas",
                column: "etapa_nova_id");

            migrationBuilder.CreateIndex(
                name: "ix_historico_etapas_lead_id",
                table: "historico_etapas",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_historico_etapas_motivo_perda_id",
                table: "historico_etapas",
                column: "motivo_perda_id");

            migrationBuilder.CreateIndex(
                name: "ix_historico_etapas_ocorrido_em",
                table: "historico_etapas",
                column: "ocorrido_em");

            migrationBuilder.CreateIndex(
                name: "ix_historico_etapas_usuario_id",
                table: "historico_etapas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_campanha_id",
                table: "leads",
                column: "campanha_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_cpf_cnpj",
                table: "leads",
                column: "cpf_cnpj");

            migrationBuilder.CreateIndex(
                name: "ix_leads_criado_em",
                table: "leads",
                column: "criado_em");

            migrationBuilder.CreateIndex(
                name: "ix_leads_email",
                table: "leads",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_leads_etapa_id",
                table: "leads",
                column: "etapa_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_motivo_perda_id",
                table: "leads",
                column: "motivo_perda_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_responsavel_id_etapa_id",
                table: "leads",
                columns: new[] { "responsavel_id", "etapa_id" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_telefone",
                table: "leads",
                column: "telefone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entradas_recebidas");

            migrationBuilder.DropTable(
                name: "historico_etapas");

            migrationBuilder.DropTable(
                name: "leads");

            migrationBuilder.DropTable(
                name: "campanhas");
        }
    }
}
