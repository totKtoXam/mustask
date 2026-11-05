import Box from '@mui/material/Box';
import { InlineMultiSelectField } from '../../../shared/ui';
import type { BoardProject } from '../api/boardsApi';
import { projectFilterTexts } from '../lib/projectFilterTexts';

/**
 * «Проект» of a team's backlog and board (feat-040 BR-TS-8; design § 4.1, § 4.2): the kit's
 * `InlineMultiSelectField` over the team's linked active projects by name, from the page's own read
 * (no request of its own), with no counts. The page keeps the chosen values (`string[]`, in its
 * address) and sends them as `projectId` (api-contract § 3.1). Any chosen value matches.
 *
 * The field works while the first read is pending (its list says «Загрузка…») and when it failed
 * (its list says so, and opening it reads the page again). A value that names no project the field
 * knows — unknown, malformed, another organization's, unlinked or deactivated — stays in the
 * filter and selects nothing; the field names only the projects it knows and, with none known,
 * reads «Фильтр задан»: no project and no reason (BR-TS-8). «Очистить фильтры» removes it.
 */
export function ProjectFilter({
  projects,
  failed,
  value,
  onOpen,
  onChange,
}: {
  /** The team's linked active projects as the page read them; `undefined` until the first read ends. */
  projects: readonly BoardProject[] | undefined;
  /** The first read failed: the list says so. */
  failed: boolean;
  /** The chosen project ids; empty is «Все проекты». */
  value: readonly string[];
  /** The list opens: a failed read is read again. */
  onOpen(): void;
  onChange(values: string[]): void;
}) {
  return (
    <Box sx={(theme) => ({ width: theme.spacing(30), maxWidth: '100%' })}>
      <InlineMultiSelectField<string>
        label={projectFilterTexts.label}
        frame="always"
        emptyText={value.length > 0 ? projectFilterTexts.filterOn : projectFilterTexts.allProjects}
        value={value}
        options={(projects ?? []).map((project) => ({ value: project.id, label: project.name }))}
        loading={projects === undefined && !failed}
        loadingText={projectFilterTexts.loading}
        noOptionsText={failed ? projectFilterTexts.failed : projectFilterTexts.none}
        onOpen={onOpen}
        onChange={onChange}
      />
    </Box>
  );
}
