using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oficina.Infraestrutura.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Fase 4: o status <c>EmExecucao</c> passa a se chamar <c>EmReparo</c> (o enum é persistido como
    /// string, então os dados existentes são renomeados aqui). A concorrência otimista usa a coluna de
    /// sistema <c>xmin</c> do PostgreSQL, que já existe em toda tabela: o <c>AddColumn xmin</c> gerado
    /// pelo Npgsql foi removido à mão.
    /// </remarks>
    public partial class Fase4CicloDeVidaOs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelada_em",
                schema: "os",
                table: "ordem_servico",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_cancelamento",
                schema: "os",
                table: "ordem_servico",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE os.ordem_servico SET status='EmReparo' WHERE status='EmExecucao';");
            migrationBuilder.Sql("UPDATE os.historico_status SET status_anterior='EmReparo' WHERE status_anterior='EmExecucao';");
            migrationBuilder.Sql("UPDATE os.historico_status SET status_novo='EmReparo' WHERE status_novo='EmExecucao';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE os.historico_status SET status_novo='EmExecucao' WHERE status_novo='EmReparo';");
            migrationBuilder.Sql("UPDATE os.historico_status SET status_anterior='EmExecucao' WHERE status_anterior='EmReparo';");
            migrationBuilder.Sql("UPDATE os.ordem_servico SET status='EmExecucao' WHERE status='EmReparo';");

            migrationBuilder.DropColumn(
                name: "cancelada_em",
                schema: "os",
                table: "ordem_servico");

            migrationBuilder.DropColumn(
                name: "motivo_cancelamento",
                schema: "os",
                table: "ordem_servico");
        }
    }
}
