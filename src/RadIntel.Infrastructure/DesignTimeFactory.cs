using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RadIntel.Application;

namespace RadIntel.Infrastructure;

// Scaffolding does not connect to a database or apply migrations.
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<RadIntelDbContext>
{
    public RadIntelDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<RadIntelDbContext>().UseNpgsql(
            "Host=localhost;Database=radintel_design_only;Username=design_only;Password=not_a_runtime_secret",
            p => p.MigrationsAssembly(typeof(RadIntelDbContext).Assembly.FullName)).Options, new DesignActor());
    private sealed class DesignActor : ICurrentActor
    {
        public Guid OrganizationId => Guid.Empty;
        public string UserId => "design-time";
        public bool CanProvision => false;
        public bool HasRecentMfa => false;
    }
}
