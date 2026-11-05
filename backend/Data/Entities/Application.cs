namespace Data.Entities;

public class Application
{
    public long Id { get; set; }
    public long AnnouncementId { get; set; }
    public long ApplicantId { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Announcement Announcement { get; set; } = null!;
    public User Applicant { get; set; } = null!;
}