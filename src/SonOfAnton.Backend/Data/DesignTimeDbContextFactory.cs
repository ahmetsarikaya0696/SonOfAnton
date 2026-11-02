using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SonOfAnton.Backend.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SonOfAntonDbContext>
{
    public SonOfAntonDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=sonofanton_db;Username=sonofanton;Password=sonofanton_dev_password";
        return new SonOfAntonDbContext(new DbContextOptionsBuilder<SonOfAntonDbContext>()
            .UseNpgsql(connection, options => options.UseVector()).Options);
    }
}
