using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonOfAnton.Backend.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "workflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "folders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "data_stores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_stores", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_stores_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspaces_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workspaces_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "data_store_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_store_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_store_entries_data_stores_data_store_id",
                        column: x => x.data_store_id,
                        principalTable: "data_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workspace_members_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_workspace_id",
                table: "workflows",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_folders_workspace_id",
                table: "folders",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_store_entries_data_store_id_key",
                table: "data_store_entries",
                columns: new[] { "data_store_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_stores_tenant_id",
                table: "data_stores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_stores_tenant_id_name",
                table: "data_stores",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_members_user_id",
                table: "workspace_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_members_workspace_id_user_id",
                table: "workspace_members",
                columns: new[] { "workspace_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_owner_user_id",
                table: "workspaces",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_tenant_id",
                table: "workspaces",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_tenant_id_name",
                table: "workspaces",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_folders_workspaces_workspace_id",
                table: "folders",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_workflows_workspaces_workspace_id",
                table: "workflows",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_folders_workspaces_workspace_id",
                table: "folders");

            migrationBuilder.DropForeignKey(
                name: "FK_workflows_workspaces_workspace_id",
                table: "workflows");

            migrationBuilder.DropTable(
                name: "data_store_entries");

            migrationBuilder.DropTable(
                name: "workspace_members");

            migrationBuilder.DropTable(
                name: "data_stores");

            migrationBuilder.DropTable(
                name: "workspaces");

            migrationBuilder.DropIndex(
                name: "IX_workflows_workspace_id",
                table: "workflows");

            migrationBuilder.DropIndex(
                name: "IX_folders_workspace_id",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "folders");
        }
    }
}
