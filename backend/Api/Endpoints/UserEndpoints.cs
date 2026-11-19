using System.Security.Claims;
using GiveMeTeam.Data;
using Microsoft.EntityFrameworkCore;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/me", GetMe).RequireAuthorization();
    }

    private static async Task<IResult> GetMe(ClaimsPrincipal principal, AppDbContext db)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var userId))
            return Results.Json(new { code = "unauthorized", message = "Не авторизован" }, statusCode: 401);

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
        if (me is null)
            return Results.Json(new { code = "unauthorized", message = "Не авторизован" }, statusCode: 401);

        return Results.Ok(me);
    }
}