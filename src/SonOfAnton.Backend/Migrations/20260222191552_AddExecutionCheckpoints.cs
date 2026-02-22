using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonOfAnton.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "execution_checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    variables_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    execution_memory = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_checkpoints", x => x.id);
                    table.ForeignKey(
                        name: "FK_execution_checkpoints_executions_execution_id",
                        column: x => x.execution_id,
                        principalTable: "executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_checkpoints_execution_id",
                table: "execution_checkpoints",
                column: "execution_id");

            migrationBuilder.CreateIndex(
                name: "IX_execution_checkpoints_node_id",
                table: "execution_checkpoints",
                column: "node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_checkpoints");
        }
    }
}
