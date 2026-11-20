public static class AgeCalculator
{
    public static int FullYears(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        // Если день рождения в этом году ещё не наступил, вычитаем 1
        if (birthDate > today.AddYears(-age)) age--;
        return age;
    }
}