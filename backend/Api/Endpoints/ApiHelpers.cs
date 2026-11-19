using System.Security.Claims;
using System.Text.RegularExpressions;

public static class ApiHelpers
{
    public static IResult Error(int status, string code, string message) =>
        Results.Json(new { code, message }, statusCode: status);

    public static IResult Unauthorized() =>
        Error(401, "unauthorized", "Не авторизован");

    public static long? CurrentUserId(ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

public static class InputRules
{
    public static string? EmailError(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255
            || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")
            || email != email.ToLowerInvariant())
            return "Неверный формат почты (буквы должны быть строчными)";
        return null;
    }

    public static string? PasswordError(string? password)
    {
        if (password is null || password.Length < 8
            || !password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            return "Пароль: минимум 8 символов и хотя бы один специальный символ";
        return null;
    }
}