using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gateway.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without a running host or
/// a real connection string — EF only needs the model shape at design time.
/// </summary>
public sealed class GatewayDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    public GatewayDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GatewayDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=Gateway;Trusted_Connection=True;TrustServerCertificate=True;");
        return new GatewayDbContext(optionsBuilder.Options);
    }
}
