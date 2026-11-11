namespace GiveMeTeam.Data.Entities;

public class Announcement
{
    public long Id { get; set; }
    public long AuthorId { get; set; }
    public long GameId { get; set; }
    public long GameModeId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public long ExperienceLevelId { get; set; }
    public long AgeCategoryId { get; set; }
    public short MaxParticipants { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User Author { get; set; } = null!;
    public Game Game { get; set; } = null!;
    public GameMode GameMode { get; set; } = null!;
    public ExperienceLevel ExperienceLevel { get; set; } = null!;
    public AgeCategory AgeCategory { get; set; } = null!;

    public ICollection<AnnouncementLanguage> AnnouncementLanguages { get; set; } = [];
    public ICollection<Application> Applications { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}