using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SponsorPulse.Infrastructure.Persistence;

public sealed class SponsorPulseDbContextFactory
    : IDesignTimeDbContextFactory<SponsorPulseDbContext>
{
    public SponsorPulseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SponsorPulseDbContext>();
        optionsBuilder.UseSqlite("Data Source=SponsorPulse.db;Cache=Shared;Foreign Keys=False");
        return new SponsorPulseDbContext(optionsBuilder.Options);
    }
}
