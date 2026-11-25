namespace GiveMeTeam.Data.Entities;

public class Country
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ICollection<User> Users { get; set; } = [];
}
