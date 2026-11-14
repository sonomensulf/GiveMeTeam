using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("languages", table =>
        {
            table.HasCheckConstraint("ck_languages_name_not_empty", "length(trim(name)) > 0");   
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).UseIdentityAlwaysColumn();

        builder.Property(l => l.Code).HasMaxLength(10).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique().HasDatabaseName("uq_languages_code");
        builder.HasIndex(l => l.Name).IsUnique().HasDatabaseName("uq_languages_name");
    }
}