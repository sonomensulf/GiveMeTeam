using GiveMeTeam.Data;
using GiveMeTeam.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public class ApplicationService(AppDbContext db, TimeProvider time)
{
    private const int MaxPendingApplications = 5;

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
                    x.ApplicantId == applicantId && x.Status == "accepted", ct))
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
                    && x.Status == "rejected", ct))
                throw new ConflictException("application_rejected",
                    "Ваша заявка на это объявление была отклонена, повторная подача запрещена");

            // 9) Нет активной (pending/accepted) заявки на это объявление
            if (await db.Applications.AnyAsync(x =>
                    x.AnnouncementId == announcementId && x.ApplicantId == applicantId
                    && (x.Status == "pending" || x.Status == "accepted"), ct))
                throw new ConflictException("application_exists",
                    "Вы уже подали заявку на это объявление");

            // 10) Не более 5 заявок pending
            var pending = await db.Applications.CountAsync(x =>
                x.ApplicantId == applicantId && x.Status == "pending", ct);
            if (pending >= MaxPendingApplications)
                throw new ConflictException("pending_limit",
                    "Нельзя иметь более 5 заявок в ожидании");

            // 11) Вставка заявки
            var entity = new Application
            {
                AnnouncementId = announcementId,
                ApplicantId = applicantId,
                Status = "pending"
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
}