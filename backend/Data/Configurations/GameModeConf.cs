
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class GameModeConfiguration : IEntityTypeConfiguration<GameMode>
{
    public void Configure(EntityTypeBuilder<GameMode> builder)
    {
        builder.ToTable("game_modes", table =>
        {
            table.HasCheckConstraint("ck_game_modes_name_not_empty","length(trim(name)) > 0");
        });

        builder.HasKey(gm => gm.Id);
        builder.Property(gm => gm.Id).UseIdentityAlwaysColumn();

        builder.Property(gm => gm.GameId).IsRequired();
        builder.Property(gm => gm.Name).HasMaxLength(50).IsRequired();

        builder.HasOne(gm => gm.Game)
            .WithMany(g => g.GameModes)
            .HasForeignKey(gm => gm.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(gm => new { gm.GameId, gm.Name }).IsUnique().HasDatabaseName("uq_game_modes_game_id_name");
        builder.HasAlternateKey(gm => new { gm.GameId, gm.Id }).HasName("uq_game_modes_game_id_id");
    }
}