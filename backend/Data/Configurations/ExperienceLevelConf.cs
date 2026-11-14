
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class ExperienceLevelConfiguration : IEntityTypeConfiguration<ExperienceLevel>
{
    public void Configure(EntityTypeBuilder<ExperienceLevel> builder)
    {
        builder.ToTable("experience_levels", table =>
        {
            table.HasCheckConstraint("ck_experience_levels_name_not_empty","length(trim(name)) > 0");
        });

        builder.HasKey(el => el.Id);
        builder.Property(el => el.Id).UseIdentityAlwaysColumn();

        builder.Property(el => el.Code).HasMaxLength(20).IsRequired();
        builder.Property(el => el.Name).HasMaxLength(50).IsRequired();

        builder.HasIndex(el => el.Code).IsUnique().HasDatabaseName("uq_experience_levels_code");
        builder.HasIndex(el => el.Name).IsUnique().HasDatabaseName("uq_experience_levels_name");
    }
}
