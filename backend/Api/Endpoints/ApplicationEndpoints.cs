using static ApiHelpers;
using System.Security.Claims;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
        app.MapPost("/announcements/{id:long}/applications", CreateApplication).RequireAuthorization();
        app.MapGet("/announcements/{id:long}/applications", ListAnnouncementApplications).RequireAuthorization();
        app.MapGet("/me/applications", ListMyApplications).RequireAuthorization();
        app.MapGet("/me/announcements/applications", ListIncomingApplications).RequireAuthorization();
        app.MapPost("/applications/{id:long}/accept", AcceptApplication).RequireAuthorization();
        app.MapPost("/applications/{id:long}/reject", RejectApplication).RequireAuthorization();
        app.MapPost("/applications/{id:long}/withdraw", WithdrawApplication).RequireAuthorization();
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

    private static async Task<IResult> ListMyApplications(
        ClaimsPrincipal principal,
        ApplicationService service,
        CancellationToken ct)
    {
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        var result = await service.ListMineAsync(userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListIncomingApplications(
        ClaimsPrincipal principal,
        ApplicationService service,
        CancellationToken ct)
    {
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        var result = await service.ListIncomingAsync(userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListAnnouncementApplications(
        long id,
        ClaimsPrincipal principal,
        ApplicationService service,
        CancellationToken ct)
    {
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        var result = await service.ListForAnnouncementAsync(id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AcceptApplication(
        long id,
        ClaimsPrincipal principal,
        ApplicationService service,
        CancellationToken ct)
    {
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        var result = await service.AcceptAsync(id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> RejectApplication(
        long id,
        ClaimsPrincipal principal,
        ApplicationService service,
        CancellationToken ct)
    {
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        var result = await service.RejectAsync(id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> WithdrawApplication(
        long id,
        ClaimsPrincipal principal,
        ApplicationService service,
        CancellationToken ct)
    {
        if (CurrentUserId(principal) is not long userId) return Unauthorized();

        var result = await service.WithdrawAsync(id, userId, ct);
        return Results.Ok(result);
    }
}

public record ApplicationResponse(
    long Id,
    long AnnouncementId,
    long ApplicantId,
    string Status,
    DateTimeOffset CreatedAt);

public record ApplicationAnnouncementBrief(
    long Id,
    string Title,
    string Status,
    long AuthorId,
    string AuthorNickname,
    NamedRef Game,
    NamedRef GameMode);

public record MyApplicationItem(
    long Id,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ApplicationAnnouncementBrief Announcement);

public record ApplicantBrief(long Id, string Nickname, int Age);

public record IncomingAnnouncementBrief(long Id, string Title, string Status);

public record IncomingApplicationItem(
    long Id,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ApplicantBrief Applicant,
    IncomingAnnouncementBrief Announcement);