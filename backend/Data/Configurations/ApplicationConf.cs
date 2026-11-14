
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GiveMeTeam.Data.Entities;

namespace GiveMeTeam.Data.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("applications", table =>
        {
            table.HasCheckConstraint("ck_applications_status","status IN ('pending', 'accepted', 'rejected', 'withdrawn')");
            table.HasCheckConstraint("ck_applications_updated_at","updated_at >= created_at");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();

        builder.Property(a => a.AnnouncementId).IsRequired();
        builder.Property(a => a.ApplicantId).IsRequired();

        builder.Property(a => a.Status)
            .HasMaxLength(9)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(a => a.UpdatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasOne(a => a.Announcement)
            .WithMany(an => an.Applications)
            .HasForeignKey(a => a.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Applicant)
            .WithMany(u => u.Applications)
            .HasForeignKey(a => a.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        // запрет иметь > 1 активной заявки на 1 объяв.
        builder.HasIndex(a => new { a.AnnouncementId, a.ApplicantId })
            .IsUnique()
            .HasFilter("status IN ('pending', 'accepted')")
            .HasDatabaseName("uq_applications_announcement_applicant_active");

        //состоит в одной группе
        builder.HasIndex(a => a.ApplicantId)
            .IsUnique()
            .HasFilter("status = 'accepted'")
            .HasDatabaseName("uq_applications_applicant_accepted");

        builder.HasIndex(a => new { a.ApplicantId, a.Status })
            .HasDatabaseName("ix_applications_applicant_status");

        builder.HasIndex(a => new { a.AnnouncementId, a.Status })
            .HasDatabaseName("ix_applications_announcement_status");
    }
}
