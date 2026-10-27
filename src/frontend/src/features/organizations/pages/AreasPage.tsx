import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import NotesOutlinedIcon from '@mui/icons-material/NotesOutlined';
import UnfoldLessIcon from '@mui/icons-material/UnfoldLess';
import UnfoldMoreIcon from '@mui/icons-material/UnfoldMore';
import { useAccessCheck } from '../../../shared/lib/access';
import { TruncatedText } from '../../../shared/ui';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import {
  TreeRegistry,
  type TreeRegistryAction,
  type TreeRegistryHandle,
} from '../../../shared/ui/TreeRegistry';
import type { AreaNode } from '../api/organizationsApi';
import { CreateAreaDialog, DeleteAreaDialog, MoveAreaDialog } from '../components/AreaDialogs';
import { DescriptionDialog } from '../components/DescriptionDialog';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import {
  useCreateArea,
  useDeleteArea,
  useDescribeArea,
  useMoveArea,
  useRenameArea,
} from '../hooks/mutations';
import { useAreaTree } from '../hooks/queries';
import { expandableIds, hasChildren, initialExpansion } from '../lib/areaTree';
import {
  areaCommands,
  areaEditDialogs,
  areasPage,
  createAreaDialog,
  deleteAreaDialog,
  moveAreaDialog,
} from '../lib/areaTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import { organizationAreaPath } from '../paths';
import { organizationsPermissions } from '../permissions';

type AreaDialog =
  | { kind: 'create'; parentId: string | null }
  | { kind: 'rename' | 'describe' | 'move' | 'delete'; area: AreaNode }
  | null;

/** An area as the tree registry shows it: its name is the label. */
type AreaRow = AreaNode & { label: string };

/**
 * «Области» (mod-018 design § 3; US-1, US-2, US-6): the organization's area tree on the tree registry,
 * columns «Область» (the name, a link to the area's page) and «Описание». The initial expansion is D-3
 * (everything while there are at most 100 areas) with «Развернуть всё» / «Свернуть всё» in the header.
 * «Новая область» is the holder of `area.create`'s; a row menu offers the commands of `area.write`, the
 * server refusing what it must. **No area at all is the first state after the upgrade (US-5)**, a
 * normal one: the empty frame says so. The tree is read again after every write, never patched.
 */
export function AreasPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useAreaTree(organizationId);
  const createArea = useCreateArea(organizationId);
  const renameArea = useRenameArea(organizationId);
  const describeArea = useDescribeArea(organizationId);
  const moveArea = useMoveArea(organizationId);
  const deleteArea = useDeleteArea(organizationId);
  const notify = useNotify();
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.areaWrite, 'organization');
  const canCreate = access(organizationsPermissions.areaCreate, 'organization');
  const canLink = access(organizationsPermissions.areaLink, 'organization');
  const canLinkProjects = canLink && access(organizationsPermissions.projectLink, 'organization');
  const canLinkTeams = canLink && access(organizationsPermissions.teamLink, 'organization');
  const treeRef = useRef<TreeRegistryHandle>(null);
  const [dialog, setDialog] = useState<AreaDialog>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  // `null`: the visit's start, the initial expansion of D-3.
  const [expanded, setExpanded] = useState<ReadonlySet<string> | null>(null);
  const [focusRequest, setFocusRequest] = useState<{ id: string } | null>(null);

  // Focus after a change (design § 12), once the dialog has handed focus back: the new, renamed
  // or moved row, or a deleted row's parent.
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
  const expandedIds = expanded ?? initialExpansion(areas);
  const expandable = expandableIds(areas);
  const allExpanded = [...expandable].every((id) => expandedIds.has(id));
  const rows: AreaRow[] = areas.map((area) => ({ ...area, label: area.name }));

  function expand(...ids: string[]) {
    setExpanded((current) => new Set([...(current ?? expandedIds), ...ids]));
  }

  function settle(id: string | null, notice: string, select = true) {
    setDialog(null);
    if (id !== null) {
      if (select) {
        setSelectedId(id);
      }
      setFocusRequest({ id });
    }
    notify(notice);
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
    }
  }

  function actions(area: AreaNode): TreeRegistryAction[] {
    // Every area write needs `organizations.area.write`: without it no node has an action and the
    // table draws no actions column.
    if (!canWrite) {
      return [];
    }
    return [
      {
        label: areaCommands.createChild,
        icon: <AddIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'create', parentId: area.id }),
      },
      {
        label: areaCommands.rename,
        icon: <EditOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'rename', area }),
      },
      {
        label: areaCommands.describe,
        icon: <NotesOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'describe', area }),
      },
      {
        label: areaCommands.move,
        icon: <DriveFileMoveOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'move', area }),
      },
      {
        label: areaCommands.delete,
        icon: <DeleteOutlineOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'delete', area }),
        destructive: true,
      },
    ];
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const empty = query.isSuccess && areas.length === 0;
  const commands: Command[] =
    query.isSuccess && expandable.size > 0
      ? [
          allExpanded
            ? {
                label: areasPage.collapseAll,
                icon: <UnfoldLessIcon />,
                onClick: () => setExpanded(new Set()),
              }
            : {
                label: areasPage.expandAll,
                icon: <UnfoldMoreIcon />,
                onClick: () => setExpanded(expandable),
              },
        ]
      : [];
  const createTopLevel = () => setDialog({ kind: 'create', parentId: null });

  return (
    <>
      <PageHeader
        title={areasPage.title}
        commands={commands}
        primaryAction={
          canCreate
            ? { label: areasPage.create, icon: <AddIcon />, onClick: createTopLevel }
            : undefined
        }
      />
      {empty ? (
        <StateFrame
          icon={InboxOutlinedIcon}
          heading={areasPage.empty.heading}
          hint={canCreate ? areasPage.empty.adminHint : areasPage.empty.otherHint}
          action={
            canCreate
              ? { label: areasPage.create, icon: <AddIcon />, onClick: createTopLevel }
              : undefined
          }
        />
      ) : (
        <TreeRegistry<AreaRow>
          ref={treeRef}
          aria-label={areasPage.title}
          firstColumn={areasPage.columns.area}
          columns={[
            {
              header: areasPage.columns.description,
              render: (area) => <TruncatedText>{area.description ?? ''}</TruncatedText>,
            },
          ]}
          nodes={rows}
          labelTo={(area) => organizationAreaPath(organizationId, area.id)}
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
      )}
      <RequiresOrganizationCode code={organizationsPermissions.areaWrite}>
        {dialog?.kind === 'create' && (
          <CreateAreaDialog
            organizationId={organizationId}
            initialParentId={dialog.parentId}
            canTopLevel={canCreate}
            canLinkProjects={canLinkProjects}
            canLinkTeams={canLinkTeams}
            submit={(body) => createArea.mutateAsync(body)}
            onSucceeded={(area) => {
              if (area.parentId) {
                expand(area.parentId);
              }
              settle(area.id, createAreaDialog.created(area.name));
            }}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'rename' && (
          <NameDialog
            title={areaEditDialogs.rename.title}
            helper={areaEditDialogs.rename.helper}
            initialName={dialog.area.name}
            submitLabel={areaEditDialogs.rename.submit}
            submit={(name) => renameArea.mutateAsync({ areaId: dialog.area.id, name })}
            onSucceeded={() => settle(dialog.area.id, areaEditDialogs.rename.renamed, false)}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'describe' && (
          <DescriptionDialog
            title={areaEditDialogs.description.title}
            initial={dialog.area.description}
            submit={(description) =>
              describeArea.mutateAsync({ areaId: dialog.area.id, description })
            }
            onSucceeded={() => settle(dialog.area.id, areaEditDialogs.description.saved, false)}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'move' && (
          <MoveAreaDialog
            organizationId={organizationId}
            area={dialog.area}
            canTopLevel={canCreate}
            submit={(parentId) => moveArea.mutateAsync({ areaId: dialog.area.id, parentId })}
            onSucceeded={({ parentId }) => {
              if (parentId) {
                expand(parentId);
              }
              settle(dialog.area.id, moveAreaDialog.moved(dialog.area.name));
            }}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'delete' && (
          <DeleteAreaDialog
            organizationId={organizationId}
            area={dialog.area}
            hasChildren={hasChildren(areas, dialog.area.id)}
            action={() => deleteArea.mutateAsync(dialog.area.id)}
            onSucceeded={() =>
              settle(dialog.area.parentId, deleteAreaDialog.deleted(dialog.area.name))
            }
            onClose={closeDialog}
          />
        )}
      </RequiresOrganizationCode>
    </>
  );
}
