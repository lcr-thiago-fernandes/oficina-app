using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oficina.Infraestrutura.Migrations
{
    /// <inheritdoc />
    public partial class InicialClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clientes");

            migrationBuilder.CreateTable(
                name: "cliente",
                schema: "clientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    tipo_pessoa = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cliente", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "veiculo",
                schema: "clientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    placa = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    modelo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_veiculo", x => x.id);
                    table.ForeignKey(
                        name: "FK_veiculo_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalSchema: "clientes",
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cliente_documento",
                schema: "clientes",
                table: "cliente",
                column: "documento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_cliente_id",
                schema: "clientes",
                table: "veiculo",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_placa",
                schema: "clientes",
                table: "veiculo",
                column: "placa",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "veiculo",
                schema: "clientes");

            migrationBuilder.DropTable(
                name: "cliente",
                schema: "clientes");
        }
    }
}
