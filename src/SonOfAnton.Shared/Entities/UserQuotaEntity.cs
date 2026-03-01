using System;

namespace SonOfAnton.Shared.Entities;

public class UserQuotaEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    
    // Limits
    public int MaxWorkflows { get; set; } = 5; // e.g., free tier 5 workflows
    public int MaxExecutionsPerMonth { get; set; } = 100; // e.g., free tier 100 executions per month
    
    // Current usage
    public int CurrentMonthExecutions { get; set; } = 0;
    public DateTime CurrentMonthStart { get; set; } = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
