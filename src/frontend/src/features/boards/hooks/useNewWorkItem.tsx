import { useState, type MouseEvent, type ReactNode } from 'react';
import AddIcon from '@mui/icons-material/Add';
import {
  CreateWorkItemDialog,
  NEW_WORK_ITEM_LABEL,
  TypePickerMenu,
  useCreateWorkItem,
  workItemsPermissions,
  type CreatableType,
  type WorkItem,
} from '../../workitems';
import { useAccessCheck } from '../../../shared/lib/access';
import type { Command } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import type { BoardLevel, BoardTeam } from '../api/boardsApi';

/** What a surface's read gives the creation: the team and its level. */
export interface CreationSurface {
  team: BoardTeam;
  level: BoardLevel;
}

export interface NewWorkItem {
  /**
   * The user may create items here (`workitems.workitem.write`, mod-016 design
   * § 6.2): without it there is no command, nothing opens, and a surface's
   * empty state draws no action.
   */
  canWrite: boolean;
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
 * Creating from a team's backlog or board (US-3, US-6; spec § Dialog;
 * BR-M4-21): mod-003's picker and dialog with the level's types only — a
 * level of one type opens the dialog preset to it, a level of several the
 * picker first — and mod-003's create with the team's id (api-contract § 7),
 * a fresh `requestId` per submission as the dialog makes it. The dialog's text
 * quotes the team's default area as the surface read it.
 */
export function useNewWorkItem({
  organizationId,
  projectId,
  surface,
  onCreated,
  onContextGone,
}: {
  organizationId: string;
  projectId: string;
  surface: CreationSurface | undefined;
  onCreated(item: WorkItem): void;
  /** The team, project or organization answered not found: the page is read again. */
  onContextGone(): void;
}): NewWorkItem {
  const create = useCreateWorkItem(organizationId, projectId);
  const access = useAccessCheck();
  const canWrite = access(workItemsPermissions.workItemWrite, 'organization');
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [creatingTypeId, setCreatingTypeId] = useState<string | null>(null);

  const types: CreatableType[] = (surface?.level.types ?? []).map((type) => ({
    id: type.id,
    name: type.name,
    appearance: type.appearance,
    initialStateName: type.initialState.name,
  }));
  const opensMenu = types.length > 1;

  const open = (event?: MouseEvent<HTMLElement>) => {
    const [only] = types;
    if (!canWrite) {
      return;
    }
    if (types.length === 1 && only) {
      setCreatingTypeId(only.id);
    } else if (opensMenu && event) {
      setAnchor(event.currentTarget);
    }
  };

  const command: Command | undefined =
    canWrite && types.length > 0
      ? {
          label: NEW_WORK_ITEM_LABEL,
          icon: <AddIcon />,
          menuOpen: opensMenu ? anchor !== null : undefined,
          onClick: open,
        }
      : undefined;

  const team = surface?.team;
  const elements = (
    <>
      <TypePickerMenu
        anchor={anchor}
        allowed={canWrite}
        types={types}
        onChoose={(type) => {
          setAnchor(null);
          setCreatingTypeId(type.id);
        }}
        onClose={() => setAnchor(null)}
      />
      {creatingTypeId !== null && team && (
        <RequiresOrganizationCode code={workItemsPermissions.workItemWrite}>
          <CreateWorkItemDialog
            organizationId={organizationId}
            projectId={projectId}
            types={types}
            initialTypeId={creatingTypeId}
            team={{ name: team.name, defaultAreaPath: team.defaultArea.path }}
            submit={(body) => create.mutateAsync({ ...body, teamId: team.id })}
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

  return { canWrite, command, open, opensMenu, menuOpen: anchor !== null, elements };
}
