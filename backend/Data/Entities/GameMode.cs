namespace GiveMeTeam.Data.Entities;

public class GameMode
{
    public long Id { get; set; }
    public long GameId { get; set; }
    public string Name { get; set; } = null!;

    public Game Game { get; set; } = null!;
    public ICollection<Announcement> Announcements { get; set; } = [];
}