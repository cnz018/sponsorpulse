using Microsoft.EntityFrameworkCore;
using SponsorPulse.Domain.Models.Analytics;
using SponsorPulse.Domain.Models.Twitch;

namespace SponsorPulse.Infrastructure.Persistence;

public class SponsorPulseAnalyticsDbContext(
    DbContextOptions<SponsorPulseAnalyticsDbContext> options
) : DbContext(options)
{
    public DbSet<SocialPostEntity> SocialPosts { get; set; }
    public DbSet<TwitchStreamSnapshot> TwitchStreamSnapshots { get; set; }
    public DbSet<StorytellingAnalysis> StorytellingAnalyses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SocialPostEntity>(entity =>
        {
            entity.ToTable("SocialPosts");
            entity.HasKey(post => post.Id);
            entity.Property(post => post.Platform).HasConversion<string>().IsRequired();
            entity.Property(post => post.RawJsonPayload).HasColumnType("TEXT").IsRequired();
            entity
                .Property(post => post.PlatformSpecificDataJson)
                .HasColumnType("TEXT")
                .IsRequired();
            entity.Property(post => post.FetchedAt).IsRequired();
            entity.Property(post => post.ImpressionsCount).IsRequired().HasDefaultValue(0);
            entity.HasIndex(post => post.Platform);
            entity.HasIndex(post => post.CreatedAt);
        });

        modelBuilder.Entity<TwitchStreamSnapshot>(entity =>
        {
            entity.ToTable("twitchStreamSnapshots");
            entity.HasKey(snap => snap.Id);
        });

        modelBuilder.Entity<StorytellingAnalysis>(entity =>
        {
            entity.ToTable("StorytellingAnalyses");
            entity.HasKey(analysis => analysis.EventId);
            entity.Property(analysis => analysis.ResponseJson).HasColumnType("TEXT").IsRequired();
            entity.Property(analysis => analysis.GeneratedAt).IsRequired();
        });
    }
}
