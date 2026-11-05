namespace Data.Entities;

public class Game
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public ICollection<GameMode> GameModes { get; set; } = []; // у одной игры несколько режимов
    public ICollection<Announcement> Announcements { get; set; } = [];
}