using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonOfAnton.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceAndCustomNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_node_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    inputs_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    outputs_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    parameters_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    script_type = table.Column<string>(type: "text", nullable: false),
                    script_code = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_node_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_node_definitions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custom_node_definitions_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tags = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    content_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    download_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    average_rating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_marketplace_items_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_marketplace_items_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_installs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    installed_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    installed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_installs", x => x.id);
                    table.ForeignKey(
                        name: "FK_marketplace_installs_marketplace_items_item_id",
                        column: x => x.item_id,
                        principalTable: "marketplace_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_marketplace_installs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_marketplace_reviews_marketplace_items_item_id",
                        column: x => x.item_id,
                        principalTable: "marketplace_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_marketplace_reviews_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_custom_node_definitions_created_by_user_id",
                table: "custom_node_definitions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_node_definitions_tenant_id",
                table: "custom_node_definitions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_node_definitions_tenant_id_type",
                table: "custom_node_definitions",
                columns: new[] { "tenant_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_installs_item_id_user_id",
                table: "marketplace_installs",
                columns: new[] { "item_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_installs_user_id",
                table: "marketplace_installs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_items_category",
                table: "marketplace_items",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_items_item_type",
                table: "marketplace_items",
                column: "item_type");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_items_published_by_user_id",
                table: "marketplace_items",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_items_tenant_id",
                table: "marketplace_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_reviews_item_id_user_id",
                table: "marketplace_reviews",
                columns: new[] { "item_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_reviews_user_id",
                table: "marketplace_reviews",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_node_definitions");

            migrationBuilder.DropTable(
                name: "marketplace_installs");

            migrationBuilder.DropTable(
                name: "marketplace_reviews");

            migrationBuilder.DropTable(
                name: "marketplace_items");
        }
    }
}
