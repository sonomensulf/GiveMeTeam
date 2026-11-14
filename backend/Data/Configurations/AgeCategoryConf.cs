
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class AgeCategoryConfiguration : IEntityTypeConfiguration<AgeCategory>
{
    public void Configure(EntityTypeBuilder<AgeCategory> builder)
    {
        builder.ToTable("age_categories", table =>
        {
            table.HasCheckConstraint("ck_age_categories_name_not_empty","length(trim(name)) > 0");
            table.HasCheckConstraint("ck_age_categories_min_age_non_negative","min_age >= 0");
        });

        builder.HasKey(ac => ac.Id);
        builder.Property(ac => ac.Id).UseIdentityAlwaysColumn();

        builder.Property(ac => ac.Code).HasMaxLength(20).IsRequired();
        builder.Property(ac => ac.Name).HasMaxLength(50).IsRequired();
        builder.Property(ac => ac.MinAge).IsRequired();

        builder.HasIndex(ac => ac.Code).IsUnique().HasDatabaseName("uq_age_categories_code");
        builder.HasIndex(ac => ac.Name).IsUnique().HasDatabaseName("uq_age_categories_name");
    }
}
