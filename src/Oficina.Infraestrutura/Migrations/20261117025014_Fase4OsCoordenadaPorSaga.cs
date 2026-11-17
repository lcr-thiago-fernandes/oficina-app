using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oficina.Infraestrutura.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Fase 4: marca as OSs abertas com saga orquestrada. As linhas existentes (OSs da Fase 3)
    /// recebem <c>false</c> pelo DEFAULT: não têm saga e são canceladas de forma síncrona.
    /// Nenhuma operação sobre <c>xmin</c> nem sobre os schemas <c>auth</c>/<c>clientes</c>.
    /// </remarks>
    public partial class Fase4OsCoordenadaPorSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "coordenada_por_saga",
                schema: "os",
                table: "ordem_servico",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "coordenada_por_saga",
                schema: "os",
                table: "ordem_servico");
        }
    }
}
