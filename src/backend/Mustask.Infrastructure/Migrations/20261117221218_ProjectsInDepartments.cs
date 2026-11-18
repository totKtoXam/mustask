using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <summary>
    /// mod-022 (doc-016 BR-TD-3, BR-TD-6, BR-TD-19 steps 2 and 3, BR-TD-20; ADR-0040): every project stands in a department, and no
    /// project–team link joins two departments. In this one migration's transaction (all or nothing): (1) the log table is created;
    /// (2) <see cref="ProjectsInDepartmentsUpgrade.Script"/> places the top-level projects and cuts the links across departments, naming
    /// each in the log and in notices; (3) the shape check of <c>areas</c> then requires every project node to have a parent. Nothing
    /// is deleted before it is logged. The migration is irreversible in data: Down only relaxes the check, it does not return a project
    /// to the top level and does not restore a cut link (Down drops the log table too: read it, or the migration notices, before going down; a stand reset is the clean way back).
    /// </summary>
    /// <inheritdoc />
    public partial class ProjectsInDepartments : Migration
    {
        private const string ShapeBefore = "(type = 1 AND project_id IS NULL AND parent_id IS NULL) OR (type = 2 AND project_id IS NOT NULL) OR (type = 3 AND project_id IS NOT NULL AND parent_id IS NOT NULL)";
        private const string ShapeAfter = "(type = 1 AND project_id IS NULL AND parent_id IS NULL) OR (type = 2 AND project_id IS NOT NULL AND parent_id IS NOT NULL) OR (type = 3 AND project_id IS NOT NULL AND parent_id IS NOT NULL)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ProjectsInDepartmentsUpgrade.CreateLog);
            migrationBuilder.Sql(ProjectsInDepartmentsUpgrade.Script);
            migrationBuilder.Sql("DROP FUNCTION pg_temp.mustask_choose_color(text[]);");

            migrationBuilder.DropCheckConstraint(
                name: "ck_areas_type_shape",
                table: "areas");

            migrationBuilder.AddCheckConstraint(
                name: "ck_areas_type_shape",
                table: "areas",
                sql: ShapeAfter);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_areas_type_shape",
                table: "areas");

            migrationBuilder.AddCheckConstraint(
                name: "ck_areas_type_shape",
                table: "areas",
                sql: ShapeBefore);

            migrationBuilder.Sql("DROP TABLE IF EXISTS project_place_upgrade_log;");
        }
    }
}
