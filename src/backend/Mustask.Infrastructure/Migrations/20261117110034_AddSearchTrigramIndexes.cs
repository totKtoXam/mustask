using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <summary>
    /// feat-071 (ADR-0044 § 1; feat-065 BR-GS-3, -17): the quick search's trigram indexes. The extension <c>pg_trgm</c>, the
    /// <c>IMMUTABLE</c> fold function <c>search_fold(text)</c> (lower case under the explicit ICU collation <c>ru-x-icu</c>, «ё» to «е»: the same fold the read model applies
    /// to the typed text, independent of the database's ctype as in the other read models) and six GIN <c>gin_trgm_ops</c> indexes: on <c>search_fold(name)</c> of areas (departments and sub-areas are area
    /// rows, ADR-0031), teams and projects, on <c>search_fold(key)</c> of projects, and on the user projection's already folded
    /// <c>search_name</c> and <c>search_email</c>. Additive: no column, constraint or write path of another module changes. The
    /// indexes are expression indexes, which the EF model cannot declare, so they are raw SQL here and the model snapshot does not
    /// carry them; <c>SearchDatabaseTests</c> pins their existence and that the read model's predicate text uses the same expression.
    /// <para>
    /// Precondition (feat-071 review F2): <c>pg_trgm</c> classifies word characters by the database's <c>datctype</c>, so on a
    /// <c>C</c>/<c>POSIX</c> database Cyrillic is not word text and the typo tier would be dead. <c>Up</c> refuses such a database
    /// with a clear message instead of building indexes that cannot work.
    /// </para>
    /// </summary>
    /// <inheritdoc />
    public partial class AddSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF (SELECT datctype FROM pg_database WHERE datname = current_database()) IN ('C', 'POSIX') THEN
                        RAISE EXCEPTION 'The quick search needs a database whose ctype knows Cyrillic (for example en_US.UTF-8 or ru_RU.UTF-8); this database has a C/POSIX ctype, where pg_trgm treats Cyrillic letters as separators. Recreate the database with a UTF-8 locale (docs/deploy/vm.md, section 3).';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                """
                CREATE FUNCTION search_fold(text) RETURNS text
                    IMMUTABLE PARALLEL SAFE LANGUAGE sql
                    AS $$ SELECT replace(lower($1 COLLATE "ru-x-icu"), 'ё', 'е') $$;
                """);

            migrationBuilder.Sql("CREATE INDEX ix_areas_search_name_trgm ON areas USING gin (search_fold(name) gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_teams_search_name_trgm ON teams USING gin (search_fold(name) gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_projects_search_name_trgm ON projects USING gin (search_fold(name) gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_projects_search_key_trgm ON projects USING gin (search_fold(\"key\") gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_directory_users_search_name_trgm ON directory_users USING gin (search_name gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_directory_users_search_email_trgm ON directory_users USING gin (search_email gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_directory_users_search_email_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_directory_users_search_name_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_projects_search_key_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_projects_search_name_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_teams_search_name_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_areas_search_name_trgm;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS search_fold(text);");
            // pg_trgm stays: a DBA may have created it before this migration (ADR-0044 Q-4) and other objects may use it.
        }
    }
}
