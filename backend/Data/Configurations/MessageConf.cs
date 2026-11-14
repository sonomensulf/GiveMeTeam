
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", table =>
        {
            table.HasCheckConstraint("ck_messages_body_not_empty","length(trim(body)) > 0");
            table.HasCheckConstraint("ck_messages_body_length","length(body) <= 2000");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).UseIdentityAlwaysColumn();

        builder.Property(m => m.AnnouncementId).IsRequired();
        builder.Property(m => m.SenderId).IsRequired();

        builder.Property(m => m.Body)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(m => m.DeletedAt)
            .HasColumnType("timestamptz");

        builder.HasOne(m => m.Announcement)
            .WithMany(a => a.Messages)
            .HasForeignKey(m => m.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany(u => u.Messages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.AnnouncementId, m.CreatedAt })
            .HasDatabaseName("ix_messages_announcement_created");
    }
}
