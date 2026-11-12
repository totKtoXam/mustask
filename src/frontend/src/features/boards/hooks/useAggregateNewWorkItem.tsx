import { useMemo, useState, type MouseEvent, type ReactNode } from 'react';
import AddIcon from '@mui/icons-material/Add';
import { useWorkContext } from '../../../shared/lib/workContext';
import type { Command } from '../../../shared/ui/PageHeader';
import { RequiresCode } from '../../../shared/ui/RequiresOrganizationCode';
import { useProjectCodes } from '../../organizations';
import { useProcessDefinition } from '../../process';
import {
  CreateWorkItemDialog,
  NEW_WORK_ITEM_LABEL,
  TypePickerMenu,
  creatableTypes,
  useCreateWorkItemIn,
  workItemsPermissions,
  type WorkItem,
} from '../../workitems';
import type { AggregateSelectors, GroupHeader } from '../api/aggregateApi';
import { viewLevelOf } from '../lib/grouping';

/** What the aggregate board's header gets from the creation (feat-060, D-1). */
export interface AggregateNewWorkItem {
  /** The header's command; none when the user may not create here or nothing can be offered yet. */
  command: Command | undefined;
  /** The type picker and the dialog, rendered by the page. */
  elements: ReactNode;
}

/**
 * Creating from an aggregate board at the organization and department level (feat-060 D-1, reversing
 * feat-048 BR-AV-8 for the board only): the team views' picker and dialog (feat-040 BR-TS-9) with the
 * board level's types, and «Проект» chosen in the dialog among the active projects the view's scope
 * holds — a department's own projects, or all of the organization's — in which the user holds
 * `workitems.workitem.write` (BR-SA-18). The project list is the shell's `selector-offers` read,
 * which the work context already holds, so nothing is requested here. The address's project filter
 * preselects a single project.
 */
export function useAggregateNewWorkItem({
  organizationId,
  selectors,
  groups,
  enabled,
  onCreated,
}: {
  organizationId: string;
  selectors: AggregateSelectors;
  /** The board's group headers: the level's types are the ones the columns have drop targets for. */
  groups: readonly GroupHeader[] | undefined;
  /** Only the board at the organization and department level creates. */
  enabled: boolean;
  onCreated(item: WorkItem): void;
}): AggregateNewWorkItem {
  const create = useCreateWorkItemIn(organizationId);
  const context = useWorkContext();
  const { codes: projectCodes } = useProjectCodes(organizationId, { enabled });
  const process = useProcessDefinition(organizationId);
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [creatingTypeId, setCreatingTypeId] = useState<string | null>(null);

  const level = viewLevelOf(selectors);
  const inScope = enabled && (level === 'organization' || level === 'department');
  const offers = context.offers;
  const projects = useMemo(
    () =>
      !inScope || !offers || !projectCodes
        ? []
        : offers.projects
            .filter(
              (project) =>
                (!selectors.departmentId || project.departmentId === selectors.departmentId) &&
                (projectCodes.get(project.id) ?? []).includes(workItemsPermissions.workItemWrite),
            )
            .map(({ id, name, key }) => ({ id, name, key })),
    [inScope, offers, projectCodes, selectors.departmentId],
  );

  const types = useMemo(() => {
    const levelTypeIds = new Set(
      (groups?.[0]?.columns ?? []).flatMap((column) => column.targets.map((t) => t.typeId)),
    );
    return creatableTypes(
      (process.data?.workItemTypes ?? []).filter((type) => levelTypeIds.has(type.id)),
      process.data?.backlogLevels,
    );
  }, [groups, process.data]);

  const canCreate = inScope && projects.length > 0 && types.length > 0;
  const opensMenu = types.length > 1;

  const command: Command | undefined = canCreate
    ? {
        label: NEW_WORK_ITEM_LABEL,
        icon: <AddIcon />,
        menuOpen: opensMenu ? anchor !== null : undefined,
        onClick: (event?: MouseEvent<HTMLElement>) => {
          const [only] = types;
          if (types.length === 1 && only) {
            setCreatingTypeId(only.id);
          } else if (event) {
            setAnchor(event.currentTarget);
          }
        },
      }
    : undefined;

  const elements = (
    <>
      <TypePickerMenu
        anchor={anchor}
        allowed={canCreate}
        types={types}
        onChoose={(type) => {
          setAnchor(null);
          setCreatingTypeId(type.id);
        }}
        onClose={() => setAnchor(null)}
      />
      {creatingTypeId !== null && canCreate && (
        <RequiresCode holds={canCreate}>
          <CreateWorkItemDialog
            organizationId={organizationId}
            place={{
              kind: 'scope',
              projects,
              filterProjectIds: selectors.projectId ? [selectors.projectId] : [],
            }}
            types={types}
            initialTypeId={creatingTypeId}
            submit={(projectId, body) => create.mutateAsync({ projectId, body })}
            onCreated={(item) => {
              setCreatingTypeId(null);
              onCreated(item);
            }}
            onClose={() => setCreatingTypeId(null)}
          />
        </RequiresCode>
      )}
    </>
  );

  return { command, elements };
}
