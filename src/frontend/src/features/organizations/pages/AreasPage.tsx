import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import UnfoldLessIcon from '@mui/icons-material/UnfoldLess';
import UnfoldMoreIcon from '@mui/icons-material/UnfoldMore';
import { useAccessCheck } from '../../../shared/lib/access';
import { Tag, TruncatedText } from '../../../shared/ui';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import {
  TreeRegistry,
  type TreeRegistryAction,
  type TreeRegistryHandle,
} from '../../../shared/ui/TreeRegistry';
import type { AreaNode } from '../api/organizationsApi';
import {
  AreaNodeDialogs,
  type NodeDialog,
  type NodeDialogResult,
} from '../components/AreaNodeDialogs';
import { AreaTypeMark, commandIcon } from '../components/AreaTypeMark';
import { ListFilter } from '../components/ListFilter';
import { useAreaTree } from '../hooks/queries';
import { useObjectLink, type ObjectLink } from '../hooks/useObjectLink';
import { commandKinds, commandLabel } from '../lib/areaCommands';
import { areasPage } from '../lib/areaTexts';
import { expandableIds, filterTree, hasChildren, initialExpansion } from '../lib/areaTree';
import { notFoundLevel } from '../lib/notFoundLevel';
import { organizationAreaPath } from '../paths';
import { organizationsPermissions } from '../permissions';

/** A node as the tree registry shows it: its name is the label. */
type AreaRow = AreaNode & { label: string };

/**
 * «Области» (mod-018 design § 3; US-1, US-2, US-6): the organization's typed tree on the tree
 * registry, columns «Название» (a link to the object's page; a deactivated project's node is tagged and plain
 * «деактивирован»), «Тип» and «Описание». The organization is not a row (D-8): departments and
 * project nodes stand at the top. The initial expansion is D-3 with «Развернуть всё» / «Свернуть
 * всё». «Новый департамент» is the holder of `area.create`'s; a row menu offers the commands of its
 * type (`areaCommands.ts`), none on a node of a deactivated project. No department at all is a
 * normal state (US-5). The tree is read again after every write, never patched.
 */
export function AreasPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useAreaTree(organizationId);
  const objectLink = useObjectLink(organizationId);
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.areaWrite, 'organization');
  const canCreate = access(organizationsPermissions.areaCreate, 'organization');
  const canLinkTeams =
    access(organizationsPermissions.areaLink, 'organization') &&
    access(organizationsPermissions.teamLink, 'organization');
  const treeRef = useRef<TreeRegistryHandle>(null);
  const [dialog, setDialog] = useState<NodeDialog>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  // `null`: the visit's start, the initial expansion of D-3.
  const [expanded, setExpanded] = useState<ReadonlySet<string> | null>(null);
  const [focusRequest, setFocusRequest] = useState<{ id: string } | null>(null);
  const [filter, setFilter] = useState('');

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
  const shown = filterTree(areas, filter);
  const filtering = filter.trim() !== '';
  const expandable = expandableIds(shown);
  // A search shows every match under its ancestors (design § 5.2).
  const expandedIds = filtering ? expandable : (expanded ?? initialExpansion(areas));
  const allExpanded = [...expandable].every((id) => expandedIds.has(id));
  const rows: AreaRow[] = shown.map((area) => ({ ...area, label: area.name }));
  const nothingFound = query.isSuccess && areas.length > 0 && shown.length === 0;

  function expand(id: string) {
    setExpanded((current) => new Set([...(current ?? expandedIds), id]));
  }

  function settle(id: string | null, select = true) {
    setDialog(null);
    if (id !== null) {
      if (select) {
        setSelectedId(id);
      }
      setFocusRequest({ id });
    }
  }

  function done(result: NodeDialogResult) {
    switch (result.kind) {
      case 'created':
        if (result.area.parentId) {
          expand(result.area.parentId);
        }
        settle(result.area.id);
        break;
      case 'moved':
        if (result.parentId) {
          expand(result.parentId);
        }
        settle(result.node.id);
        break;
      case 'deleted':
        settle(result.node.parentId);
        break;
      default:
        settle(result.node.id, false);
    }
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
    }
  }

  // A department and a project open their own page and become the chosen object (BR-NV-19; OQ-1 A:
  // a project opens its «Общие»); a sub-area opens its page under its project's menu. A deactivated
  // project's node and sub-areas open nothing, they are plain text (OQ-3 A).
  function rowLink(area: AreaNode): ObjectLink | null {
    if (area.projectActive === false) return null;
    if (area.type === 'department') return objectLink('department', area.id);
    if (area.type === 'project' && area.projectId) return objectLink('project', area.projectId);
    // A sub-area keeps the team that fits its project (D-6).
    if (area.projectId) return objectLink('project', area.projectId, area.id);
    return { to: organizationAreaPath(organizationId, area.id) };
  }

  function actions(area: AreaNode): TreeRegistryAction[] {
    return commandKinds(area, { canWrite, canCreate }).map((kind) => ({
      label: commandLabel(kind, area.type),
      icon: commandIcon(kind, true),
      destructive: kind === 'delete',
      onClick: () => {
        if (kind === 'createChild') {
          if (area.projectId !== null) {
            setDialog({ kind: 'createSub', projectId: area.projectId, parentId: area.id });
          }
        } else if (kind === 'delete') {
          setDialog({ kind, node: area, hasChildren: hasChildren(areas, area.id) });
        } else {
          setDialog({ kind, node: area });
        }
      },
    }));
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
  const createDepartment = () => setDialog({ kind: 'createDepartment' });

  return (
    <>
      <PageHeader
        title={areasPage.title}
        commands={commands}
        primaryAction={
          canCreate
            ? { label: areasPage.create, icon: <AddIcon />, onClick: createDepartment }
            : undefined
        }
      />
      {query.isSuccess && areas.length > 0 && (
        <ListFilter label={areasPage.search} text={filter} onText={setFilter} />
      )}
      {nothingFound ? (
        <StateFrame
          icon={FilterListOffIcon}
          heading={areasPage.nothingFound}
          action={{ label: areasPage.resetFilter, onClick: () => setFilter('') }}
        />
      ) : empty ? (
        <StateFrame
          icon={InboxOutlinedIcon}
          heading={areasPage.empty.heading}
          hint={canCreate ? areasPage.empty.adminHint : areasPage.empty.otherHint}
          action={
            canCreate
              ? { label: areasPage.create, icon: <AddIcon />, onClick: createDepartment }
              : undefined
          }
        />
      ) : (
        <TreeRegistry<AreaRow>
          ref={treeRef}
          aria-label={areasPage.title}
          firstColumn={areasPage.columns.name}
          columns={[
            {
              header: areasPage.columns.type,
              render: (area) => <AreaTypeMark type={area.type} />,
            },
            {
              header: areasPage.columns.description,
              render: (area) => <TruncatedText>{area.description ?? ''}</TruncatedText>,
            },
          ]}
          nodes={rows}
          adornment={(area) =>
            area.projectActive === false ? <Tag label={areasPage.deactivated} /> : null
          }
          labelTo={(area) => rowLink(area)?.to}
          labelState={(area) => rowLink(area)?.state}
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
      <AreaNodeDialogs
        organizationId={organizationId}
        dialog={dialog}
        canCreate={canCreate}
        canLinkTeams={canLinkTeams}
        onDone={done}
        onClose={closeDialog}
      />
    </>
  );
}
