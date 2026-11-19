using GiveMeTeam.Data;
using GiveMeTeam.Data.Entities;
using static ApiHelpers;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Npgsql;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/me", GetMe).RequireAuthorization();
        app.MapPatch("/me", UpdateMe).RequireAuthorization();
        app.MapPatch("/me/password", ChangePassword).RequireAuthorization();
        app.MapGet("/users/{id:long}", GetPublicProfile);
    }

    private static async Task<IResult> GetMe(ClaimsPrincipal principal, AppDbContext db)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        var me = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                id = u.Id,
                nickname = u.Nickname,
                email = u.Email,
                birth_date = u.BirthDate,
                created_at = u.CreatedAt,
                languages = u.UserLanguages
                    .Select(ul => new { id = ul.Language.Id, code = ul.Language.Code, name = ul.Language.Name })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        // cookie есть, а пользователя в базе уже нет
        if (me is null) return Unauthorized();

        return Results.Ok(me);
    }

    private static async Task<IResult> UpdateMe(
        PatchMeRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        if (request.Email is null && request.LanguageIds is null)
            return Error(400, "validation_error", "Укажите email или языки общения");

        if (request.Email is not null)
        {
            var emailError = InputRules.EmailError(request.Email);
            if (emailError is not null) return Error(400, "validation_error", emailError);
        }

        if (request.LanguageIds is not null)
        {
            var ids = request.LanguageIds;
            if (ids.Count == 0)
                return Error(400, "validation_error", "Выберите хотя бы один язык общения");
            if (ids.Distinct().Count() != ids.Count)
                return Error(400, "validation_error", "Языки не должны повторяться");

            var existing = await db.Languages.CountAsync(l => ids.Contains(l.Id));
            if (existing != ids.Count)
                return Error(400, "validation_error", "Указан несуществующий язык");
        }

        var user = await db.Users
            .Include(u => u.UserLanguages)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Unauthorized();

        if (request.Email is not null)
            user.Email = request.Email;

        if (request.LanguageIds is not null)
        {
            var newIds = request.LanguageIds.ToHashSet();

            // удаляем только лишние и добавляем только недостающие,
            // чтобы EF не пытался удалить и вставить одну и ту же пару
            foreach (var ul in user.UserLanguages.Where(x => !newIds.Contains(x.LanguageId)).ToList())
                user.UserLanguages.Remove(ul);

            var have = user.UserLanguages.Select(x => x.LanguageId).ToHashSet();
            foreach (var id in newIds.Where(id => !have.Contains(id)))
                user.UserLanguages.Add(new UserLanguage { LanguageId = id });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Error(409, "conflict", "Эта почта уже занята");
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IPasswordHasher<User> hasher)
    {
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        if (string.IsNullOrEmpty(request.CurrentPassword))
            return Error(400, "validation_error", "Укажите текущий пароль");

        var passwordError = InputRules.PasswordError(request.NewPassword);
        if (passwordError is not null) return Error(400, "validation_error", passwordError);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Unauthorized();

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
                == PasswordVerificationResult.Failed)
            return Error(400, "validation_error", "Неверный текущий пароль");

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetPublicProfile(long id, AppDbContext db)
    {
        var user = await db.Users
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Nickname,
                u.BirthDate,
                u.CreatedAt,
                Languages = u.UserLanguages
                    .Select(ul => new { id = ul.Language.Id, code = ul.Language.Code, name = ul.Language.Name })
                    .ToList(),
                ActiveAnnouncement = u.Announcements
                    .Where(a => a.Status == "open" || a.Status == "full")
                    .Select(a => new
                    {
                        id = a.Id,
                        title = a.Title,
                        game = a.Game.Name,
                        game_mode = a.GameMode.Name,
                        status = a.Status
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (user is null)
            return Error(404, "not_found", "Пользователь не найден");

        return Results.Ok(new
        {
            id = user.Id,
            nickname = user.Nickname,
            age = CalculateAge(user.BirthDate),
            created_at = user.CreatedAt,
            languages = user.Languages,
            active_announcement = user.ActiveAnnouncement
        });
    }

    private static int CalculateAge(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return age;
    }

    public record PatchMeRequest(string? Email, List<long>? LanguageIds);
    public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
}