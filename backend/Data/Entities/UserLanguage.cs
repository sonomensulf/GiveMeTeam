namespace GiveMeTeam.Data.Entities;

public class UserLanguage
{
    public long UserId { get; set; }
    public long LanguageId { get; set; }

    public User User { get; set; } = null!;
    public Language Language { get; set; } = null!;
}