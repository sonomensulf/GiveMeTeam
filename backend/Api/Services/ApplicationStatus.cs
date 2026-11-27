public static class ApplicationStatus
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Withdrawn = "withdrawn";

    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [Pending] = [Accepted, Rejected, Withdrawn],
        [Accepted] = [Withdrawn, Rejected],
        [Rejected] = [],
        [Withdrawn] = []
    };

    public static bool CanTransition(string from, string to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    // Бросает 409, если переход запрещён
    public static void EnsureTransition(string from, string to)
    {
        if (!CanTransition(from, to))
            throw new ConflictException("invalid_status_transition",
                $"Недопустимый переход статуса заявки: {from} → {to}");
    }
}