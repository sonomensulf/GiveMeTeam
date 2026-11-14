using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class UserLanguageConfiguration : IEntityTypeConfiguration<UserLanguage>
{
    public void Configure(EntityTypeBuilder<UserLanguage> builder)
    {
        builder.ToTable("user_languages");
        
        builder.HasKey(ul => new { ul.UserId, ul.LanguageId });

        builder.HasOne(ul => ul.User)
            .WithMany(u => u.UserLanguages)
            .HasForeignKey(ul => ul.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ul => ul.Language)
            .WithMany(l => l.UserLanguages)
            .HasForeignKey(ul => ul.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}