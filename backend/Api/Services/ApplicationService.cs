using GiveMeTeam.Data;
using GiveMeTeam.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public class ApplicationService(AppDbContext db, TimeProvider time)
{
    private const int MaxPendingApplications = 5;
    private const string Pending = ApplicationStatus.Pending,
                         Accepted = ApplicationStatus.Accepted,
                         Rejected = ApplicationStatus.Rejected,
                         Withdrawn = ApplicationStatus.Withdrawn;

    public async Task<ApplicationResponse> CreateAsync(
        long announcementId, long applicantId, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // транзакция T5
            // 1) Блокируем объявление (FOR SHARE): параллельные заявки на него
            //    не мешают друг другу, но принятие/закрытие (FOR UPDATE) ждёт нас
            var a = await db.Announcements
                .FromSqlInterpolated($"SELECT * FROM announcements WHERE id = {announcementId} FOR SHARE")
                .SingleOrDefaultAsync(ct);
            if (a is null)
                throw new NotFoundException("not_found", "Объявление не найдено");

            // 2) Блокируем заявителя (порядок: объявление -> пользователь)
            //    Это сериализует параллельные запросы одного пользователя,
            //    поэтому лимит в 5 pending нельзя обойти гонкой
            var birthDate = await db.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE id = {applicantId} FOR UPDATE")
                .Select(u => u.BirthDate)
                .SingleOrDefaultAsync(ct);
            if (birthDate == default)
                throw new ConflictException("user_not_found", "Пользователь не найден");

            // 3) Объявление в статусе open
            if (a.Status != AnnouncementStatus.Open)
                throw new ConflictException("announcement_not_open",
                    "Заявки принимаются только в объявления со статусом open");

            // 4) Не собственное объявление
            if (a.AuthorId == applicantId)
                throw new ConflictException("own_announcement",
                    "Нельзя подать заявку на собственное объявление");

            // 5) Возраст заявителя против min_age категории
            var minAge = await db.AgeCategories
                .Where(c => c.Id == a.AgeCategoryId)
                .Select(c => (int)c.MinAge)
                .SingleAsync(ct);
            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            if (AgeCalculator.FullYears(birthDate, today) < minAge)
                throw new ConflictException("age_restricted",
                    "Ваш возраст не подходит для выбранной возрастной категории");

            // 6) Заявитель не состоит в группе (нет заявки accepted)
            if (await db.Applications.AnyAsync(x =>
                    x.ApplicantId == applicantId && x.Status == Accepted, ct))
                throw new ConflictException("already_in_group",
                    "Вы состоите в группе и не можете подавать заявки");

            // 7) У заявителя нет собственного активного объявления
            if (await db.Announcements.AnyAsync(x =>
                    x.AuthorId == applicantId
                    && (x.Status == AnnouncementStatus.Open || x.Status == AnnouncementStatus.Full), ct))
                throw new ConflictException("active_announcement_exists",
                    "У вас есть активное объявление, вы не можете подавать заявки");

            // 8) Нет заявки rejected на это объявление (повтор запрещён).
            //    После withdrawn повторная подача разрешена
            if (await db.Applications.AnyAsync(x =>
                    x.AnnouncementId == announcementId && x.ApplicantId == applicantId
                    && x.Status == Rejected, ct))
                throw new ConflictException("application_rejected",
                    "Ваша заявка на это объявление была отклонена, повторная подача запрещена");

            // 9) Нет активной (pending/accepted) заявки на это объявление
            if (await db.Applications.AnyAsync(x =>
                    x.AnnouncementId == announcementId && x.ApplicantId == applicantId
                    && (x.Status == Pending || x.Status == Accepted), ct))
                throw new ConflictException("application_exists",
                    "Вы уже подали заявку на это объявление");

            // 10) Не более 5 заявок pending
            var pending = await db.Applications.CountAsync(x =>
                x.ApplicantId == applicantId && x.Status == Pending, ct);
            if (pending >= MaxPendingApplications)
                throw new ConflictException("pending_limit",
                    "Нельзя иметь более 5 заявок в ожидании");

            // 11) Вставка заявки
            var entity = new Application
            {
                AnnouncementId = announcementId,
                ApplicantId = applicantId,
                Status = Pending
            };
            db.Applications.Add(entity);
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return new ApplicationResponse(
                entity.Id, entity.AnnouncementId, entity.ApplicantId,
                entity.Status, entity.CreatedAt);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Страховка от гонки: частичный уникальный индекс
            // uq_applications_announcement_applicant_active
            throw new ConflictException("application_exists",
                "Вы уже подали заявку на это объявление");
        }
    }

    public async Task<IReadOnlyList<MyApplicationItem>> ListMineAsync(
        long applicantId, CancellationToken ct)
    {
        // ── Только заявки этого пользователя, в любых статусах, новые первыми ──
        var rows = await db.Applications
            .AsNoTracking()
            .Where(a => a.ApplicantId == applicantId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new MyApplicationItem(
                a.Id,
                a.Status,
                a.CreatedAt,
                a.UpdatedAt,
                new ApplicationAnnouncementBrief(
                    a.Announcement.Id,
                    a.Announcement.Title,
                    a.Announcement.Status,
                    a.Announcement.AuthorId,
                    a.Announcement.Author.Nickname,
                    new NamedRef(a.Announcement.Game.Id, a.Announcement.Game.Name),
                    new NamedRef(a.Announcement.GameMode.Id, a.Announcement.GameMode.Name))))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<IncomingApplicationItem>> ListIncomingAsync(
        long authorId, CancellationToken ct)
    {
        // ── Заявки на объявления этого автора, в любых статусах, новые первыми ──
        var rows = await db.Applications
            .AsNoTracking()
            .Where(a => a.Announcement.AuthorId == authorId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Status,
                a.CreatedAt,
                a.UpdatedAt,
                AnnouncementId = a.Announcement.Id,
                AnnouncementTitle = a.Announcement.Title,
                AnnouncementStatus = a.Announcement.Status,
                ApplicantId = a.Applicant.Id,
                ApplicantNickname = a.Applicant.Nickname,
                ApplicantBirthDate = a.Applicant.BirthDate
            })
            .ToListAsync(ct);

        // ── Возраст считаем в C#, превращаем строки в DTO ──
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return rows.Select(r => new IncomingApplicationItem(
            r.Id,
            r.Status,
            r.CreatedAt,
            r.UpdatedAt,
            new ApplicantBrief(
                r.ApplicantId,
                r.ApplicantNickname,
                AgeCalculator.FullYears(r.ApplicantBirthDate, today)),
            new IncomingAnnouncementBrief(
                r.AnnouncementId,
                r.AnnouncementTitle,
                r.AnnouncementStatus))).ToList();
    }

    public async Task<IReadOnlyList<IncomingApplicationItem>> ListForAnnouncementAsync(
        long announcementId, long currentUserId, CancellationToken ct)
    {
        // ── 1. Объявление существует? Берём только id автора ──
        var authorId = await db.Announcements
            .AsNoTracking()
            .Where(a => a.Id == announcementId)
            .Select(a => (long?)a.AuthorId)
            .SingleOrDefaultAsync(ct);
        if (authorId is null)
            throw new NotFoundException("not_found", "Объявление не найдено");

        // ── 2. Смотреть заявки может только автор ──
        if (authorId != currentUserId)
            throw new ForbiddenException("forbidden", "Просматривать заявки может только автор объявления");

        // ── 3. Заявки на это объявление, в любых статусах, новые первыми ──
        var rows = await db.Applications
            .AsNoTracking()
            .Where(a => a.AnnouncementId == announcementId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Status,
                a.CreatedAt,
                a.UpdatedAt,
                AnnouncementId = a.Announcement.Id,
                AnnouncementTitle = a.Announcement.Title,
                AnnouncementStatus = a.Announcement.Status,
                ApplicantId = a.Applicant.Id,
                ApplicantNickname = a.Applicant.Nickname,
                ApplicantBirthDate = a.Applicant.BirthDate
            })
            .ToListAsync(ct);

        // ── 4. Возраст считаем в C#, превращаем строки в DTO ──
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return rows.Select(r => new IncomingApplicationItem(
            r.Id,
            r.Status,
            r.CreatedAt,
            r.UpdatedAt,
            new ApplicantBrief(
                r.ApplicantId,
                r.ApplicantNickname,
                AgeCalculator.FullYears(r.ApplicantBirthDate, today)),
            new IncomingAnnouncementBrief(
                r.AnnouncementId,
                r.AnnouncementTitle,
                r.AnnouncementStatus))).ToList();
    }

    public async Task<ApplicationResponse> AcceptAsync(
        long applicationId, long currentUserId, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // транзакция T6
            // 0) Узнаём, к какому объявлению относится заявка (без блокировки).
            //    Нужно только для того, чтобы первым заблокировать объявление
            var announcementId = await db.Applications
                .AsNoTracking()
                .Where(x => x.Id == applicationId)
                .Select(x => (long?)x.AnnouncementId)
                .SingleOrDefaultAsync(ct);
            if (announcementId is null)
                throw new NotFoundException("not_found", "Заявка не найдена");

            // 1) Блокируем объявление (порядок: объявление -> пользователь -> заявки)
            var a = await db.Announcements
                .FromSqlInterpolated($"SELECT * FROM announcements WHERE id = {announcementId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (a is null)
                throw new NotFoundException("not_found", "Заявка не найдена");

            // 2) Только автор объявления
            if (a.AuthorId != currentUserId)
                throw new ForbiddenException("forbidden", "Принимать заявки может только автор объявления");

            // 3) Перечитываем заявку уже под блокировкой: за время ожидания
            //    её могли отозвать, отклонить или удалить вместе с объявлением
            var app = await db.Applications.SingleOrDefaultAsync(x => x.Id == applicationId, ct);
            if (app is null)
                throw new NotFoundException("not_found", "Заявка не найдена");

            // 4) Принять можно только заявку в статусе pending
            ApplicationStatus.EnsureTransition(app.Status, Accepted);

            // 5) Объявление должно быть в статусе open
            if (a.Status != AnnouncementStatus.Open)
                throw new ConflictException("announcement_not_open",
                    "Заявки принимаются только в объявлении со статусом open");

            // 6) Есть свободное место (автор + принятые < max_participants)
            var participants = await db.Applications
                .CountAsync(x => x.AnnouncementId == a.Id && x.Status == Accepted, ct) + 1;
            if (participants >= a.MaxParticipants)
                throw new ConflictException("group_full", "В группе нет свободных мест");

            // 7) Блокируем заявителя (порядок: объявление -> пользователь)
            await db.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE id = {app.ApplicantId} FOR UPDATE")
                .Select(u => u.Id)
                .SingleAsync(ct);

            // 8) У заявителя нет заявки accepted (он не состоит в другой группе)
            if (await db.Applications.AnyAsync(x =>
                    x.ApplicantId == app.ApplicantId && x.Status == Accepted, ct))
                throw new ConflictException("already_in_group",
                    "Пользователь уже состоит в другой группе");

            // 9) У заявителя нет собственного активного объявления
            if (await db.Announcements.AnyAsync(x =>
                    x.AuthorId == app.ApplicantId
                    && (x.Status == AnnouncementStatus.Open || x.Status == AnnouncementStatus.Full), ct))
                throw new ConflictException("active_announcement_exists",
                    "У пользователя есть активное объявление");

            // 10) Заявка -> accepted
            app.Status = Accepted;
            await db.SaveChangesAsync(ct);

            // 11) Остальные pending-заявки заявителя -> withdrawn
            await db.Applications
                .Where(x => x.ApplicantId == app.ApplicantId
                            && 	x.Status == Pending && x.Id != applicationId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, Withdrawn), ct);

            // 12) Если группа набрана, объявление -> full
            var targetStatus = AnnouncementStatus.Recalculate(a.Status, participants + 1, a.MaxParticipants);
            if (targetStatus != a.Status)
            {
                AnnouncementStatus.EnsureTransition(a.Status, targetStatus);
                a.Status = targetStatus;
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);

            return new ApplicationResponse(
                app.Id, app.AnnouncementId, app.ApplicantId, app.Status, app.CreatedAt);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Страховка от гонки: частичный уникальный индекс
            // uq_applications_applicant_accepted не даёт иметь две заявки accepted
            throw new ConflictException("already_in_group",
                "Пользователь уже состоит в другой группе");
        }
    }

    public async Task<ApplicationResponse> RejectAsync(
        long applicationId, long currentUserId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // транзакция T7 (отклонение заявки / исключение участника)
        // 0) Узнаём объявление заявки (без блокировки), чтобы заблокировать его первым
        var announcementId = await db.Applications
            .AsNoTracking()
            .Where(x => x.Id == applicationId)
            .Select(x => (long?)x.AnnouncementId)
            .SingleOrDefaultAsync(ct);
        if (announcementId is null)
            throw new NotFoundException("not_found", "Заявка не найдена");

        // 1) Блокируем объявление
        var a = await db.Announcements
            .FromSqlInterpolated($"SELECT * FROM announcements WHERE id = {announcementId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (a is null)
            throw new NotFoundException("not_found", "Заявка не найдена");

        // 2) Только автор объявления
        if (a.AuthorId != currentUserId)
            throw new ForbiddenException("forbidden", "Отклонять заявки может только автор объявления");

        // 3) Перечитываем заявку под блокировкой
        var app = await db.Applications.SingleOrDefaultAsync(x => x.Id == applicationId, ct);
        if (app is null)
            throw new NotFoundException("not_found", "Заявка не найдена");

        // 4) Переход pending/accepted -> rejected должен быть разрешён таблицей
        ApplicationStatus.EnsureTransition(app.Status, Rejected);

        // 5) Заявка -> rejected
        var wasAccepted = app.Status == Accepted;
        app.Status = Rejected;
        await db.SaveChangesAsync(ct);

        // 6) Если исключили участника и объявление было full, место освободилось -> open
        if (wasAccepted)
        {
            var participants = await db.Applications
                .CountAsync(x => x.AnnouncementId == a.Id && x.Status == Accepted, ct) + 1;
            var targetStatus = AnnouncementStatus.Recalculate(a.Status, participants, a.MaxParticipants);
            if (targetStatus != a.Status)
            {
                AnnouncementStatus.EnsureTransition(a.Status, targetStatus);
                a.Status = targetStatus;
                await db.SaveChangesAsync(ct);
            }
        }

        await tx.CommitAsync(ct);

        return new ApplicationResponse(
            app.Id, app.AnnouncementId, app.ApplicantId, app.Status, app.CreatedAt);
    }

    public async Task<ApplicationResponse> WithdrawAsync(
        long applicationId, long currentUserId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // транзакция T7 (отзыв заявки / выход из группы)
        // 0) Узнаём объявление заявки (без блокировки), чтобы заблокировать его первым
        var announcementId = await db.Applications
            .AsNoTracking()
            .Where(x => x.Id == applicationId)
            .Select(x => (long?)x.AnnouncementId)
            .SingleOrDefaultAsync(ct);
        if (announcementId is null)
            throw new NotFoundException("not_found", "Заявка не найдена");

        // 1) Блокируем объявление
        var a = await db.Announcements
            .FromSqlInterpolated($"SELECT * FROM announcements WHERE id = {announcementId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (a is null)
            throw new NotFoundException("not_found", "Заявка не найдена");

        // 2) Перечитываем заявку под блокировкой
        var app = await db.Applications.SingleOrDefaultAsync(x => x.Id == applicationId, ct);
        if (app is null)
            throw new NotFoundException("not_found", "Заявка не найдена");

        // 3) Только заявитель
        if (app.ApplicantId != currentUserId)
            throw new ForbiddenException("forbidden", "Отозвать заявку может только заявитель");

        // 4) Переход pending/accepted -> withdrawn должен быть разрешён таблицей
        ApplicationStatus.EnsureTransition(app.Status, Withdrawn);

        // 5) Заявка -> withdrawn
        var wasAccepted = app.Status == Accepted;
        app.Status = Withdrawn;
        await db.SaveChangesAsync(ct);

        // 6) Если вышел участник и объявление было full, место освободилось -> open
        if (wasAccepted)
        {
            var participants = await db.Applications
                .CountAsync(x => x.AnnouncementId == a.Id && x.Status == Accepted, ct) + 1;
            var targetStatus = AnnouncementStatus.Recalculate(a.Status, participants, a.MaxParticipants);
            if (targetStatus != a.Status)
            {
                AnnouncementStatus.EnsureTransition(a.Status, targetStatus);
                a.Status = targetStatus;
                await db.SaveChangesAsync(ct);
            }
        }

        await tx.CommitAsync(ct);

        return new ApplicationResponse(
            app.Id, app.AnnouncementId, app.ApplicantId, app.Status, app.CreatedAt);
    }
}