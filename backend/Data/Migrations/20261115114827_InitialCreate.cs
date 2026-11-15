using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "age_categories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    min_age = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_age_categories", x => x.id);
                    table.CheckConstraint("ck_age_categories_min_age_non_negative", "min_age >= 0");
                    table.CheckConstraint("ck_age_categories_name_not_empty", "length(trim(name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "experience_levels",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experience_levels", x => x.id);
                    table.CheckConstraint("ck_experience_levels_name_not_empty", "length(trim(name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.id);
                    table.CheckConstraint("ck_games_name_not_empty", "length(trim(name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_languages", x => x.id);
                    table.CheckConstraint("ck_languages_name_not_empty", "length(trim(name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nickname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_birth_date_not_future", "birth_date <= current_date");
                    table.CheckConstraint("ck_users_email_lower", "email = lower(email)");
                    table.CheckConstraint("ck_users_nickname_not_empty", "length(trim(nickname)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "game_modes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    game_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_modes", x => x.id);
                    table.UniqueConstraint("uq_game_modes_game_id_id", x => new { x.game_id, x.id });
                    table.CheckConstraint("ck_game_modes_name_not_empty", "length(trim(name)) > 0");
                    table.ForeignKey(
                        name: "fk_game_modes_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_languages",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    language_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_languages", x => new { x.user_id, x.language_id });
                    table.ForeignKey(
                        name: "fk_user_languages_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_languages_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    author_id = table.Column<long>(type: "bigint", nullable: false),
                    game_id = table.Column<long>(type: "bigint", nullable: false),
                    game_mode_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    experience_level_id = table.Column<long>(type: "bigint", nullable: false),
                    age_category_id = table.Column<long>(type: "bigint", nullable: false),
                    max_participants = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false, defaultValue: "open"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcements", x => x.id);
                    table.CheckConstraint("ck_announcements_description_length", "length(description) <= 2000");
                    table.CheckConstraint("ck_announcements_description_not_empty", "length(trim(description)) > 0");
                    table.CheckConstraint("ck_announcements_max_participants", "max_participants BETWEEN 2 AND 10");
                    table.CheckConstraint("ck_announcements_status", "status IN ('open', 'full', 'closed')");
                    table.CheckConstraint("ck_announcements_title_not_empty", "length(trim(title)) > 0");
                    table.CheckConstraint("ck_announcements_updated_at", "updated_at >= created_at");
                    table.ForeignKey(
                        name: "fk_announcements_age_categories_age_category_id",
                        column: x => x.age_category_id,
                        principalTable: "age_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcements_experience_levels_experience_level_id",
                        column: x => x.experience_level_id,
                        principalTable: "experience_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcements_game_modes_game_id_game_mode_id",
                        columns: x => new { x.game_id, x.game_mode_id },
                        principalTable: "game_modes",
                        principalColumns: new[] { "game_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcements_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcements_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "announcement_languages",
                columns: table => new
                {
                    announcement_id = table.Column<long>(type: "bigint", nullable: false),
                    language_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcement_languages", x => new { x.announcement_id, x.language_id });
                    table.ForeignKey(
                        name: "fk_announcement_languages_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_announcement_languages_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    announcement_id = table.Column<long>(type: "bigint", nullable: false),
                    applicant_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false, defaultValue: "pending"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.CheckConstraint("ck_applications_status", "status IN ('pending', 'accepted', 'rejected', 'withdrawn')");
                    table.CheckConstraint("ck_applications_updated_at", "updated_at >= created_at");
                    table.ForeignKey(
                        name: "fk_applications_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_applications_users_applicant_id",
                        column: x => x.applicant_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    announcement_id = table.Column<long>(type: "bigint", nullable: false),
                    sender_id = table.Column<long>(type: "bigint", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.CheckConstraint("ck_messages_body_length", "length(body) <= 2000");
                    table.CheckConstraint("ck_messages_body_not_empty", "length(trim(body)) > 0");
                    table.ForeignKey(
                        name: "fk_messages_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_age_categories_code",
                table: "age_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_age_categories_name",
                table: "age_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_announcement_languages_language_id",
                table: "announcement_languages",
                column: "language_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_age_category_id",
                table: "announcements",
                column: "age_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_author_id",
                table: "announcements",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_experience_level_id",
                table: "announcements",
                column: "experience_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_game_id_game_mode_id",
                table: "announcements",
                columns: new[] { "game_id", "game_mode_id" });

            migrationBuilder.CreateIndex(
                name: "ix_announcements_status_game_created",
                table: "announcements",
                columns: new[] { "status", "game_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_announcements_author_active",
                table: "announcements",
                column: "author_id",
                unique: true,
                filter: "status IN ('open', 'full')");

            migrationBuilder.CreateIndex(
                name: "ix_applications_announcement_status",
                table: "applications",
                columns: new[] { "announcement_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_applicant_status",
                table: "applications",
                columns: new[] { "applicant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_applications_announcement_applicant_active",
                table: "applications",
                columns: new[] { "announcement_id", "applicant_id" },
                unique: true,
                filter: "status IN ('pending', 'accepted')");

            migrationBuilder.CreateIndex(
                name: "uq_applications_applicant_accepted",
                table: "applications",
                column: "applicant_id",
                unique: true,
                filter: "status = 'accepted'");

            migrationBuilder.CreateIndex(
                name: "uq_experience_levels_code",
                table: "experience_levels",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_experience_levels_name",
                table: "experience_levels",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_game_modes_game_id_name",
                table: "game_modes",
                columns: new[] { "game_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_games_code",
                table: "games",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_games_name",
                table: "games",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_languages_code",
                table: "languages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_languages_name",
                table: "languages",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_announcement_created",
                table: "messages",
                columns: new[] { "announcement_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender_id",
                table: "messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_languages_language_id",
                table: "user_languages",
                column: "language_id");

            migrationBuilder.CreateIndex(
                name: "uq_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_users_nickname",
                table: "users",
                column: "nickname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_languages");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "user_languages");

            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "languages");

            migrationBuilder.DropTable(
                name: "age_categories");

            migrationBuilder.DropTable(
                name: "experience_levels");

            migrationBuilder.DropTable(
                name: "game_modes");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
