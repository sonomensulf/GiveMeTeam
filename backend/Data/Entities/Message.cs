namespace GiveMeTeam.Data.Entities;

public class Message
{
    public long Id { get; set; }
    public long AnnouncementId { get; set; }
    public long SenderId { get; set; }
    public string Body { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Announcement Announcement { get; set; } = null!;
    public User Sender { get; set; } = null!;
}