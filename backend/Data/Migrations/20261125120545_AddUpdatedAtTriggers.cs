using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER trg_applications_updated_at ON applications;
                DROP TRIGGER trg_announcements_updated_at ON announcements;
                DROP FUNCTION set_updated_at();
            ");
        }
    }
}
