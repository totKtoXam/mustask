import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Typography from '@mui/material/Typography';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import {
  TreeRegistry,
  type TreeRegistryAction,
  type TreeRegistryHandle,
} from '../../../shared/ui/TreeRegistry';
import type { AreaNode } from '../api/organizationsApi';
import {
  AreaOwnerDialog,
  CreateAreaDialog,
  DeleteAreaDialog,
  MoveAreaDialog,
} from '../components/AreaDialogs';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import {
  useCreateArea,
  useDeleteArea,
  useMoveArea,
  useRenameArea,
  useSetAreaOwner,
} from '../hooks/mutations';
import { useAreaTree, useTeams } from '../hooks/queries';
import { rootOf } from '../lib/areaTree';
import { notFoundLevel } from '../lib/notFoundLevel';
import { organizationsPermissions } from '../permissions';

type AreaDialog =
  | { kind: 'create'; parent: AreaNode }
  | { kind: 'rename' | 'move' | 'owner' | 'delete'; area: AreaNode }
  | null;

/** An area as the tree registry shows it: its name is the label. */
type AreaRow = AreaNode & { label: string };

/**
 * «Области» (US-5, US-6; design § 11): the project's area tree on the shared
 * tree registry. Each node shows its one owner — «унаследовано» after an
 * inherited one — and «По умолчанию» on a team's default area. The root
 * offers «Новая дочерняя область» only (BR-M1-11); any other node every
 * action, and the server refuses what it must (DC-8). The tree is read again
 * after every write, never patched.
 */
export function AreasPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useAreaTree(organizationId, projectId);
  const teams = useTeams(organizationId, projectId, { enabled: query.isSuccess });
  const createArea = useCreateArea(organizationId, projectId);
  const renameArea = useRenameArea(organizationId, projectId);
  const moveArea = useMoveArea(organizationId, projectId);
  const setAreaOwner = useSetAreaOwner(organizationId, projectId);
  const deleteArea = useDeleteArea(organizationId, projectId);
  const notify = useNotify();
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.areaWrite, 'organization');
  const treeRef = useRef<TreeRegistryHandle>(null);
  const [dialog, setDialog] = useState<AreaDialog>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  // `null`: the visit's start, the root alone expanded (design § 11.2).
  const [expanded, setExpanded] = useState<ReadonlySet<string> | null>(null);
  const [focusRequest, setFocusRequest] = useState<{ id: string } | null>(null);

  // Focus after a change (design § 11.7), once the dialog has handed focus
  // back: the new, renamed, re-owned or moved row, or a deleted row's parent.
  useEffect(() => {
    if (focusRequest && dialog === null) {
      treeRef.current?.focusRow(focusRequest.id);
    }
  }, [focusRequest, dialog]);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  const areas = query.data ?? [];
  const root = rootOf(areas);
  const expandedIds = expanded ?? new Set(root ? [root.id] : []);
  const selected = areas.find((area) => area.id === selectedId);
  const rows: AreaRow[] = areas.map((area) => ({ ...area, label: area.name }));

  function expand(...ids: string[]) {
    setExpanded((current) => new Set([...(current ?? expandedIds), ...ids]));
  }

  function settle(id: string, notice: string, select = true) {
    setDialog(null);
    if (select) {
      setSelectedId(id);
    }
    setFocusRequest({ id });
    notify(notice);
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
    }
  }

  function actions(area: AreaNode): TreeRegistryAction[] {
    // mod-016 design § 6.2: every area write needs `organizations.area.write`;
    // without it no node has an action and the table draws no actions column.
    if (!canWrite) {
      return [];
    }
    const createChild: TreeRegistryAction = {
      label: 'Новая дочерняя область',
      icon: <AddIcon fontSize="small" />,
      onClick: () => setDialog({ kind: 'create', parent: area }),
    };
    if (area.parentId === null) {
      return [createChild];
    }
    return [
      createChild,
      {
        label: 'Переименовать',
        icon: <EditOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'rename', area }),
      },
      {
        label: 'Переместить',
        icon: <DriveFileMoveOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'move', area }),
      },
      {
        label: 'Изменить владельца',
        icon: <GroupOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'owner', area }),
      },
      {
        label: 'Удалить',
        icon: <DeleteOutlineOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'delete', area }),
        destructive: true,
      },
    ];
  }

  const createUnderRoot = () => root && setDialog({ kind: 'create', parent: root });
  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <>
      <PageHeader
        title="Области"
        commands={
          query.isSuccess && root && canWrite
            ? [
                { label: 'Новая область', icon: <AddIcon />, onClick: createUnderRoot },
                {
                  label: 'Новая дочерняя область',
                  icon: <AddIcon />,
                  disabled: !selected,
                  onClick: () => selected && setDialog({ kind: 'create', parent: selected }),
                },
              ]
            : undefined
        }
      />
      <TreeRegistry<AreaRow>
        ref={treeRef}
        aria-label="Области"
        firstColumn="Область"
        columns={[
          {
            header: 'Владелец',
            render: (area) => (
              <>
                {area.resolvedOwner.name}
                {area.ownerInherited && (
                  <>
                    {' '}
                    <Typography component="span" variant="caption" color="text.secondary">
                      унаследовано
                    </Typography>
                  </>
                )}
              </>
            ),
          },
        ]}
        nodes={rows}
        adornment={(area) =>
          area.defaultOfTeam ? <Chip size="small" label="По умолчанию" /> : null
        }
        actions={actions}
        showActions={canWrite ? undefined : false}
        selectedId={selectedId}
        onSelect={setSelectedId}
        expanded={expandedIds}
        onToggle={(id) =>
          setExpanded((current) => {
            const next = new Set(current ?? expandedIds);
            if (next.has(id)) {
              next.delete(id);
            } else {
              next.add(id);
            }
            return next;
          })
        }
        loading={loading}
        error={query.isError ? query.error : undefined}
        onRetry={() => void query.refetch()}
      />
      {query.isSuccess && areas.length === 1 && (
        <Box>
          <StateFrame
            icon={InboxOutlinedIcon}
            heading="Вложенных областей пока нет"
            hint={
              canWrite
                ? 'Создайте первую область кнопкой «Новая область» выше.'
                : 'Области задаёт администратор организации.'
            }
            action={
              canWrite
                ? { label: 'Новая область', icon: <AddIcon />, onClick: createUnderRoot }
                : undefined
            }
          />
        </Box>
      )}
      <RequiresOrganizationCode code={organizationsPermissions.areaWrite}>
        {dialog?.kind === 'create' && (
          <CreateAreaDialog
            parent={dialog.parent}
            teams={teams}
            submit={(body) => createArea.mutateAsync(body)}
            onSucceeded={(area) => {
              expand(dialog.parent.id);
              settle(area.id, `Область «${area.name}» создана.`);
            }}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'rename' && (
          <NameDialog
            title="Переименование области"
            initialName={dialog.area.name}
            submitLabel="Сохранить"
            submit={(name) => renameArea.mutateAsync({ areaId: dialog.area.id, name })}
            onSucceeded={() => settle(dialog.area.id, 'Область переименована.', false)}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'move' && (
          <MoveAreaDialog
            area={dialog.area}
            tree={areas}
            submit={(parentId) => moveArea.mutateAsync({ areaId: dialog.area.id, parentId })}
            onSucceeded={({ parentId }) => {
              expand(parentId);
              settle(dialog.area.id, `Область «${dialog.area.name}» перемещена.`);
            }}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'owner' && (
          <AreaOwnerDialog
            area={dialog.area}
            teams={teams}
            submit={(ownerTeamId) =>
              setAreaOwner.mutateAsync({ areaId: dialog.area.id, ownerTeamId })
            }
            onSucceeded={() =>
              settle(dialog.area.id, `Владелец области «${dialog.area.name}» изменён.`, false)
            }
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'delete' && (
          <DeleteAreaDialog
            area={dialog.area}
            action={() => deleteArea.mutateAsync(dialog.area.id)}
            onSucceeded={() =>
              settle(dialog.area.parentId ?? '', `Область «${dialog.area.name}» удалена.`)
            }
            onClose={closeDialog}
          />
        )}
      </RequiresOrganizationCode>
    </>
  );
}
