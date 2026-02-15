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
    public DbSet<ChatHistoryEntity> ChatHistory => Set<ChatHistoryEntity>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}


