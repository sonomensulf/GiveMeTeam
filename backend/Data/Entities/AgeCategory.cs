namespace Data.Entities;

public class AgeCategory
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public short MinAge { get; set; }

    public ICollection<Announcement> Announcements { get; set; } = [];
}