using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace SonOfAnton.Backend.Migrations
{
    /// <inheritdoc />
    public partial class SyncCoreReadinessModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censorship_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_text_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    detected_items_json = table.Column<string>(type: "jsonb", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    triggered_rule_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censorship_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_censorship_audit_logs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_censorship_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "censorship_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    rule_type = table.Column<string>(type: "text", nullable: false),
                    pattern = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    replacement_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    severity = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censorship_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_censorship_rules_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loop_execution_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loop_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    current_iteration = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_iterations = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    context_json = table.Column<string>(type: "jsonb", nullable: true),
                    exit_reason = table.Column<string>(type: "text", nullable: true),
                    exit_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    hangfire_job_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_heartbeat_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loop_execution_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_loop_execution_states_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "memory_collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    default_sector_type = table.Column<string>(type: "text", nullable: false),
                    embedding_dimension = table.Column<int>(type: "integer", nullable: false, defaultValue: 384),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_collections", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_collections_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_memory_collections_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "planning_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_type = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    file_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planning_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_planning_documents_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "memory_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    embedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    sector_type = table.Column<string>(type: "text", nullable: false),
                    importance = table.Column<float>(type: "real", nullable: false, defaultValue: 0.5f),
                    access_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    chunk_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    source_document_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_entries_memory_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "memory_collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censorship_audit_logs_tenant_id",
                table: "censorship_audit_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_censorship_audit_logs_timestamp",
                table: "censorship_audit_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_censorship_audit_logs_user_id",
                table: "censorship_audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_censorship_rules_is_active",
                table: "censorship_rules",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_censorship_rules_tenant_id",
                table: "censorship_rules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_loop_execution_states_loop_id",
                table: "loop_execution_states",
                column: "loop_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loop_execution_states_state",
                table: "loop_execution_states",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_loop_execution_states_tenant_id",
                table: "loop_execution_states",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_collections_tenant_id",
                table: "memory_collections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_collections_tenant_id_name",
                table: "memory_collections",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memory_collections_workspace_id",
                table: "memory_collections",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_entries_collection_id",
                table: "memory_entries",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_entries_created_at",
                table: "memory_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_memory_entries_embedding",
                table: "memory_entries",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "ivfflat")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:lists", 100);

            migrationBuilder.CreateIndex(
                name: "IX_memory_entries_sector_type",
                table: "memory_entries",
                column: "sector_type");

            migrationBuilder.CreateIndex(
                name: "IX_memory_entries_source_document_id",
                table: "memory_entries",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_planning_documents_agent_id_file_type",
                table: "planning_documents",
                columns: new[] { "agent_id", "file_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planning_documents_tenant_id",
                table: "planning_documents",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censorship_audit_logs");

            migrationBuilder.DropTable(
                name: "censorship_rules");

            migrationBuilder.DropTable(
                name: "loop_execution_states");

            migrationBuilder.DropTable(
                name: "memory_entries");

            migrationBuilder.DropTable(
                name: "planning_documents");

            migrationBuilder.DropTable(
                name: "memory_collections");
        }
    }
}
