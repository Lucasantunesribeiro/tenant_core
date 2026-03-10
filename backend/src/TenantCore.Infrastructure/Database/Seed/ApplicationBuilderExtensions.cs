using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TenantCore.Infrastructure.Database.Seed;

public static class ApplicationBuilderExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenantCoreDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();

        await dbContext.Database.MigrateAsync();
        await seeder.SeedAsync();
    }
}
