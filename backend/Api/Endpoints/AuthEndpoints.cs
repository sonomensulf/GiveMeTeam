using static ApiHelpers;
using GiveMeTeam.Data;
using GiveMeTeam.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/register", Register);
        app.MapPost("/auth/login", Login);
        app.MapPost("/auth/logout", (Delegate)Logout).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        AppDbContext db,
        IPasswordHasher<User> hasher)
    {
        // 1. Проверка данных без обращения к базе
        var error = Validate(request);
        if (error is not null)
            return Error(400, "validation_error", error);

        // 2. Проверка, что все указанные языки существуют
        var languageIds = request.LanguageIds!.Distinct().ToList();
        var existing = await db.Languages.CountAsync(l => languageIds.Contains(l.Id));
        if (existing != languageIds.Count)
            return Error(400, "validation_error", "Указан несуществующий язык");

        // 3. Создание пользователя
        var user = new User
        {
            Nickname = request.Nickname!.Trim(),
            Email = request.Email!,
            BirthDate = request.BirthDate!.Value,
            UserLanguages = languageIds
                .Select(id => new UserLanguage { LanguageId = id })
                .ToList()
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);

        // 4. Сохранение (одна транзакция: пользователь + его языки)
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Error(409, "conflict", "Почта или никнейм уже заняты");
        }

        return Results.Created($"/users/{user.Id}", new { id = user.Id });
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext http,
        AppDbContext db,
        IPasswordHasher<User> hasher)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return Error(400, "validation_error", "Укажите почту и пароль");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed)
            return Error(401, "invalid_credentials", "Неверная почта или пароль");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Nickname)
        };
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Results.Ok(new { id = user.Id, nickname = user.Nickname });
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static string? Validate(RegisterRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Nickname) || r.Nickname.Trim().Length > 50)
            return "Никнейм обязателен и не длиннее 50 символов";

        var emailError = InputRules.EmailError(r.Email);
        if (emailError is not null)
            return emailError;

        if (r.BirthDate is null || r.BirthDate > DateOnly.FromDateTime(DateTime.UtcNow))
            return "Укажите корректную дату рождения";

        var passwordError = InputRules.PasswordError(r.Password);
        if (passwordError is not null)
            return passwordError;

        if (r.LanguageIds is null || r.LanguageIds.Count == 0)
            return "Выберите хотя бы один язык общения";

        return null;
    }
}

public record RegisterRequest(
    string? Nickname,
    string? Email,
    DateOnly? BirthDate,
    string? Password,
    List<long>? LanguageIds);

public record LoginRequest(string? Email, string? Password);