CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;
CREATE TABLE age_categories (
    id bigint GENERATED ALWAYS AS IDENTITY,
    code character varying(20) NOT NULL,
    name character varying(50) NOT NULL,
    min_age smallint NOT NULL,
    CONSTRAINT pk_age_categories PRIMARY KEY (id),
    CONSTRAINT ck_age_categories_min_age_non_negative CHECK (min_age >= 0),
    CONSTRAINT ck_age_categories_name_not_empty CHECK (length(trim(name)) > 0)
);

CREATE TABLE countries (
    id bigint GENERATED ALWAYS AS IDENTITY,
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    CONSTRAINT pk_countries PRIMARY KEY (id),
    CONSTRAINT ck_countries_name_not_empty CHECK (length(trim(name)) > 0)
);

CREATE TABLE experience_levels (
    id bigint GENERATED ALWAYS AS IDENTITY,
    code character varying(20) NOT NULL,
    name character varying(50) NOT NULL,
    CONSTRAINT pk_experience_levels PRIMARY KEY (id),
    CONSTRAINT ck_experience_levels_name_not_empty CHECK (length(trim(name)) > 0)
);

CREATE TABLE games (
    id bigint GENERATED ALWAYS AS IDENTITY,
    code character varying(50) NOT NULL,
    name character varying(100) NOT NULL,
    CONSTRAINT pk_games PRIMARY KEY (id),
    CONSTRAINT ck_games_name_not_empty CHECK (length(trim(name)) > 0)
);

CREATE TABLE languages (
    id bigint GENERATED ALWAYS AS IDENTITY,
    code character varying(10) NOT NULL,
    name character varying(50) NOT NULL,
    CONSTRAINT pk_languages PRIMARY KEY (id),
    CONSTRAINT ck_languages_name_not_empty CHECK (length(trim(name)) > 0)
);

CREATE TABLE users (
    id bigint GENERATED ALWAYS AS IDENTITY,
    nickname character varying(50) NOT NULL,
    email character varying(255) NOT NULL,
    password_hash character varying(255) NOT NULL,
    birth_date date NOT NULL,
    country_id bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT (now()),
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT ck_users_birth_date_not_future CHECK (birth_date <= current_date),
    CONSTRAINT ck_users_email_lower CHECK (email = lower(email)),
    CONSTRAINT ck_users_nickname_not_empty CHECK (length(trim(nickname)) > 0),
    CONSTRAINT fk_users_countries_country_id FOREIGN KEY (country_id) REFERENCES countries (id) ON DELETE RESTRICT
);

CREATE TABLE game_modes (
    id bigint GENERATED ALWAYS AS IDENTITY,
    game_id bigint NOT NULL,
    name character varying(50) NOT NULL,
    CONSTRAINT pk_game_modes PRIMARY KEY (id),
    CONSTRAINT uq_game_modes_game_id_id UNIQUE (game_id, id),
    CONSTRAINT ck_game_modes_name_not_empty CHECK (length(trim(name)) > 0),
    CONSTRAINT fk_game_modes_games_game_id FOREIGN KEY (game_id) REFERENCES games (id) ON DELETE RESTRICT
);

CREATE TABLE announcements (
    id bigint GENERATED ALWAYS AS IDENTITY,
    author_id bigint NOT NULL,
    game_id bigint NOT NULL,
    game_mode_id bigint NOT NULL,
    title character varying(150) NOT NULL,
    description text NOT NULL,
    experience_level_id bigint NOT NULL,
    age_category_id bigint NOT NULL,
    max_participants smallint NOT NULL,
    status character varying(6) NOT NULL DEFAULT 'open',
    created_at timestamptz NOT NULL DEFAULT (now()),
    updated_at timestamptz NOT NULL DEFAULT (now()),
    CONSTRAINT pk_announcements PRIMARY KEY (id),
    CONSTRAINT ck_announcements_description_length CHECK (length(description) <= 2000),
    CONSTRAINT ck_announcements_description_not_empty CHECK (length(trim(description)) > 0),
    CONSTRAINT ck_announcements_max_participants CHECK (max_participants BETWEEN 2 AND 10),
    CONSTRAINT ck_announcements_status CHECK (status IN ('open', 'full', 'closed')),
    CONSTRAINT ck_announcements_title_not_empty CHECK (length(trim(title)) > 0),
    CONSTRAINT ck_announcements_updated_at CHECK (updated_at >= created_at),
    CONSTRAINT fk_announcements_age_categories_age_category_id FOREIGN KEY (age_category_id) REFERENCES age_categories (id) ON DELETE RESTRICT,
    CONSTRAINT fk_announcements_experience_levels_experience_level_id FOREIGN KEY (experience_level_id) REFERENCES experience_levels (id) ON DELETE RESTRICT,
    CONSTRAINT fk_announcements_game_modes_game_id_game_mode_id FOREIGN KEY (game_id, game_mode_id) REFERENCES game_modes (game_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_announcements_games_game_id FOREIGN KEY (game_id) REFERENCES games (id) ON DELETE RESTRICT,
    CONSTRAINT fk_announcements_users_author_id FOREIGN KEY (author_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE announcement_languages (
    announcement_id bigint NOT NULL,
    language_id bigint NOT NULL,
    CONSTRAINT pk_announcement_languages PRIMARY KEY (announcement_id, language_id),
    CONSTRAINT fk_announcement_languages_announcements_announcement_id FOREIGN KEY (announcement_id) REFERENCES announcements (id) ON DELETE CASCADE,
    CONSTRAINT fk_announcement_languages_languages_language_id FOREIGN KEY (language_id) REFERENCES languages (id) ON DELETE RESTRICT
);

CREATE TABLE applications (
    id bigint GENERATED ALWAYS AS IDENTITY,
    announcement_id bigint NOT NULL,
    applicant_id bigint NOT NULL,
    status character varying(9) NOT NULL DEFAULT 'pending',
    created_at timestamptz NOT NULL DEFAULT (now()),
    updated_at timestamptz NOT NULL DEFAULT (now()),
    CONSTRAINT pk_applications PRIMARY KEY (id),
    CONSTRAINT ck_applications_status CHECK (status IN ('pending', 'accepted', 'rejected', 'withdrawn')),
    CONSTRAINT ck_applications_updated_at CHECK (updated_at >= created_at),
    CONSTRAINT fk_applications_announcements_announcement_id FOREIGN KEY (announcement_id) REFERENCES announcements (id) ON DELETE CASCADE,
    CONSTRAINT fk_applications_users_applicant_id FOREIGN KEY (applicant_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE messages (
    id bigint GENERATED ALWAYS AS IDENTITY,
    announcement_id bigint NOT NULL,
    sender_id bigint NOT NULL,
    body text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT (now()),
    deleted_at timestamptz,
    CONSTRAINT pk_messages PRIMARY KEY (id),
    CONSTRAINT ck_messages_body_length CHECK (length(body) <= 2000),
    CONSTRAINT ck_messages_body_not_empty CHECK (length(trim(body)) > 0),
    CONSTRAINT fk_messages_announcements_announcement_id FOREIGN KEY (announcement_id) REFERENCES announcements (id) ON DELETE CASCADE,
    CONSTRAINT fk_messages_users_sender_id FOREIGN KEY (sender_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX uq_age_categories_code ON age_categories (code);

CREATE UNIQUE INDEX uq_age_categories_name ON age_categories (name);

CREATE INDEX ix_announcement_languages_language_id ON announcement_languages (language_id);

CREATE INDEX ix_announcements_age_category_id ON announcements (age_category_id);

CREATE INDEX ix_announcements_author_id ON announcements (author_id);

CREATE INDEX ix_announcements_experience_level_id ON announcements (experience_level_id);

CREATE INDEX ix_announcements_game_id_game_mode_id ON announcements (game_id, game_mode_id);

CREATE INDEX ix_announcements_status_game_created ON announcements (status, game_id, created_at);

CREATE UNIQUE INDEX uq_announcements_author_active ON announcements (author_id) WHERE status IN ('open', 'full');

CREATE INDEX ix_applications_announcement_status ON applications (announcement_id, status);

CREATE INDEX ix_applications_applicant_status ON applications (applicant_id, status);

CREATE UNIQUE INDEX uq_applications_announcement_applicant_active ON applications (announcement_id, applicant_id) WHERE status IN ('pending', 'accepted');

CREATE UNIQUE INDEX uq_applications_applicant_accepted ON applications (applicant_id) WHERE status = 'accepted';

CREATE UNIQUE INDEX uq_countries_code ON countries (code);

CREATE UNIQUE INDEX uq_countries_name ON countries (name);

CREATE UNIQUE INDEX uq_experience_levels_code ON experience_levels (code);

CREATE UNIQUE INDEX uq_experience_levels_name ON experience_levels (name);

CREATE UNIQUE INDEX uq_game_modes_game_id_name ON game_modes (game_id, name);

CREATE UNIQUE INDEX uq_games_code ON games (code);

CREATE UNIQUE INDEX uq_games_name ON games (name);

CREATE UNIQUE INDEX uq_languages_code ON languages (code);

CREATE UNIQUE INDEX uq_languages_name ON languages (name);

CREATE INDEX ix_messages_announcement_created ON messages (announcement_id, created_at);

CREATE INDEX ix_messages_sender_id ON messages (sender_id);

CREATE INDEX ix_users_country_id ON users (country_id);

CREATE UNIQUE INDEX uq_users_email ON users (email);

CREATE UNIQUE INDEX uq_users_nickname ON users (nickname);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261125120511_InitialCreate', '10.0.14');

COMMIT;

START TRANSACTION;

                CREATE FUNCTION set_updated_at() RETURNS trigger AS $$
                BEGIN
                    NEW.updated_at = now();
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_announcements_updated_at
                    BEFORE UPDATE ON announcements
                    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

                CREATE TRIGGER trg_applications_updated_at
                    BEFORE UPDATE ON applications
                    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
            

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261125120545_AddUpdatedAtTriggers', '10.0.14');

COMMIT;

