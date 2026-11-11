namespace GiveMeTeam.Data.Entities;

public class ExperienceLevel
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public ICollection<Announcement> Announcements { get; set; } = [];
}