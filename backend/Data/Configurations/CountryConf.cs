using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries", table =>
        {
            table.HasCheckConstraint("ck_countries_name_not_empty", "length(trim(name)) > 0");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).UseIdentityAlwaysColumn();

        builder.Property(c => c.Code).HasMaxLength(10).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("uq_countries_code");
        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("uq_countries_name");
    }
}
