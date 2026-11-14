using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_nickname_not_empty", "length(trim(nickname)) > 0");
            table.HasCheckConstraint("ck_users_email_lower", "email = lower(email)");
            table.HasCheckConstraint("ck_users_birth_date_not_future", "birth_date <= current_date");
        });
        
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).UseIdentityAlwaysColumn();

        builder.Property(u => u.Nickname).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(u => u.BirthDate).HasColumnType("date").IsRequired();
        builder.Property(u => u.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(u => u.Nickname).IsUnique().HasDatabaseName("uq_users_nickname");
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("uq_users_email");
    }
}