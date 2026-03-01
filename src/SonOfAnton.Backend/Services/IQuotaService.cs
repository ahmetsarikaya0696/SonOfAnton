namespace SonOfAnton.Backend.Services;

public interface IQuotaService
{
    Task<bool> CheckCanExecuteAsync(Guid userId);
    Task IncrementExecutionAsync(Guid userId);
    Task<SonOfAnton.Shared.Entities.UserQuotaEntity> GetUserQuotaAsync(Guid userId);
}
