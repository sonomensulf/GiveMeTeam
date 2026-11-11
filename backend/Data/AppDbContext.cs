using Microsoft.EntityFrameworkCore;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // основные
    public DbSet<User> Users => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Message> Messages => Set<Message>();

    // справочники
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<GameMode> GameModes => Set<GameMode>();
    public DbSet<ExperienceLevel> ExperienceLevels => Set<ExperienceLevel>();
    public DbSet<AgeCategory> AgeCategories => Set<AgeCategory>();

    // связующие
    public DbSet<UserLanguage> UserLanguages => Set<UserLanguage>();
    public DbSet<AnnouncementLanguage> AnnouncementLanguages => Set<AnnouncementLanguage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}