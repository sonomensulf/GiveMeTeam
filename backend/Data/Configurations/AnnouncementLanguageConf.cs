
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class AnnouncementLanguageConfiguration : IEntityTypeConfiguration<AnnouncementLanguage>
{
    public void Configure(EntityTypeBuilder<AnnouncementLanguage> builder)
    {
        builder.ToTable("announcement_languages");

        builder.HasKey(al => new { al.AnnouncementId, al.LanguageId });

        builder.HasOne(al => al.Announcement)
            .WithMany(a => a.AnnouncementLanguages)
            .HasForeignKey(al => al.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(al => al.Language)
            .WithMany(l => l.AnnouncementLanguages)
            .HasForeignKey(al => al.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
