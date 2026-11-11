namespace GiveMeTeam.Data.Entities;

public class AnnouncementLanguage
{
    public long AnnouncementId { get; set; }
    public long LanguageId { get; set; }

    public Announcement Announcement { get; set; } = null!;
    public Language Language { get; set; } = null!;
}