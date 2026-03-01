using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using SonOfAnton.Backend.Controllers;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Data;

/// <summary>
/// SonOfAnton veritabanı context'i
/// </summary>
public class SonOfAntonDbContext : DbContext
{
    public SonOfAntonDbContext(DbContextOptions<SonOfAntonDbContext> options) : base(options)
    {
    }

    // ─── Identity & Multi-Tenancy ────────────────────────────────
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<TenantRoleEntity> TenantRoles => Set<TenantRoleEntity>();
    public DbSet<UserTenantRoleEntity> UserTenantRoles => Set<UserTenantRoleEntity>();

    // ─── Business Entities ─────────────────────────────────────
    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    public DbSet<ExecutionEntity> Executions => Set<ExecutionEntity>();
    public DbSet<ExecutionLogEntity> ExecutionLogs => Set<ExecutionLogEntity>();
    public DbSet<VectorStoreEntity> VectorStore => Set<VectorStoreEntity>();
    public DbSet<WebhookEntity> Webhooks => Set<WebhookEntity>();
    public DbSet<FolderEntity> Folders => Set<FolderEntity>();
    public DbSet<ExecutionSnapshotEntity> ExecutionSnapshots => Set<ExecutionSnapshotEntity>();
    public DbSet<ExecutionCheckpointEntity> ExecutionCheckpoints => Set<ExecutionCheckpointEntity>();
    public DbSet<ChatHistoryEntity> ChatHistory => Set<ChatHistoryEntity>();
    public DbSet<WorkflowRevisionEntity> WorkflowRevisions => Set<WorkflowRevisionEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<WorkflowApiEndpointEntity> WorkflowApiEndpoints => Set<WorkflowApiEndpointEntity>();
    public DbSet<CredentialEntity> Credentials => Set<CredentialEntity>();
    public DbSet<LinkedAccountEntity> LinkedAccounts => Set<LinkedAccountEntity>();

    // ─── Data Stores ─────────────────────────────────────────────
    public DbSet<DataStoreEntity> DataStores => Set<DataStoreEntity>();
    public DbSet<DataStoreEntryEntity> DataStoreEntries => Set<DataStoreEntryEntity>();

    // ─── Data Tables (Zapier-like) ───────────────────────────────
    public DbSet<DataTableEntity> DataTables => Set<DataTableEntity>();
    public DbSet<DataRowEntity> DataRows => Set<DataRowEntity>();
    public DbSet<DataFormEntity> DataForms => Set<DataFormEntity>();

    // ─── Custom Nodes & Marketplace ──────────────────────────────
    public DbSet<CustomNodeDefinitionEntity> CustomNodeDefinitions => Set<CustomNodeDefinitionEntity>();
    public DbSet<MarketplaceItemEntity> MarketplaceItems => Set<MarketplaceItemEntity>();
    public DbSet<MarketplaceReviewEntity> MarketplaceReviews => Set<MarketplaceReviewEntity>();
    public DbSet<MarketplaceInstallEntity> MarketplaceInstalls => Set<MarketplaceInstallEntity>();

    // ─── Chatbots ────────────────────────────────────────────────
    public DbSet<ChatbotEntity> Chatbots => Set<ChatbotEntity>();
    public DbSet<ChatbotSessionEntity> ChatbotSessions => Set<ChatbotSessionEntity>();

    // ─── Workspaces ───────────────────────────────────────────────
    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();
    public DbSet<WorkspaceMemberEntity> WorkspaceMembers => Set<WorkspaceMemberEntity>();

    // ─── Notifications ──────────────────────────────────────────────
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    // ─── Usage Quotas ───────────────────────────────────────────────
    public DbSet<UserQuotaEntity> UserQuotas => Set<UserQuotaEntity>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Chatbots ────────────────────────────────────────────────
        modelBuilder.Entity<ChatbotEntity>(entity =>
        {
            entity.ToTable("chatbots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.SystemPrompt).HasColumnName("system_prompt").HasMaxLength(4000);
            entity.Property(e => e.Model).HasColumnName("model").HasMaxLength(100);
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.IsPublic).HasColumnName("is_public").HasDefaultValue(false);
            
            entity.HasIndex(e => e.WorkspaceId);
            entity.HasOne(e => e.Workspace)
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatbotSessionEntity>(entity =>
        {
            entity.ToTable("chatbot_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChatbotId).HasColumnName("chatbot_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255);
            entity.Property(e => e.HistoryJson).HasColumnName("history_json").HasColumnType("jsonb");
            
            entity.HasIndex(e => e.ChatbotId);
            entity.HasIndex(e => e.UserId);
            
            entity.HasOne(e => e.Chatbot)
                .WithMany(c => c.Sessions)
                .HasForeignKey(e => e.ChatbotId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // ─── Identity & Multi-Tenancy ──────────────────────────────

        // Tenant
        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(100);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.OidcAuthority).HasColumnName("oidc_authority").HasMaxLength(500);
            entity.Property(e => e.OidcClientId).HasColumnName("oidc_client_id").HasMaxLength(255);
            entity.Property(e => e.OidcClientSecret).HasColumnName("oidc_client_secret").HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // User
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ExternalIdentityId).HasColumnName("external_identity_id").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(255);
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");

            entity.HasIndex(e => e.ExternalIdentityId).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.TenantId);

            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TenantRole
        modelBuilder.Entity<TenantRoleEntity>(entity =>
        {
            entity.ToTable("tenant_roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ExternalRoleName).HasColumnName("external_role_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.MappedPermissions).HasColumnName("mapped_permissions").HasColumnType("jsonb");
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => new { e.TenantId, e.ExternalRoleName }).IsUnique();

            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Roles)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserTenantRole (many-to-many)
        modelBuilder.Entity<UserTenantRoleEntity>(entity =>
        {
            entity.ToTable("user_tenant_roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TenantRoleId).HasColumnName("tenant_role_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");

            entity.HasIndex(e => new { e.UserId, e.TenantRoleId }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TenantRole)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.TenantRoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Business Entities ─────────────────────────────────────

        // Workflow
        modelBuilder.Entity<WorkflowEntity>(entity =>
        {
            entity.ToTable("workflows");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.TriggerType).HasColumnName("trigger_type").HasConversion<string>();
            entity.Property(e => e.GraphData).HasColumnName("graph_data").HasColumnType("jsonb");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasMany(e => e.Executions)
                .WithOne(e => e.Workflow)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WorkflowRevision
        modelBuilder.Entity<WorkflowRevisionEntity>(entity =>
        {
            entity.ToTable("workflow_revisions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkflowId).HasColumnName("workflow_id");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.WorkflowJson).HasColumnName("workflow_json").HasColumnType("jsonb");
            entity.Property(e => e.CommitMessage).HasColumnName("commit_message").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => new { e.WorkflowId, e.Version }).IsUnique();

            entity.HasOne(e => e.Workflow)
                .WithMany(w => w.Revisions)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Execution
        modelBuilder.Entity<ExecutionEntity>(entity =>
        {
            entity.ToTable("executions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkflowId).HasColumnName("workflow_id");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>();
            entity.Property(e => e.CurrentNodeId).HasColumnName("current_node_id");
            entity.Property(e => e.Memory).HasColumnName("memory").HasColumnType("jsonb");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(4000);
            entity.Property(e => e.HangfireJobId).HasColumnName("hangfire_job_id").HasMaxLength(100);
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.Status);

            entity.HasMany(e => e.Logs)
                .WithOne(e => e.Execution)
                .HasForeignKey(e => e.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ExecutionLog
        modelBuilder.Entity<ExecutionLogEntity>(entity =>
        {
            entity.ToTable("execution_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ExecutionId).HasColumnName("execution_id");
            entity.Property(e => e.NodeId).HasColumnName("node_id");
            entity.Property(e => e.Level).HasColumnName("level").HasConversion<string>();
            entity.Property(e => e.Message).HasColumnName("message").HasMaxLength(4000);
            entity.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");

            entity.HasIndex(e => e.ExecutionId);
            entity.HasIndex(e => e.Timestamp);
        });

        // VectorStore
        modelBuilder.Entity<VectorStoreEntity>(entity =>
        {
            entity.ToTable("vector_store");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Collection).HasColumnName("collection").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            // nomic-embed-text modeli 768 boyut üretir
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.Collection);
            
            // pgvector index for similarity search
            entity.HasIndex(e => e.Embedding)
                .HasMethod("ivfflat")
                .HasOperators("vector_cosine_ops")
                .HasStorageParameter("lists", 100);
        });
        
        // Webhook
        modelBuilder.Entity<WebhookEntity>(entity =>
        {
            entity.ToTable("webhooks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WebhookId).HasColumnName("webhook_id").HasMaxLength(50).IsRequired();
            entity.Property(e => e.WorkflowId).HasColumnName("workflow_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.Secret).HasColumnName("secret").HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.MinIntervalSeconds).HasColumnName("min_interval_seconds");
            entity.Property(e => e.TriggerCount).HasColumnName("trigger_count");
            entity.Property(e => e.LastTriggeredAt).HasColumnName("last_triggered_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            
            entity.HasIndex(e => e.WebhookId).IsUnique();
            entity.HasIndex(e => e.WorkflowId);
            
            entity.HasOne(e => e.Workflow)
                .WithMany()
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ApiKey
        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(50).IsRequired();
            entity.Property(e => e.KeyHash).HasColumnName("key_hash").HasMaxLength(128).IsRequired();
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.Scopes).HasColumnName("scopes").HasColumnType("jsonb");
            entity.Property(e => e.RateLimitPerMinute).HasColumnName("rate_limit_per_minute").HasDefaultValue(60);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.LastUsedAt).HasColumnName("last_used_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => e.TenantId);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // WorkflowApiEndpoint
        modelBuilder.Entity<WorkflowApiEndpointEntity>(entity =>
        {
            entity.ToTable("workflow_api_endpoints");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkflowId).HasColumnName("workflow_id");
            entity.Property(e => e.Route).HasColumnName("route").HasMaxLength(255).IsRequired();
            entity.Property(e => e.HttpMethod).HasColumnName("http_method").HasMaxLength(10).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.InputSchema).HasColumnName("input_schema").HasColumnType("jsonb");
            entity.Property(e => e.OutputTemplate).HasColumnName("output_template");
            entity.Property(e => e.ResponseContentType).HasColumnName("response_content_type").HasMaxLength(100).IsRequired();
            entity.Property(e => e.RequireApiKey).HasColumnName("require_api_key");
            entity.Property(e => e.WaitForResult).HasColumnName("wait_for_result");
            entity.Property(e => e.WaitTimeoutSeconds).HasColumnName("wait_timeout_seconds");
            entity.Property(e => e.RateLimitPerMinute).HasColumnName("rate_limit_per_minute");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.Route).IsUnique();
            entity.HasIndex(e => e.WorkflowId);

            entity.HasOne(e => e.Workflow)
                .WithMany()
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Credentials
        modelBuilder.Entity<CredentialEntity>(entity =>
        {
            entity.ToTable("credentials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.SystemName).HasColumnName("system_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.EncryptedData).HasColumnName("encrypted_data").IsRequired();
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.UserId);
            
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LinkedAccounts
        modelBuilder.Entity<LinkedAccountEntity>(entity =>
        {
            entity.ToTable("linked_accounts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProviderUserId).HasColumnName("provider_user_id").HasMaxLength(255).IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.CredentialId).HasColumnName("credential_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.Provider, e.ProviderUserId }).IsUnique();

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Credential)
                .WithMany()
                .HasForeignKey(e => e.CredentialId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Workflow Statistics columns
        modelBuilder.Entity<WorkflowEntity>(entity =>
        {
            entity.Property(e => e.RunCount).HasColumnName("run_count").HasDefaultValue(0);
            entity.Property(e => e.SuccessCount).HasColumnName("success_count").HasDefaultValue(0);
            entity.Property(e => e.FailureCount).HasColumnName("failure_count").HasDefaultValue(0);
            entity.Property(e => e.AvgDurationMs).HasColumnName("avg_duration_ms").HasDefaultValue(0.0);
            entity.Property(e => e.LastRunAt).HasColumnName("last_run_at");
            entity.Property(e => e.FolderId).HasColumnName("folder_id");

            entity.HasOne(e => e.Folder)
                .WithMany(f => f.Workflows)
                .HasForeignKey(e => e.FolderId)
                .OnDelete(DeleteBehavior.SetNull);

            // Multi-Tenant FK
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.HasIndex(e => e.TenantId);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Workspace FK
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.HasIndex(e => e.WorkspaceId);

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Workflows)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Folders
        modelBuilder.Entity<FolderEntity>(entity =>
        {
            entity.ToTable("folders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Icon).HasColumnName("icon").HasMaxLength(50);
            entity.Property(e => e.Color).HasColumnName("color").HasMaxLength(20);
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.ParentFolderId).HasColumnName("parent_folder_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.ParentFolder)
                .WithMany(e => e.ChildFolders)
                .HasForeignKey(e => e.ParentFolderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Multi-Tenant FK
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.HasIndex(e => e.TenantId);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Workspace FK
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.HasIndex(e => e.WorkspaceId);

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Folders)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Execution Snapshots
        modelBuilder.Entity<ExecutionSnapshotEntity>(entity =>
        {
            entity.ToTable("execution_snapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ExecutionId).HasColumnName("execution_id");
            entity.Property(e => e.Label).HasColumnName("label").HasMaxLength(255);
            entity.Property(e => e.NodeStates).HasColumnName("node_states").HasColumnType("jsonb");
            entity.Property(e => e.MemorySnapshot).HasColumnName("memory_snapshot").HasColumnType("jsonb");
            entity.Property(e => e.AtNodeId).HasColumnName("at_node_id");
            entity.Property(e => e.IsAutomatic).HasColumnName("is_automatic");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.ExecutionId);

            entity.HasOne(e => e.Execution)
                .WithMany(e => e.Snapshots)
                .HasForeignKey(e => e.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Execution Checkpoints
        modelBuilder.Entity<ExecutionCheckpointEntity>(entity =>
        {
            entity.ToTable("execution_checkpoints");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ExecutionId).HasColumnName("execution_id");
            entity.Property(e => e.NodeId).HasColumnName("node_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.VariablesJson).HasColumnName("variables_json").HasColumnType("jsonb");
            entity.Property(e => e.ExecutionMemory).HasColumnName("execution_memory").HasColumnType("jsonb");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");

            entity.HasIndex(e => e.ExecutionId);
            entity.HasIndex(e => e.NodeId);

            entity.HasOne(e => e.Execution)
                .WithMany(e => e.Checkpoints)
                .HasForeignKey(e => e.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Chat History
        modelBuilder.Entity<ChatHistoryEntity>(entity =>
        {
            entity.ToTable("chat_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkflowId).HasColumnName("workflow_id");
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20);
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Artifacts).HasColumnName("artifacts").HasColumnType("jsonb");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");

            entity.HasIndex(e => e.WorkflowId);
        });

        // ─── Custom Node Definitions ────────────────────────────────
        modelBuilder.Entity<CustomNodeDefinitionEntity>(entity =>
        {
            entity.ToTable("custom_node_definitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Icon).HasColumnName("icon").HasMaxLength(50);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(100);
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.Color).HasColumnName("color").HasMaxLength(20);
            entity.Property(e => e.InputsJson).HasColumnName("inputs_json").HasColumnType("jsonb");
            entity.Property(e => e.OutputsJson).HasColumnName("outputs_json").HasColumnType("jsonb");
            entity.Property(e => e.ParametersJson).HasColumnName("parameters_json").HasColumnType("jsonb");
            entity.Property(e => e.ScriptType).HasColumnName("script_type").HasConversion<string>();
            entity.Property(e => e.ScriptCode).HasColumnName("script_code");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.IsPublished).HasColumnName("is_published");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            // Tenant içinde type benzersiz
            entity.HasIndex(e => new { e.TenantId, e.Type }).IsUnique();
            entity.HasIndex(e => e.TenantId);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── Marketplace Items ───────────────────────────────────────
        modelBuilder.Entity<MarketplaceItemEntity>(entity =>
        {
            entity.ToTable("marketplace_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.PublishedByUserId).HasColumnName("published_by_user_id");
            entity.Property(e => e.ItemType).HasColumnName("item_type").HasConversion<string>();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(4000);
            entity.Property(e => e.Icon).HasColumnName("icon").HasMaxLength(50);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(100);
            entity.Property(e => e.Tags).HasColumnName("tags").HasColumnType("jsonb");
            entity.Property(e => e.ContentJson).HasColumnName("content_json").HasColumnType("jsonb");
            entity.Property(e => e.Version).HasColumnName("version").HasMaxLength(20);
            entity.Property(e => e.DownloadCount).HasColumnName("download_count").HasDefaultValue(0);
            entity.Property(e => e.AverageRating).HasColumnName("average_rating").HasDefaultValue(0.0);
            entity.Property(e => e.IsPublished).HasColumnName("is_published");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ItemType);
            entity.HasIndex(e => e.Category);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PublishedByUser)
                .WithMany()
                .HasForeignKey(e => e.PublishedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── Marketplace Reviews ─────────────────────────────────────
        modelBuilder.Entity<MarketplaceReviewEntity>(entity =>
        {
            entity.ToTable("marketplace_reviews");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.Comment).HasColumnName("comment").HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            // Bir kullanıcı bir öğeye sadece bir review yapabilir
            entity.HasIndex(e => new { e.ItemId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Item)
                .WithMany(i => i.Reviews)
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Marketplace Installs ────────────────────────────────────
        modelBuilder.Entity<MarketplaceInstallEntity>(entity =>
        {
            entity.ToTable("marketplace_installs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.InstalledVersion).HasColumnName("installed_version").HasMaxLength(20);
            entity.Property(e => e.InstalledAt).HasColumnName("installed_at");

            entity.HasIndex(e => new { e.ItemId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Item)
                .WithMany(i => i.Installs)
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Data Stores ─────────────────────────────────────────────
        modelBuilder.Entity<DataStoreEntity>(entity =>
        {
            entity.ToTable("data_stores");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DataStoreEntryEntity>(entity =>
        {
            entity.ToTable("data_store_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DataStoreId).HasColumnName("data_store_id");
            entity.Property(e => e.Key).HasColumnName("key").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Value).HasColumnName("value").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.DataStoreId, e.Key }).IsUnique();

            entity.HasOne(e => e.DataStore)
                .WithMany(s => s.Entries)
                .HasForeignKey(e => e.DataStoreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Data Tables ─────────────────────────────────────────────
        modelBuilder.Entity<DataTableEntity>(entity =>
        {
            entity.ToTable("data_tables");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.ColumnsJson).HasColumnName("columns_json").HasColumnType("jsonb");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.WorkspaceId);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Workspace)
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DataRowEntity>(entity =>
        {
            entity.ToTable("data_rows");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DataTableId).HasColumnName("data_table_id");
            entity.Property(e => e.ValuesJson).HasColumnName("values_json").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.DataTableId);

            entity.HasOne(e => e.DataTable)
                .WithMany(t => t.Rows)
                .HasForeignKey(e => e.DataTableId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DataFormEntity>(entity =>
        {
            entity.ToTable("data_forms");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DataTableId).HasColumnName("data_table_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(50).IsRequired();
            entity.Property(e => e.FieldsJson).HasColumnName("fields_json").HasColumnType("jsonb");
            entity.Property(e => e.ThemeJson).HasColumnName("theme_json").HasColumnType("jsonb");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.DataTableId);
            entity.HasIndex(e => e.TenantId);

            entity.HasOne(e => e.DataTable)
                .WithMany(t => t.Forms)
                .HasForeignKey(e => e.DataTableId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Workspace)
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── Workspaces ───────────────────────────────────────────────
        modelBuilder.Entity<WorkspaceEntity>(entity =>
        {
            entity.ToTable("workspaces");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(100);
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.Icon).HasColumnName("icon").HasMaxLength(50);
            entity.Property(e => e.Color).HasColumnName("color").HasMaxLength(20);
            entity.Property(e => e.IsDefault).HasColumnName("is_default");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OwnerUser)
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Workspace Members
        modelBuilder.Entity<WorkspaceMemberEntity>(entity =>
        {
            entity.ToTable("workspace_members");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Role).HasColumnName("role").HasConversion<string>();
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at");

            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Notifications ──────────────────────────────────────────────
        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Message).HasColumnName("message").HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Type).HasColumnName("type").HasConversion<string>();
            entity.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ReadAt).HasColumnName("read_at");
            entity.Property(e => e.Link).HasColumnName("link").HasMaxLength(500);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsRead);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Quotas ───────────────────────────────────────────────
        modelBuilder.Entity<UserQuotaEntity>(entity =>
        {
            entity.ToTable("user_quotas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.MaxWorkflows).HasColumnName("max_workflows").HasDefaultValue(5);
            entity.Property(e => e.MaxExecutionsPerMonth).HasColumnName("max_executions_per_month").HasDefaultValue(100);
            entity.Property(e => e.CurrentMonthExecutions).HasColumnName("current_month_executions").HasDefaultValue(0);
            entity.Property(e => e.CurrentMonthStart).HasColumnName("current_month_start");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.UserId).IsUnique(); // Bir kullanıcının bir kotası olur

            entity.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}


