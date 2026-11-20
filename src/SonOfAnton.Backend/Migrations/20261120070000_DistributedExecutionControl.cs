using Microsoft.EntityFrameworkCore.Migrations;
namespace SonOfAnton.Backend.Migrations;
public partial class DistributedExecutionControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "pause_requested", table: "executions", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "step_mode", table: "executions", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(name: "steps_remaining", table: "executions", type: "integer", nullable: false, defaultValue: 0);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "pause_requested", table: "executions");
        migrationBuilder.DropColumn(name: "step_mode", table: "executions");
        migrationBuilder.DropColumn(name: "steps_remaining", table: "executions");
    }
}
