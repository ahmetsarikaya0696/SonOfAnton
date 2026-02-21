using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonOfAnton.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "avg_duration_ms",
                table: "workflows",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "workflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "failure_count",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "folder_id",
                table: "workflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_run_at",
                table: "workflows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "run_count",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "success_count",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tenant_id",
                table: "workflows",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chat_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    artifacts = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_history_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflows",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "execution_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    node_states = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    memory_snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    at_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_automatic = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_execution_snapshots_executions_execution_id",
                        column: x => x.execution_id,
                        principalTable: "executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    oidc_authority = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    oidc_client_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    oidc_client_secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhooks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    secret = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    trigger_count = table.Column<int>(type: "integer", nullable: false),
                    last_triggered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhooks", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhooks_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    workflow_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    commit_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_revisions_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "folders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    parent_folder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folders", x => x.id);
                    table.ForeignKey(
                        name: "FK_folders_folders_parent_folder_id",
                        column: x => x.parent_folder_id,
                        principalTable: "folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_folders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_role_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mapped_permissions = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_roles_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_identity_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_tenant_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tenant_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_tenant_roles_tenant_roles_tenant_role_id",
                        column: x => x.tenant_role_id,
                        principalTable: "tenant_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_tenant_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_created_by_user_id",
                table: "workflows",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_folder_id",
                table: "workflows",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_tenant_id",
                table: "workflows",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_history_workflow_id",
                table: "chat_history",
                column: "workflow_id");

            migrationBuilder.CreateIndex(
                name: "IX_execution_snapshots_execution_id",
                table: "execution_snapshots",
                column: "execution_id");

            migrationBuilder.CreateIndex(
                name: "IX_folders_parent_folder_id",
                table: "folders",
                column: "parent_folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_folders_tenant_id",
                table: "folders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_roles_tenant_id_external_role_name",
                table: "tenant_roles",
                columns: new[] { "tenant_id", "external_role_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_roles_tenant_role_id",
                table: "user_tenant_roles",
                column: "tenant_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_roles_user_id_tenant_role_id",
                table: "user_tenant_roles",
                columns: new[] { "user_id", "tenant_role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_users_external_identity_id",
                table: "users",
                column: "external_identity_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id",
                table: "users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_webhook_id",
                table: "webhooks",
                column: "webhook_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_workflow_id",
                table: "webhooks",
                column: "workflow_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_revisions_workflow_id_version",
                table: "workflow_revisions",
                columns: new[] { "workflow_id", "version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_workflows_folders_folder_id",
                table: "workflows",
                column: "folder_id",
                principalTable: "folders",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_workflows_tenants_tenant_id",
                table: "workflows",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_workflows_users_created_by_user_id",
                table: "workflows",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workflows_folders_folder_id",
                table: "workflows");

            migrationBuilder.DropForeignKey(
                name: "FK_workflows_tenants_tenant_id",
                table: "workflows");

            migrationBuilder.DropForeignKey(
                name: "FK_workflows_users_created_by_user_id",
                table: "workflows");

            migrationBuilder.DropTable(
                name: "chat_history");

            migrationBuilder.DropTable(
                name: "execution_snapshots");

            migrationBuilder.DropTable(
                name: "folders");

            migrationBuilder.DropTable(
                name: "user_tenant_roles");

            migrationBuilder.DropTable(
                name: "webhooks");

            migrationBuilder.DropTable(
                name: "workflow_revisions");

            migrationBuilder.DropTable(
                name: "tenant_roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_workflows_created_by_user_id",
                table: "workflows");

            migrationBuilder.DropIndex(
                name: "IX_workflows_folder_id",
                table: "workflows");

            migrationBuilder.DropIndex(
                name: "IX_workflows_tenant_id",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "avg_duration_ms",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "failure_count",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "folder_id",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "last_run_at",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "run_count",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "success_count",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "workflows");
        }
    }
}
