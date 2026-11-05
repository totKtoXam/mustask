import { useState, type MouseEvent, type ReactNode } from 'react';
import AddIcon from '@mui/icons-material/Add';
import {
  CreateWorkItemDialog,
  NEW_WORK_ITEM_LABEL,
  TypePickerMenu,
  useCreateWorkItemIn,
  workItemsPermissions,
  type CreatableType,
  type CreatePlace,
  type WorkItem,
} from '../../workitems';
import { useAccessCheck } from '../../../shared/lib/access';
import type { Command } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import type { BoardLevel, BoardProject } from '../api/boardsApi';
import type { BoardScope } from '../lib/scope';

/**
 * What a surface's read gives the creation: its level, whose types it offers, and — on a team's
 * views, which hold the items of several projects — the projects it may create in (feat-040
 * BR-TS-9).
 */
export interface CreationSurface {
  level: BoardLevel;
  projects?: readonly BoardProject[];
}

export interface NewWorkItem {
  /**
   * The user may write items (`workitems.workitem.write`, mod-016 design § 6.2): without it there
   * is no command, nothing opens, and a surface's empty state draws no action.
   */
  canWrite: boolean;
  /**
   * The user may create here: `canWrite`, and on a team's views the team works on a linked active
   * project to create in (BR-TS-9). The command and the first column's «Новый элемент» follow it.
   */
  canCreate: boolean;
  /** The header's primary, once the surface is read; none while it is not. */
  command: Command | undefined;
  /** Opens the picker anchored on the control clicked, or the dialog for a level of one type. */
  open(event?: MouseEvent<HTMLElement>): void;
  /** The level has several types: the control opens a menu (`aria-haspopup`). */
  opensMenu: boolean;
  /** The picker is open. */
  menuOpen: boolean;
  /** The picker and the dialog, rendered by the page. */
  elements: ReactNode;
}

/**
 * Creating from a team's or the project's backlog or board (mod-006 US-3, US-6;
 * mod-017 US-2; feat-040 US-5): mod-003's picker and dialog with the level's types only — a
 * level of one type opens the dialog preset to it, a level of several the
 * picker first — and mod-003's create with the team the dialog shows (mod-017
 * api-contract § 2.2), a fresh `requestId` per submission as the dialog makes
 * it. A project's surface creates in the project, starting with its default team; a team's
 * creates in the project chosen in the dialog, among the team's linked ones, and starts with the
 * team (`teamName` is its name from the read). The view's project filter preselects a single
 * project.
 */
export function useNewWorkItem({
  organizationId,
  scope,
  surface,
  teamName,
  filterIds,
  onCreated,
  onContextGone,
}: {
  organizationId: string;
  scope: BoardScope;
  surface: CreationSurface | undefined;
  /** A team's views: the team's name from the read, which the dialog holds until a project is chosen. */
  teamName: string | undefined;
  /** The view's filter: on a team's views the chosen project ids. */
  filterIds: readonly string[];
  onCreated(item: WorkItem): void;
  /** The organization answered not found: the page is read again. */
  onContextGone(): void;
}): NewWorkItem {
  const create = useCreateWorkItemIn(organizationId);
  const access = useAccessCheck();
  const canWrite = access(workItemsPermissions.workItemWrite, 'organization');
  // A team's views offer a creation only when the team works on a project to create in.
  const canCreate = canWrite && (scope.kind === 'project' || (surface?.projects?.length ?? 0) > 0);
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [creatingTypeId, setCreatingTypeId] = useState<string | null>(null);

  const types: CreatableType[] = (surface?.level.types ?? []).map((type) => ({
    id: type.id,
    name: type.name,
    appearance: type.appearance,
    initialStateName: type.initialState.name,
    // Every type of a board level is on a backlog (the level has a route).
    onBacklog: true,
  }));
  const opensMenu = types.length > 1;

  const open = (event?: MouseEvent<HTMLElement>) => {
    const [only] = types;
    if (!canCreate) {
      return;
    }
    if (types.length === 1 && only) {
      setCreatingTypeId(only.id);
    } else if (opensMenu && event) {
      setAnchor(event.currentTarget);
    }
  };

  const command: Command | undefined =
    canCreate && types.length > 0
      ? {
          label: NEW_WORK_ITEM_LABEL,
          icon: <AddIcon />,
          menuOpen: opensMenu ? anchor !== null : undefined,
          onClick: open,
        }
      : undefined;

  const place: CreatePlace =
    scope.kind === 'project'
      ? { kind: 'project', projectId: scope.projectId }
      : {
          kind: 'team',
          team: { id: scope.teamId, name: teamName ?? '' },
          filterProjectIds: filterIds,
        };

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
      {creatingTypeId !== null && surface && (
        <RequiresOrganizationCode code={workItemsPermissions.workItemWrite}>
          <CreateWorkItemDialog
            organizationId={organizationId}
            place={place}
            types={types}
            initialTypeId={creatingTypeId}
            submit={(projectId, body) => create.mutateAsync({ projectId, body })}
            onCreated={(item) => {
              setCreatingTypeId(null);
              onCreated(item);
            }}
            onClose={(contextGone) => {
              setCreatingTypeId(null);
              if (contextGone) {
                onContextGone();
              }
            }}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );

  return { canWrite, canCreate, command, open, opensMenu, menuOpen: anchor !== null, elements };
}
