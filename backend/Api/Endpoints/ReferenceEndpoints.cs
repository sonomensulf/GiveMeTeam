using GiveMeTeam.Data;
using Microsoft.EntityFrameworkCore;

public static class ReferenceEndpoints
{
    public static void MapReferenceEndpoints(this WebApplication app)
    {
        app.MapGet("/languages", async (AppDbContext db) =>
        {
            var languages = await db.Languages
                .AsNoTracking()
                .OrderBy(l => l.Id)
                .Select(l => new LanguageDto(l.Id, l.Code, l.Name))
                .ToListAsync();

            return Results.Ok(languages);
        });

        app.MapGet("/countries", async (AppDbContext db) =>
        {
            var countries = await db.Countries
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new CountryDto(c.Id, c.Code, c.Name))
                .ToListAsync();

            return Results.Ok(countries);
        });

        app.MapGet("/games", async (AppDbContext db) =>
        {
            var games = await db.Games
                .AsNoTracking()
                .OrderBy(g => g.Id)
                .Select(g => new GameDto(g.Id, g.Code, g.Name))
                .ToListAsync();

            return Results.Ok(games);
        });

        app.MapGet("/games/{id:long}/modes", async (long id, AppDbContext db) =>
        {
            var gameExists = await db.Games.AnyAsync(g => g.Id == id);

            if (!gameExists)
                return Results.NotFound(new
                {
                    code = "not_found",
                    message = "Игра не найдена"
                });

            var modes = await db.GameModes
                .AsNoTracking()
                .Where(m => m.GameId == id)
                .OrderBy(m => m.Id)
                .Select(m => new GameModeDto(m.Id, m.Name))
                .ToListAsync();

            return Results.Ok(modes);
        });

        app.MapGet("/experience-levels", async (AppDbContext db) =>
        {
            var levels = await db.ExperienceLevels
                .AsNoTracking()
                .OrderBy(l => l.Id)
                .Select(l => new ExperienceLevelDto(l.Id, l.Code, l.Name))
                .ToListAsync();

            return Results.Ok(levels);
        });

        app.MapGet("/age-categories", async (AppDbContext db) =>
        {
            var categories = await db.AgeCategories
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new AgeCategoryDto(c.Id, c.Code, c.Name))
                .ToListAsync();

            return Results.Ok(categories);
        });
    }
}

public record LanguageDto(long Id, string Code, string Name);
public record CountryDto(long Id, string Code, string Name);
public record GameDto(long Id, string Code, string Name);
public record GameModeDto(long Id, string Name);
public record ExperienceLevelDto(long Id, string Code, string Name);
public record AgeCategoryDto(long Id, string Code, string Name);