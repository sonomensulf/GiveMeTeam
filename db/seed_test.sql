-- Пользователи
INSERT INTO users (nickname, email, password_hash, birth_date) VALUES
    ('alice', 'alice@example.com', 'PLACEHOLDER_HASH', '1998-05-10'),
    ('bob',   'bob@example.com',   'PLACEHOLDER_HASH', '2001-03-22'),
    ('carol', 'carol@example.com', 'PLACEHOLDER_HASH', '1999-11-02'),
    ('dave',  'dave@example.com',  'PLACEHOLDER_HASH', '1995-07-15'),
    ('eve',   'eve@example.com',   'PLACEHOLDER_HASH', '2003-01-30'),
    ('frank', 'frank@example.com', 'PLACEHOLDER_HASH', '1992-09-09'),
    ('grace', 'grace@example.com', 'PLACEHOLDER_HASH', '1997-12-12'),
    ('heidi', 'heidi@example.com', 'PLACEHOLDER_HASH', '2002-04-18');

-- Языки пользователей
INSERT INTO user_languages (user_id, language_id)
SELECT u.id, l.id
FROM (VALUES
    ('alice','ru'), ('alice','en'),
    ('bob','ru'),
    ('carol','ru'), ('carol','en'),
    ('dave','ru'),
    ('eve','ru'),
    ('frank','en'),
    ('grace','en'), ('grace','ru'),
    ('heidi','en')
) AS t(nickname, lang_code)
JOIN users u ON u.nickname = t.nickname
JOIN languages l ON l.code = t.lang_code;

-- Объявления
INSERT INTO announcements
    (author_id, game_id, game_mode_id, title, description,
     experience_level_id, age_category_id, max_participants, status)
SELECT u.id, g.id, m.id, t.title, t.descr, e.id, a.id, t.max_p, t.status
FROM (VALUES
    ('alice','cs2','Premier','Ищу команду на Premier','Играем вечером, нужен микрофон','intermediate','all',5,'open'),
    ('dave','dota2','Ranked All Pick','Ищу дуо','Рейтинговые игры','experienced','all',2,'full'),
    ('frank','lol','Ранговая очередь','Старое объявление','Набор окончен','student','all',5,'closed'),
    ('grace','apex','Рейтинговый','Трио на Apex','Только 18+','newbie','18plus',3,'open')
) AS t(nick, game_code, mode_name, title, descr, exp_code, age_code, max_p, status)
JOIN users u ON u.nickname = t.nick
JOIN games g ON g.code = t.game_code
JOIN game_modes m ON m.game_id = g.id AND m.name = t.mode_name
JOIN experience_levels e ON e.code = t.exp_code
JOIN age_categories a ON a.code = t.age_code;

-- Языки объявлений
INSERT INTO announcement_languages (announcement_id, language_id)
SELECT an.id, l.id
FROM (VALUES
    ('alice','ru'), ('alice','en'),
    ('dave','ru'),
    ('frank','en'),
    ('grace','en'), ('grace','ru')
) AS t(nick, lang_code)
JOIN users u ON u.nickname = t.nick
JOIN announcements an ON an.author_id = u.id
JOIN languages l ON l.code = t.lang_code;

-- Заявки во всех статусах
INSERT INTO applications (announcement_id, applicant_id, status)
SELECT an.id, ap.id, t.status
FROM (VALUES
    ('alice','bob','accepted'),
    ('alice','carol','pending'),
    ('dave','eve','accepted'),
    ('grace','heidi','rejected'),
    ('grace','carol','withdrawn')
) AS t(author_nick, applicant_nick, status)
JOIN users au ON au.nickname = t.author_nick
JOIN announcements an ON an.author_id = au.id
JOIN users ap ON ap.nickname = t.applicant_nick;

-- Сообщения чата (в открытом объявлении alice)
INSERT INTO messages (announcement_id, sender_id, body, deleted_at)
SELECT an.id, u.id, t.body, t.deleted_at::timestamptz
FROM (VALUES
    ('alice','Привет всем!', NULL),
    ('bob','Привет, во сколько играем?', NULL),
    ('bob','Это сообщение удалено', '2026-11-15 12:00:00+03')
) AS t(nick, body, deleted_at)
JOIN users u ON u.nickname = t.nick
JOIN announcements an ON an.author_id = (SELECT id FROM users WHERE nickname = 'alice');