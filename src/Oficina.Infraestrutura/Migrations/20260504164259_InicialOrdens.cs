using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Oficina.Infraestrutura.Migrations
{
    /// <inheritdoc />
    public partial class InicialOrdens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "os");

            migrationBuilder.CreateTable(
                name: "ordem_servico",
                schema: "os",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    diagnosticada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    enviada_aprovacao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    orcamento_aprovado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    orcamento_rejeitado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entregue_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordem_servico", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_peca",
                schema: "os",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    peca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peca_nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    preco_snapshot = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_peca", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_peca_ordem_servico_ordem_servico_id",
                        column: x => x.ordem_servico_id,
                        principalSchema: "os",
                        principalTable: "ordem_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_servico",
                schema: "os",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servico_nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    preco_snapshot = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_servico", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_servico_ordem_servico_ordem_servico_id",
                        column: x => x.ordem_servico_id,
                        principalSchema: "os",
                        principalTable: "ordem_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_peca_ordem_servico_id",
                schema: "os",
                table: "item_peca",
                column: "ordem_servico_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_servico_ordem_servico_id",
                schema: "os",
                table: "item_servico",
                column: "ordem_servico_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordem_servico_cliente_id",
                schema: "os",
                table: "ordem_servico",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordem_servico_numero",
                schema: "os",
                table: "ordem_servico",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ordem_servico_status",
                schema: "os",
                table: "ordem_servico",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_peca",
                schema: "os");

            migrationBuilder.DropTable(
                name: "item_servico",
                schema: "os");

            migrationBuilder.DropTable(
                name: "ordem_servico",
                schema: "os");
        }
    }
}
