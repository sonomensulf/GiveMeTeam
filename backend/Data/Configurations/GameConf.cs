
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("games", table =>
        {
            table.HasCheckConstraint("ck_games_name_not_empty", "length(trim(name)) > 0");
        });

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).UseIdentityAlwaysColumn();

        builder.Property(g => g.Code).HasMaxLength(50).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(g => g.Code).IsUnique().HasDatabaseName("uq_games_code");
        builder.HasIndex(g => g.Name).IsUnique().HasDatabaseName("uq_games_name");
    }
}
