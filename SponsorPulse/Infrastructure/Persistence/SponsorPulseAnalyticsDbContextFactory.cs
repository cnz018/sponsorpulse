using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SponsorPulse.Infrastructure.Persistence;

public sealed class SponsorPulseAnalyticsDbContextFactory
    : IDesignTimeDbContextFactory<SponsorPulseAnalyticsDbContext>
{
    public SponsorPulseAnalyticsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SponsorPulseAnalyticsDbContext>();
        optionsBuilder.UseSqlite("Data Source=sponsorpulse_analytics.db");
        return new SponsorPulseAnalyticsDbContext(optionsBuilder.Options);
    }
}
