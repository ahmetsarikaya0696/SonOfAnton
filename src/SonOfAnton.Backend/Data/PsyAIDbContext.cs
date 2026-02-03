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

    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    public DbSet<ExecutionEntity> Executions => Set<ExecutionEntity>();
    public DbSet<ExecutionLogEntity> ExecutionLogs => Set<ExecutionLogEntity>();
    public DbSet<VectorStoreEntity> VectorStore => Set<VectorStoreEntity>();
    public DbSet<WebhookEntity> Webhooks => Set<WebhookEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // pgvector extension
        modelBuilder.HasPostgresExtension("vector");

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
    }
}


