public static class AnnouncementStatus
{
    public const string Open = "open";
    public const string Full = "full";
    public const string Closed = "closed";

    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [Open] = [Full, Closed],
        [Full] = [Open, Closed],
        [Closed] = [Open]
    };

    public static bool CanTransition(string from, string to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    // Бросает 409, если переход запрещён
    public static void EnsureTransition(string from, string to)
    {
        if (!CanTransition(from, to))
            throw new ConflictException("invalid_status_transition",
                $"Недопустимый переход статуса объявления: {from} → {to}");
    }

    // Активное объявление: open или full
    public static bool IsActive(string status) => status is Open or Full;

    // Какой статус должен быть у активного объявления при таком числе участников.
    public static string Recalculate(string current, int participants, int maxParticipants)
    {
        if (current == Open && participants >= maxParticipants) return Full;
        if (current == Full && participants < maxParticipants) return Open;
        return current;
    }
}