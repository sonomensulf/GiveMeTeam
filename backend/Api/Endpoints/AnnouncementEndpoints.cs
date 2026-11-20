using static ApiHelpers;
using System.Security.Claims;

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this WebApplication app)
    {
        app.MapPost("/announcements", CreateAnnouncement).RequireAuthorization();
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