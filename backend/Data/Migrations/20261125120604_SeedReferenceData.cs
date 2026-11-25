using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO countries (code, name) VALUES
                    ('ru', 'Россия'),
                    ('by', 'Беларусь'),
                    ('kz', 'Казахстан'),
                    ('ua', 'Украина'),
                    ('us', 'США'),
                    ('de', 'Германия'),
                    ('gb', 'Великобритания'),
                    ('fr', 'Франция'),
                    ('es', 'Испания'),
                    ('cn', 'Китай'),
                    ('jp', 'Япония');

                INSERT INTO languages (code, name) VALUES
                    ('ru', 'Русский'),
                    ('en', 'Английский'),
                    ('de', 'Немецкий'),
                    ('es', 'Испанский'),
                    ('fr', 'Французский'),
                    ('zh', 'Китайский'),
                    ('jp', 'Японский');

                INSERT INTO games (code, name) VALUES
                    ('cs2', 'Counter-Strike 2'),
                    ('dota2', 'Dota 2'),
                    ('fortnite', 'Fortnite'),
                    ('apex', 'Apex Legends'),
                    ('pubg', 'PUBG: BATTLEGROUNDS'),
                    ('lol', 'League of Legends'),
                    ('wot', 'World of Tanks');

                INSERT INTO game_modes (game_id, name)
                SELECT g.id, m.name
                FROM (VALUES
                    ('cs2', 'Premier'),
                    ('cs2', 'Соревновательный'),
                    ('cs2', 'Напарники'),
                    ('cs2', 'Обычная игра'),
                    ('dota2', 'Ranked All Pick'),
                    ('dota2', 'Random Draft'),
                    ('dota2', 'Turbo'),
                    ('dota2', 'Обычная игра'),
                    ('fortnite', 'Королевская битва'),
                    ('fortnite', 'Нулевая высота'),
                    ('fortnite', 'Перезарядка'),
                    ('fortnite', 'Блиц-рояль'),
                    ('apex', 'Рейтинговый'),
                    ('apex', 'Обычная игра'),
                    ('apex', 'Сборник'),
                    ('pubg', 'Обычная игра'),
                    ('pubg', 'Рейтинговый'),
                    ('pubg', 'Командный бой насмерть'),
                    ('pubg', 'Экстремальная королевская битва'),
                    ('lol', 'Обычная игра'),
                    ('lol', 'Режим выбора'),
                    ('lol', 'Ранговая очередь'),
                    ('lol', 'Арена'),
                    ('wot', 'Ранговые бои'),
                    ('wot', 'Натиск'),
                    ('wot', 'Стандартный бой'),
                    ('wot', 'Встречный бой'),
                    ('wot', 'Штурм'),
                    ('wot', 'Генеральное сражение')
                ) AS m(game_code, name)
                JOIN games g ON g.code = m.game_code;

                INSERT INTO experience_levels (code, name) VALUES
                    ('newbie', 'Новичок 0-50 часов'),
                    ('student', 'Ученик 51-100 часов'),
                    ('intermediate', 'Средний 101-200 часов'),
                    ('experienced', 'Опытный 201-500 часов'),
                    ('pro', 'Профессионал 500+ часов');

                INSERT INTO age_categories (code, name, min_age) VALUES
                    ('all', 'Для всех', 0),
                    ('14plus', '14+', 14),
                    ('16plus', '16+', 16),
                    ('18plus', '18+', 18),
                    ('22plus', '22+', 22),
                    ('30plus', '30+', 30);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM game_modes;
                DELETE FROM games;
                DELETE FROM languages;
                DELETE FROM countries;
                DELETE FROM experience_levels;
                DELETE FROM age_categories;
            ");
        }
    }
}
