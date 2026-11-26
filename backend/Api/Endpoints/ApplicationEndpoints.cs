using static ApiHelpers;
using System.Security.Claims;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
        app.MapPost("/announcements/{id:long}/applications", CreateApplication).RequireAuthorization();
    }

    private static async Task<IResult> CreateApplication(
        long id,
        ClaimsPrincipal principal,
        ApplicationService service,
        CancellationToken ct)
    {
        // id заявителя берём из cookie-сессии, а не из тела запроса
        if (CurrentUserId(principal) is not long applicantId) return Unauthorized();

        var result = await service.CreateAsync(id, applicantId, ct);
        return Results.Created($"/applications/{result.Id}", result);
    }
}

public record ApplicationResponse(
    long Id,
    long AnnouncementId,
    long ApplicantId,
    string Status,
    DateTimeOffset CreatedAt);