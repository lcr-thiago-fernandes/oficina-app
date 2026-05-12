using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oficina.Infraestrutura.Migrations
{
    /// <inheritdoc />
    public partial class InicialCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalogo");

            migrationBuilder.CreateTable(
                name: "servico",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    preco_base = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tempo_estimado_minutos = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servico", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_servico_nome",
                schema: "catalogo",
                table: "servico",
                column: "nome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "servico",
                schema: "catalogo");
        }
    }
}
