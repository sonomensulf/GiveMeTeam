using GiveMeTeam.Data;
using GiveMeTeam.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public class AnnouncementService(AppDbContext db, TimeProvider time)
{
    private const int MaxAnnouncementsPerAuthor = 10;
    private const string Open = "open", Full = "full", Closed = "closed";

    public async Task<AnnouncementResponse> CreateAsync(
        long authorId, CreateAnnouncementRequest r, CancellationToken ct)
    {
        // ── Фаза 1: проверки, не требующие блокировок ──
        var languageIds = Validate(r);
        await EnsureReferencesExistAsync(r, languageIds, ct);

        try
        {
            // ── Фаза 2: транзакция T2 ──
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // 1) Блокируем строку автора
            var birthDate = await db.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE id = {authorId} FOR UPDATE")
                .Select(u => u.BirthDate)
                .SingleOrDefaultAsync(ct);
            if (birthDate == default)
                throw new ConflictException("user_not_found", "Пользователь не найден");

            // 2) Лимит 10 объявлений в любых статусах
            var total = await db.Announcements.CountAsync(a => a.AuthorId == authorId, ct);
            if (total >= MaxAnnouncementsPerAuthor)
                throw new ConflictException("announcement_limit", "Нельзя иметь более 10 объявлений");

            // 3) Возраст автора против min_age категории
            var minAge = await db.AgeCategories
                .Where(c => c.Id == r.AgeCategoryId)
                .Select(c => (int)c.MinAge)
                .SingleAsync(ct);
            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            if (AgeCalculator.FullYears(birthDate, today) < minAge)
                throw new ConflictException("age_restricted",
                    "Ваш возраст не подходит для выбранной возрастной категории");

            // 4) Для активного объявления: нет другого активного и нет заявки accepted
            var isOpen = r.Status == Open;
            if (isOpen)
            {
                if (await db.Announcements.AnyAsync(a =>
                        a.AuthorId == authorId && (a.Status == Open || a.Status == Full), ct))
                    throw new ConflictException("active_announcement_exists",
                        "У вас уже есть активное объявление");

                if (await db.Applications.AnyAsync(a =>
                        a.ApplicantId == authorId && a.Status == "accepted", ct))
                    throw new ConflictException("already_in_group",
                        "Вы состоите в группе и не можете создать активное объявление");
            }

            // 5) Вставка объявления вместе с языками
            var entity = new Announcement
            {
                AuthorId = authorId,
                GameId = r.GameId,
                GameModeId = r.GameModeId,
                Title = r.Title!.Trim(),
                Description = r.Description!.Trim(),
                ExperienceLevelId = r.ExperienceLevelId,
                AgeCategoryId = r.AgeCategoryId,
                MaxParticipants = (short)r.MaxParticipants,
                Status = r.Status!,
                AnnouncementLanguages = languageIds
                    .Select(id => new AnnouncementLanguage { LanguageId = id })
                    .ToList()
            };
            db.Announcements.Add(entity);
            await db.SaveChangesAsync(ct);

            // 6) Для активного: свои pending-заявки автора -> withdrawn
            if (isOpen)
            {
                await db.Applications
                    .Where(a => a.ApplicantId == authorId && a.Status == "pending")
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "withdrawn"), ct);
            }

            await tx.CommitAsync(ct);

            return new AnnouncementResponse(
                entity.Id, entity.AuthorId, entity.GameId, entity.GameModeId,
                entity.Title, entity.Description, entity.ExperienceLevelId,
                entity.AgeCategoryId, languageIds, entity.MaxParticipants,
                entity.Status, entity.CreatedAt);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Страховка от гонки: два запроса одновременно прошли проверки,
            // но частичный уникальный индекс не даёт вставить второе активное.
            throw new ConflictException("active_announcement_exists",
                "У вас уже есть активное объявление");
        }
    }

    public async Task<PagedResponse<AnnouncementListItem>> ListAsync(
        AnnouncementListFilter f, CancellationToken ct)
    {
        // 1) Проверка параметров пагинации
        var page = f.Page ?? 1;
        var pageSize = f.PageSize ?? 20;

        if (page is < 1 or > 1_000_000)
            throw new ValidationException("invalid_page", "Номер страницы должен быть не меньше 1");
        if (pageSize is < 1 or > 100)
            throw new ValidationException("invalid_page_size", "Размер страницы от 1 до 100");

        // 2) Базовый запрос: только активные объявления
        var query = db.Announcements
            .AsNoTracking()
            .Where(a => a.Status == Open || a.Status == Full);

        // 3) Необязательные фильтры: добавляем условие, только если параметр пришёл
        if (f.GameId is long gameId)
            query = query.Where(a => a.GameId == gameId);
        if (f.GameModeId is long modeId)
            query = query.Where(a => a.GameModeId == modeId);
        if (f.ExperienceLevelId is long expId)
            query = query.Where(a => a.ExperienceLevelId == expId);
        if (f.AgeCategoryId is long ageId)
            query = query.Where(a => a.AgeCategoryId == ageId);
        if (f.LanguageId is long langId)
            query = query.Where(a => a.AnnouncementLanguages.Any(l => l.LanguageId == langId));

        // 4) Общее число подходящих объявлений (для постраничной навигации)
        var total = await query.CountAsync(ct);

        // 5) Нужная страница, новые первыми
        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Title,
                Preview = a.Description.Length > 200
                    ? a.Description.Substring(0, 200)
                    : a.Description,
                AuthorId = a.AuthorId,
                AuthorNickname = a.Author.Nickname,
                AuthorBirthDate = a.Author.BirthDate,
                GameId = a.GameId,
                GameName = a.Game.Name,
                ModeId = a.GameModeId,
                ModeName = a.GameMode.Name,
                ExpId = a.ExperienceLevelId,
                ExpName = a.ExperienceLevel.Name,
                AgeId = a.AgeCategoryId,
                AgeName = a.AgeCategory.Name,
                Languages = a.AnnouncementLanguages
                    .Select(l => new LanguageBrief(l.Language.Id, l.Language.Code, l.Language.Name))
                    .ToList(),
                // автор + принятые заявки
                Participants = a.Applications.Count(x => x.Status == "accepted") + 1,
                a.MaxParticipants,
                a.Status,
                a.CreatedAt
            })
            .ToListAsync(ct);

        // 6) Возраст считаем в C#, превращаем строки в DTO
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var items = rows.Select(r => new AnnouncementListItem(
            r.Id,
            r.Title,
            r.Preview,
            new AuthorBrief(r.AuthorId, r.AuthorNickname, AgeCalculator.FullYears(r.AuthorBirthDate, today)),
            new NamedRef(r.GameId, r.GameName),
            new NamedRef(r.ModeId, r.ModeName),
            new NamedRef(r.ExpId, r.ExpName),
            new NamedRef(r.AgeId, r.AgeName),
            r.Languages,
            r.Participants,
            r.MaxParticipants,
            r.Status == Full,
            r.Status,
            r.CreatedAt)).ToList();

        return new PagedResponse<AnnouncementListItem>(items, page, pageSize, total);
    }

    // Простые проверки формата. Возвращает очищенный список языков
    private static List<long> Validate(CreateAnnouncementRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title))
            throw new ValidationException("invalid_title", "Заголовок не может быть пустым");
        if (r.Title.Length > 150)
            throw new ValidationException("invalid_title", "Заголовок не длиннее 150 символов");

        if (string.IsNullOrWhiteSpace(r.Description))
            throw new ValidationException("invalid_description", "Описание не может быть пустым");
        if (r.Description.Length > 2000)
            throw new ValidationException("invalid_description", "Описание не длиннее 2000 символов");

        if (r.MaxParticipants is < 2 or > 10)
            throw new ValidationException("invalid_max_participants", "Размер группы от 2 до 10");

        if (r.Status is not (Open or Closed))
            throw new ValidationException("invalid_status", "Статус: open или closed");

        if (r.LanguageIds is null || r.LanguageIds.Count == 0)
            throw new ValidationException("invalid_languages", "Укажите хотя бы один язык");
        if (r.LanguageIds.Distinct().Count() != r.LanguageIds.Count)
            throw new ValidationException("invalid_languages", "Языки не должны повторяться");

        return r.LanguageIds;
    }

    // Проверяем, что все ссылки указывают на реальные записи справочников
    private async Task EnsureReferencesExistAsync(
        CreateAnnouncementRequest r, List<long> languageIds, CancellationToken ct)
    {
        if (!await db.Games.AnyAsync(g => g.Id == r.GameId, ct))
            throw new ValidationException("invalid_game", "Игра не найдена");

        if (!await db.GameModes.AnyAsync(m => m.Id == r.GameModeId && m.GameId == r.GameId, ct))
            throw new ValidationException("invalid_game_mode", "Режим не найден или не относится к выбранной игре");

        if (!await db.ExperienceLevels.AnyAsync(e => e.Id == r.ExperienceLevelId, ct))
            throw new ValidationException("invalid_experience_level", "Уровень опыта не найден");

        if (!await db.AgeCategories.AnyAsync(c => c.Id == r.AgeCategoryId, ct))
            throw new ValidationException("invalid_age_category", "Возрастная категория не найдена");

        var found = await db.Languages.CountAsync(l => languageIds.Contains(l.Id), ct);
        if (found != languageIds.Count)
            throw new ValidationException("invalid_languages", "Один или несколько языков не найдены");
    }
}