using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonOfAnton.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddDataTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_tables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    columns_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_tables", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_tables_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_tables_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_data_tables_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "data_forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_table_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fields_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    theme_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_forms", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_forms_data_tables_data_table_id",
                        column: x => x.data_table_id,
                        principalTable: "data_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_data_forms_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_forms_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "data_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_table_id = table.Column<Guid>(type: "uuid", nullable: false),
                    values_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_rows", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_rows_data_tables_data_table_id",
                        column: x => x.data_table_id,
                        principalTable: "data_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_data_forms_data_table_id",
                table: "data_forms",
                column: "data_table_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_forms_slug",
                table: "data_forms",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_forms_tenant_id",
                table: "data_forms",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_forms_workspace_id",
                table: "data_forms",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_rows_data_table_id",
                table: "data_rows",
                column: "data_table_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_tables_created_by_user_id",
                table: "data_tables",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_tables_tenant_id",
                table: "data_tables",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_tables_workspace_id",
                table: "data_tables",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_forms");

            migrationBuilder.DropTable(
                name: "data_rows");

            migrationBuilder.DropTable(
                name: "data_tables");
        }
    }
}
