
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements", table =>
        {
            table.HasCheckConstraint("ck_announcements_title_not_empty","length(trim(title)) > 0");
            table.HasCheckConstraint("ck_announcements_description_not_empty","length(trim(description)) > 0");
            table.HasCheckConstraint("ck_announcements_description_length","length(description) <= 2000");
            table.HasCheckConstraint("ck_announcements_max_participants","max_participants BETWEEN 2 AND 10");
            table.HasCheckConstraint("ck_announcements_status","status IN ('open', 'full', 'closed')");
            table.HasCheckConstraint("ck_announcements_updated_at","updated_at >= created_at");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();

        builder.Property(a => a.Title).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Description).HasColumnType("text").IsRequired();
        builder.Property(a => a.MaxParticipants).IsRequired();

        builder.Property(a => a.Status)
            .HasMaxLength(6)
            .HasDefaultValue("open")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(a => a.UpdatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasOne(a => a.Author)
            .WithMany(u => u.Announcements)
            .HasForeignKey(a => a.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Game)
            .WithMany(g => g.Announcements)
            .HasForeignKey(a => a.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        // Игровой режим (составной внешний ключ)
        builder.HasOne(a => a.GameMode)
            .WithMany(gm => gm.Announcements)
            .HasForeignKey(a => new { a.GameId, a.GameModeId })
            .HasPrincipalKey(gm => new { gm.GameId, gm.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ExperienceLevel)
            .WithMany(el => el.Announcements)
            .HasForeignKey(a => a.ExperienceLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AgeCategory)
            .WithMany(ac => ac.Announcements)
            .HasForeignKey(a => a.AgeCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Не более одного активного объявления у автора
        builder.HasIndex(a => a.AuthorId, "uq_announcements_author_active")
            .IsUnique()
            .HasFilter("status IN ('open', 'full')");

        // Индекс для подсчёта объявлений автора
        // и получения списка его объявлений
        builder.HasIndex(a => a.AuthorId, "ix_announcements_author_id");
        
        // Индекс для поиска объявлений
        builder.HasIndex(a => new { a.Status, a.GameId, a.CreatedAt })
            .HasDatabaseName("ix_announcements_status_game_created");
    }
}
