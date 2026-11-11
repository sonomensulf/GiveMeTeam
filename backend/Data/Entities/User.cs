namespace GiveMeTeam.Data.Entities;

public class User
{
    public long Id { get; set; }

    public string Nickname { get; set; } = null!;
    
    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateOnly BirthDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<UserLanguage> UserLanguages { get; set; } = [];
    public ICollection<Announcement> Announcements { get; set; } = [];
    public ICollection<Application> Applications { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}