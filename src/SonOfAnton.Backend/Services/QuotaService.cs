using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services;

public class QuotaService : IQuotaService
{
    private readonly SonOfAntonDbContext _dbContext;

    public QuotaService(SonOfAntonDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserQuotaEntity> GetUserQuotaAsync(Guid userId)
    {
        var quota = await _dbContext.UserQuotas.FirstOrDefaultAsync(q => q.UserId == userId);
        
        if (quota == null)
        {
            // Kota yoksa varsayılan kotayı oluştur
            quota = new UserQuotaEntity { UserId = userId };
            _dbContext.UserQuotas.Add(quota);
            await _dbContext.SaveChangesAsync();
        }

        // Yeni aya geçildiyse sıfırla
        if (quota.CurrentMonthStart.Month != DateTime.UtcNow.Month || quota.CurrentMonthStart.Year != DateTime.UtcNow.Year)
        {
            quota.CurrentMonthExecutions = 0;
            quota.CurrentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            await _dbContext.SaveChangesAsync();
        }

        return quota;
    }

    public async Task<bool> CheckCanExecuteAsync(Guid userId)
    {
        var quota = await GetUserQuotaAsync(userId);
        return quota.CurrentMonthExecutions < quota.MaxExecutionsPerMonth;
    }

    public async Task IncrementExecutionAsync(Guid userId)
    {
        var quota = await GetUserQuotaAsync(userId);
        quota.CurrentMonthExecutions++;
        quota.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }
}
