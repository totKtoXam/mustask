using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <summary>
    /// bug-004 D-3, "one name, one meaning": «Разрешена» is Resolved on every type that has it. The seed
    /// (<c>SeededProcess</c>) changed for organizations created from now on; this moves the processes that
    /// already exist. Data only, the model is unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only what is still as seeded.</b> A row moves when it is the third state of an Эпик, Фича or
    /// Пользовательская история (type keys EPIC, FEAT, US), named «Разрешена», in the category InProgress — exactly
    /// the row the old seed wrote. A state that was renamed, repositioned, recategorized or belongs to another type
    /// is not touched: a person has changed it, and a migration does not guess what they meant. «Баг» is already
    /// Resolved and is not touched either.
    /// </para>
    /// <para>
    /// <b>Reported, not guessed.</b> After the move, every process in which a state name still stands in more than
    /// one category (the rule the domain states, <c>StateNameRule</c>) is named in a <c>RAISE WARNING</c> (the
    /// process's organization, the name, the categories) and left as it is. Nothing else is named: a state renamed
    /// or repositioned by hand that breaks no rule is reported nowhere.
    /// </para>
    /// <para>
    /// <b>Where the report lands, and the step that follows.</b> In the PostgreSQL server log (container mode:
    /// <c>docker logs</c> of the database container), not in the terminal: <c>dotnet ef database update</c> prints
    /// "Applying migration … Done." and none of a migration's <c>NOTICE</c> or <c>WARNING</c> lines, with or without
    /// <c>--verbose</c>. So whoever applies this migration runs the check afterwards, against the same database; no
    /// row means nothing needs deciding. It is the <c>SELECT</c> of the <c>FOR</c> loop below, run alone; the runbook
    /// (<c>docs/runbooks/local-migrations.md</c>, "Migrations that report") quotes the same text, and
    /// <c>MoveResolvedStatesToResolvedCategoryRunbookTests</c> keeps the two equal:
    /// <code>
    /// SELECT process.organization_id AS organization_id,
    ///        state.name AS state_name,
    ///        string_agg(DISTINCT state.category, ', ' ORDER BY state.category) AS categories
    /// FROM state_definitions AS state
    /// JOIN work_item_types AS kind ON kind.id = state.work_item_type_id
    /// JOIN processes AS process ON process.id = kind.process_id
    /// GROUP BY process.id, process.organization_id, state.name
    /// HAVING count(DISTINCT state.category) > 1
    /// ORDER BY process.organization_id, state.name;
    /// </code>
    /// </para>
    /// <para>
    /// <b>Idempotent and reversible.</b> The predicate selects nothing once the rows have moved, so a second run
    /// changes nothing. <see cref="Down"/> is the mirror on the same predicate (Resolved back to InProgress), which
    /// restores the old seed's shape for rows that are still as the new seed wrote them.
    /// </para>
    /// <para>
    /// <b>Once per database, so it may have to be run again.</b> <c>database update</c> applies this migration one
    /// time. A checkout built before it that shares the database (external database mode) writes the old seed's shape
    /// for every organization it creates afterwards, and <c>database update</c> is then a no-op. The <c>UPDATE</c>
    /// is idempotent, so run it by hand once more, then the check; the runbook quotes the statement.
    /// </para>
    /// </remarks>
    public partial class MoveResolvedStatesToResolvedCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $bug004$
                DECLARE
                    moved integer;
                    breach record;
                BEGIN
                    UPDATE state_definitions AS state
                    SET category = 'Resolved'
                    FROM work_item_types AS kind
                    WHERE kind.id = state.work_item_type_id
                      AND kind.key IN ('EPIC', 'FEAT', 'US')
                      AND state.name = 'Разрешена'
                      AND state.position = 3
                      AND state.category = 'InProgress';

                    GET DIAGNOSTICS moved = ROW_COUNT;
                    RAISE NOTICE 'bug-004: % state(s) moved from InProgress to Resolved', moved;

                    FOR breach IN
                        SELECT process.organization_id AS organization_id,
                               state.name AS state_name,
                               string_agg(DISTINCT state.category, ', ' ORDER BY state.category) AS categories
                        FROM state_definitions AS state
                        JOIN work_item_types AS kind ON kind.id = state.work_item_type_id
                        JOIN processes AS process ON process.id = kind.process_id
                        GROUP BY process.id, process.organization_id, state.name
                        HAVING count(DISTINCT state.category) > 1
                        ORDER BY process.organization_id, state.name
                    LOOP
                        RAISE WARNING 'bug-004: in the process of organization %, the state name "%" stands in more than one category (%); not changed, a person decides',
                            breach.organization_id, breach.state_name, breach.categories;
                    END LOOP;
                END
                $bug004$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE state_definitions AS state
                SET category = 'InProgress'
                FROM work_item_types AS kind
                WHERE kind.id = state.work_item_type_id
                  AND kind.key IN ('EPIC', 'FEAT', 'US')
                  AND state.name = 'Разрешена'
                  AND state.position = 3
                  AND state.category = 'Resolved';
                """);
        }
    }
}
