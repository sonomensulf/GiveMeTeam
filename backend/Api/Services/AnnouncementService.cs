using GiveMeTeam.Data;
using GiveMeTeam.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public class AnnouncementService(AppDbContext db, TimeProvider time)
{
    private const int MaxAnnouncementsPerAuthor = 10;
    private const string Open = AnnouncementStatus.Open,
                     Full = AnnouncementStatus.Full,
                     Closed = AnnouncementStatus.Closed;

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

    public async Task<AnnouncementDetails> UpdateAsync(
        long id, long currentUserId, UpdateAnnouncementRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // транзакция T8
        // 1) Блокируем строку объявления
        var a = await db.Announcements
            .FromSqlInterpolated($"SELECT * FROM announcements WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (a is null)
            throw new NotFoundException("not_found", "Объявление не найдено");

        // 2) Только автор
        if (a.AuthorId != currentUserId)
            throw new ForbiddenException("forbidden", "Редактировать объявление может только автор");

        // 2.1) проверки формата, не требующие блокировок
        ValidateUpdate(r);

        // 3) Итоговые значения (что пришло, иначе текущее)
        var newGameId = r.GameId ?? a.GameId;
        var newModeId = r.GameModeId ?? a.GameModeId;
        var newAgeId = r.AgeCategoryId ?? a.AgeCategoryId;
        var newExpId = r.ExperienceLevelId ?? a.ExperienceLevelId;

        var currentLangIds = await db.AnnouncementLanguages
            .Where(l => l.AnnouncementId == id)
            .Select(l => l.LanguageId)
            .ToListAsync(ct);
        var newLangIds = r.LanguageIds ?? currentLangIds;

        // 4) Какие «защищённые» поля реально меняются
        var restrictedChanged =
            newGameId != a.GameId
            || newModeId != a.GameModeId
            || newAgeId != a.AgeCategoryId
            || !newLangIds.ToHashSet().SetEquals(currentLangIds);

        if (restrictedChanged
            && await db.Applications.AnyAsync(x => x.AnnouncementId == id, ct))
            throw new ConflictException("fields_locked",
                "Игру, режим, возрастную категорию и языки нельзя менять, если есть заявки");

        // 5) Проверки существования справочников (только для изменённых значений)
        if (newGameId != a.GameId
            && !await db.Games.AnyAsync(g => g.Id == newGameId, ct))
            throw new ValidationException("invalid_game", "Игра не найдена");

        if ((newGameId != a.GameId || newModeId != a.GameModeId)
            && !await db.GameModes.AnyAsync(m => m.Id == newModeId && m.GameId == newGameId, ct))
            throw new ValidationException("invalid_game_mode",
                "Режим не найден или не относится к выбранной игре");

        if (newExpId != a.ExperienceLevelId
            && !await db.ExperienceLevels.AnyAsync(e => e.Id == newExpId, ct))
            throw new ValidationException("invalid_experience_level", "Уровень опыта не найден");

        if (r.LanguageIds is not null)
        {
            var found = await db.Languages.CountAsync(l => newLangIds.Contains(l.Id), ct);
            if (found != newLangIds.Count)
                throw new ValidationException("invalid_languages", "Один или несколько языков не найдены");
        }

        // 6) Смена возрастной категории: возраст автора должен подходить
        if (newAgeId != a.AgeCategoryId)
        {
            var minAge = await db.AgeCategories
                .Where(c => c.Id == newAgeId)
                .Select(c => (int?)c.MinAge)
                .SingleOrDefaultAsync(ct);
            if (minAge is null)
                throw new ValidationException("invalid_age_category", "Возрастная категория не найдена");

            var birthDate = await db.Users
                .Where(u => u.Id == a.AuthorId)
                .Select(u => u.BirthDate)
                .SingleAsync(ct);
            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            if (AgeCalculator.FullYears(birthDate, today) < minAge)
                throw new ConflictException("age_restricted",
                    "Ваш возраст не подходит для выбранной возрастной категории");
        }

        // 7) max_participants не меньше числа участников (автор + accepted)
        var participants = await db.Applications
            .CountAsync(x => x.AnnouncementId == id && x.Status == "accepted", ct) + 1;
        var newMax = r.MaxParticipants ?? a.MaxParticipants;
        if (newMax < participants)
            throw new ConflictException("max_participants_too_small",
                "Размер группы не может быть меньше текущего числа участников");

        // 8) Применяем изменения
        a.GameId = newGameId;
        a.GameModeId = newModeId;
        a.AgeCategoryId = newAgeId;
        a.ExperienceLevelId = newExpId;
        a.MaxParticipants = (short)newMax;
        if (r.Title is not null) a.Title = r.Title.Trim();
        if (r.Description is not null) a.Description = r.Description.Trim();

        // Языки: удаляем лишние, добавляем недостающие
        if (r.LanguageIds is not null)
        {
            var wanted = newLangIds.ToHashSet();
            var rows = await db.AnnouncementLanguages
                .Where(l => l.AnnouncementId == id)
                .ToListAsync(ct);

            db.AnnouncementLanguages.RemoveRange(rows.Where(x => !wanted.Contains(x.LanguageId)));

            var have = rows.Select(x => x.LanguageId).ToHashSet();
            foreach (var langId in wanted.Where(l => !have.Contains(l)))
                db.AnnouncementLanguages.Add(
                    new AnnouncementLanguage { AnnouncementId = id, LanguageId = langId });
        }

        // 9) Пересчёт full <-> open (closed не трогаем)
        var targetStatus = a.Status;
        if (a.Status == Open && participants >= newMax) targetStatus = Full;
        else if (a.Status == Full && participants < newMax) targetStatus = Open;

        if (targetStatus != a.Status)
        {
            AnnouncementStatus.EnsureTransition(a.Status, targetStatus);
            a.Status = targetStatus;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetAsync(id, currentUserId, ct);
    }

    // Формат полей: проверяем только то, что пришло
    private static void ValidateUpdate(UpdateAnnouncementRequest r)
    {
        if (r.GameId is null && r.GameModeId is null && r.Title is null
            && r.Description is null && r.ExperienceLevelId is null
            && r.LanguageIds is null && r.AgeCategoryId is null && r.MaxParticipants is null)
            throw new ValidationException("empty_update", "Не указано ни одного поля для изменения");

        if (r.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(r.Title))
                throw new ValidationException("invalid_title", "Заголовок не может быть пустым");
            if (r.Title.Length > 150)
                throw new ValidationException("invalid_title", "Заголовок не длиннее 150 символов");
        }

        if (r.Description is not null)
        {
            if (string.IsNullOrWhiteSpace(r.Description))
                throw new ValidationException("invalid_description", "Описание не может быть пустым");
            if (r.Description.Length > 2000)
                throw new ValidationException("invalid_description", "Описание не длиннее 2000 символов");
        }

        if (r.MaxParticipants is < 2 or > 10)
            throw new ValidationException("invalid_max_participants", "Размер группы от 2 до 10");

        if (r.LanguageIds is not null)
        {
            if (r.LanguageIds.Count == 0)
                throw new ValidationException("invalid_languages", "Укажите хотя бы один язык");
            if (r.LanguageIds.Distinct().Count() != r.LanguageIds.Count)
                throw new ValidationException("invalid_languages", "Языки не должны повторяться");
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

    public async Task<AnnouncementDetails> GetAsync(
        long id, long? currentUserId, CancellationToken ct)
    {
        // ── 1. Один запрос: объявление + автор + справочники + языки + принятые участники ──
        var row = await db.Announcements
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                a.AuthorId,
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
                // принятые участники (автор добавляется отдельно)
                Accepted = a.Applications
                    .Where(x => x.Status == "accepted")
                    .OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Applicant.Id,
                        x.Applicant.Nickname,
                        x.Applicant.BirthDate
                    })
                    .ToList(),
                a.MaxParticipants,
                a.Status,
                a.CreatedAt,
                a.UpdatedAt
            })
            .SingleOrDefaultAsync(ct);

        // ── 2. Нет такого объявления, либо оно закрыто и смотрит не автор ──
        if (row is null || (row.Status == Closed && row.AuthorId != currentUserId))
            throw new NotFoundException("not_found", "Объявление не найдено");

        // ── 3. Собираем состав группы: автор первым, затем принятые ──
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var authorAge = AgeCalculator.FullYears(row.AuthorBirthDate, today);

        var members = new List<GroupMember>
        {
            new(row.AuthorId, row.AuthorNickname, authorAge, true)
        };
        members.AddRange(row.Accepted.Select(m =>
            new GroupMember(m.Id, m.Nickname, AgeCalculator.FullYears(m.BirthDate, today), false)));

        var participants = members.Count;                       // автор + принятые
        var freeSlots = Math.Max(0, row.MaxParticipants - participants);

        return new AnnouncementDetails(
            row.Id,
            row.Title,
            row.Description,
            new AuthorBrief(row.AuthorId, row.AuthorNickname, authorAge),
            new NamedRef(row.GameId, row.GameName),
            new NamedRef(row.ModeId, row.ModeName),
            new NamedRef(row.ExpId, row.ExpName),
            new NamedRef(row.AgeId, row.AgeName),
            row.Languages,
            row.MaxParticipants,
            participants,
            freeSlots,
            members,
            row.Status,
            row.CreatedAt,
            row.UpdatedAt);
    }

    public async Task<IReadOnlyList<AnnouncementListItem>> ListMineAsync(
        long authorId, CancellationToken ct)
    {
        // ── 1. Только объявления этого автора, в любых статусах ──
        var rows = await db.Announcements
            .AsNoTracking()
            .Where(a => a.AuthorId == authorId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
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

        // ── 2. Возраст считаем в C#, превращаем строки в DTO ──
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return rows.Select(r => new AnnouncementListItem(
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
    }
    
    public async Task<AnnouncementDetails> ActivateAsync(
        long id, long currentUserId, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // транзакция T3
            // 1) Блокируем объявление
            var a = await db.Announcements
                .FromSqlInterpolated($"SELECT * FROM announcements WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (a is null)
                throw new NotFoundException("not_found", "Объявление не найдено");

            // 2) Только автор
            if (a.AuthorId != currentUserId)
                throw new ForbiddenException("forbidden", "Активировать объявление может только автор");

            // 3) Блокируем автора (порядок: объявление -> пользователь)
            var birthDate = await db.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE id = {currentUserId} FOR UPDATE")
                .Select(u => u.BirthDate)
                .SingleAsync(ct);

            // 4) Активировать можно только закрытое объявление
            if (a.Status != Closed)
                throw new ConflictException("invalid_status_transition",
                    "Активировать можно только закрытое объявление");

            // 5) Нет другого активного объявления
            if (await db.Announcements.AnyAsync(x =>
                    x.AuthorId == currentUserId && x.Id != id
                    && (x.Status == Open || x.Status == Full), ct))
                throw new ConflictException("active_announcement_exists",
                    "У вас уже есть активное объявление");

            // 6) Автор не состоит в группе (нет заявки accepted)
            if (await db.Applications.AnyAsync(x =>
                    x.ApplicantId == currentUserId && x.Status == "accepted", ct))
                throw new ConflictException("already_in_group",
                    "Вы состоите в группе и не можете активировать объявление");

            // 7) Возраст автора против min_age категории
            var minAge = await db.AgeCategories
                .Where(c => c.Id == a.AgeCategoryId)
                .Select(c => (int)c.MinAge)
                .SingleAsync(ct);
            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            if (AgeCalculator.FullYears(birthDate, today) < minAge)
                throw new ConflictException("age_restricted",
                    "Ваш возраст не подходит для выбранной возрастной категории");

            // 8) Статус -> open
            a.Status = Open;
            await db.SaveChangesAsync(ct);

            // 9) Свои pending-заявки автора -> withdrawn
            await db.Applications
                .Where(x => x.ApplicantId == currentUserId && x.Status == "pending")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "withdrawn"), ct);

            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Страховка от гонки: частичный уникальный индекс не даёт
            // иметь два активных объявления у одного автора.
            throw new ConflictException("active_announcement_exists",
                "У вас уже есть активное объявление");
        }

        return await GetAsync(id, currentUserId, ct);
    }

    public async Task<AnnouncementDetails> CloseAsync(
        long id, long currentUserId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // транзакция T4
        // 1) Блокируем объявление
        var a = await db.Announcements
            .FromSqlInterpolated($"SELECT * FROM announcements WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (a is null)
            throw new NotFoundException("not_found", "Объявление не найдено");

        // 2) Только автор
        if (a.AuthorId != currentUserId)
            throw new ForbiddenException("forbidden", "Закрыть объявление может только автор");

        // 3) Переход в closed должен быть разрешён таблицей (из open или full)
        AnnouncementStatus.EnsureTransition(a.Status, Closed);

        // 4) Заявки pending и accepted -> withdrawn
        await db.Applications
            .Where(x => x.AnnouncementId == id
                        && (x.Status == "pending" || x.Status == "accepted"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "withdrawn"), ct);

        // 5) Все ещё не удалённые сообщения -> deleted_at = now
        var now = time.GetUtcNow();
        await db.Messages
            .Where(m => m.AnnouncementId == id && m.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.DeletedAt, now), ct);

        // 6) Статус -> closed
        a.Status = Closed;
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return await GetAsync(id, currentUserId, ct);
    }

    public async Task DeleteAsync(long id, long currentUserId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // 1) Блокируем объявление
        var a = await db.Announcements
            .FromSqlInterpolated($"SELECT * FROM announcements WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (a is null)
            throw new NotFoundException("not_found", "Объявление не найдено");

        // 2) Только автор
        if (a.AuthorId != currentUserId)
            throw new ForbiddenException("forbidden", "Удалить объявление может только автор");

        // 3) Удалять можно только закрытое объявление
        if (a.Status != Closed)
            throw new ConflictException("invalid_status_transition",
                "Удалить можно только закрытое объявление");

        // 4) Удаляем; заявки, сообщения и языки удалит БД каскадом
        db.Announcements.Remove(a);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
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