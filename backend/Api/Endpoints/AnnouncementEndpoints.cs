using static ApiHelpers;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this WebApplication app)
    {
        app.MapGet("/announcements", ListAnnouncements); 
        app.MapPost("/announcements", CreateAnnouncement).RequireAuthorization();
    }

    private static async Task<IResult> ListAnnouncements(
        AnnouncementService service,
        CancellationToken ct,
        [FromQuery(Name = "game_id")] long? gameId = null,
        [FromQuery(Name = "game_mode_id")] long? gameModeId = null,
        [FromQuery(Name = "experience_level_id")] long? experienceLevelId = null,
        [FromQuery(Name = "language_id")] long? languageId = null,
        [FromQuery(Name = "age_category_id")] long? ageCategoryId = null,
        [FromQuery(Name = "page")] int? page = null,
        [FromQuery(Name = "page_size")] int? pageSize = null)
    {
        var filter = new AnnouncementListFilter(
            gameId, gameModeId, experienceLevelId, languageId, ageCategoryId, page, pageSize);

        var result = await service.ListAsync(filter, ct);
        return Results.Ok(result);
}

    private static async Task<IResult> CreateAnnouncement(
        CreateAnnouncementRequest request,
        ClaimsPrincipal principal,
        AnnouncementService service,
        CancellationToken ct)
    {
        // id автора берём из cookie-сессии, а не из тела запроса
        if (CurrentUserId(principal) is not long authorId) return Unauthorized();

        var result = await service.CreateAsync(authorId, request, ct);
        return Results.Created($"/announcements/{result.Id}", result);
    }
}

public record CreateAnnouncementRequest(
    long GameId,
    long GameModeId,
    string? Title,
    string? Description,
    long ExperienceLevelId,
    List<long>? LanguageIds,
    long AgeCategoryId,
    int MaxParticipants,
    string? Status);

public record AnnouncementResponse(
    long Id,
    long AuthorId,
    long GameId,
    long GameModeId,
    string Title,
    string Description,
    long ExperienceLevelId,
    long AgeCategoryId,
    IReadOnlyList<long> LanguageIds,
    short MaxParticipants,
    string Status,
    DateTimeOffset CreatedAt);

public record AnnouncementListFilter(
    long? GameId,
    long? GameModeId,
    long? ExperienceLevelId,
    long? LanguageId,
    long? AgeCategoryId,
    int? Page,
    int? PageSize);

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);

public record AuthorBrief(long Id, string Nickname, int Age);
public record NamedRef(long Id, string Name);
public record LanguageBrief(long Id, string Code, string Name);

public record AnnouncementListItem(
    long Id,
    string Title,
    string DescriptionPreview,
    AuthorBrief Author,
    NamedRef Game,
    NamedRef GameMode,
    NamedRef ExperienceLevel,
    NamedRef AgeCategory,
    IReadOnlyList<LanguageBrief> Languages,
    int ParticipantsCount,
    short MaxParticipants,
    bool IsFull,
    string Status,
    DateTimeOffset CreatedAt);