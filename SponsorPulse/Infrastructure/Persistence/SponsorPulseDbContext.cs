using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Models;

namespace SponsorPulse.Infrastructure.Persistence;

public class SponsorPulseDbContext(DbContextOptions<SponsorPulseDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // When set (par les composants/authenticator), les filtres globaux utiliseront cet Id
    public Guid? CurrentUserId { get; set; }

    public DbSet<Event> Events { get; set; }
    public DbSet<EventMedia> EventMedia { get; set; }
    public DbSet<TwitchAuthToken> TwitchAuthTokens { get; set; }
    public DbSet<Settings> Settings { get; set; }
    public DbSet<Dashboard> Dashboards { get; set; }
    public DbSet<LinkedAccount> LinkedAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>().HasKey(e => e.Id);

        // Media
        modelBuilder
            .Entity<Event>()
            .HasMany(e => e.Media)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EventMedia>().HasKey(m => m.Id);

        // Owner relation between Event and ApplicationUser (1 -> many)
        modelBuilder
            .Entity<Event>()
            .HasOne(e => e.Owner)
            .WithMany(u => u.Events)
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Settings 1:1
        modelBuilder
            .Entity<ApplicationUser>()
            .HasOne(u => u.Settings)
            .WithOne(s => s.User)
            .HasForeignKey<Settings>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dashboard 1:1
        modelBuilder
            .Entity<ApplicationUser>()
            .HasOne(u => u.Dashboard)
            .WithOne(d => d.User)
            .HasForeignKey<Dashboard>(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global query filter: ne retourner que les events appartenant à l'utilisateur courant
        modelBuilder
            .Entity<Event>()
            .HasQueryFilter(e => CurrentUserId == null || e.OwnerId == CurrentUserId);

        // Twitch OAuth tokens/state
        modelBuilder.Entity<TwitchAuthToken>().HasKey(t => t.Id);
        modelBuilder.Entity<TwitchAuthToken>().Property(t => t.State).IsRequired();
        modelBuilder
            .Entity<TwitchAuthToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.TwitchAuthTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Linked accounts (Twitch, Twitter, ...)
        modelBuilder.Entity<LinkedAccount>().HasKey(l => l.Id);
        modelBuilder
            .Entity<LinkedAccount>()
            .HasOne(l => l.User)
            .WithMany(u => u.LinkedAccounts)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure TwitterAnalytics as JSON column with value converter
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        var converter = new ValueConverter<TwitterAnalytics?, string>(
            v => v == null ? null : JsonSerializer.Serialize(v, jsonSerializerOptions),
            v =>
                string.IsNullOrEmpty(v)
                    ? null
                    : JsonSerializer.Deserialize<TwitterAnalytics>(v, jsonSerializerOptions)
        );

        modelBuilder
            .Entity<Event>()
            .Property(e => e.TwitterAnalytics)
            .HasConversion(converter)
            .HasColumnType("TEXT");

        // Configuration SQLite spécifique si nécessaire
    }
}
