import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import UnfoldLessIcon from '@mui/icons-material/UnfoldLess';
import UnfoldMoreIcon from '@mui/icons-material/UnfoldMore';
import Box from '@mui/material/Box';
import { useAccessCheck } from '../../../shared/lib/access';
import { ObjectAccess } from '../../../shared/lib/objectAccessProvider';
import { BoxedTabPanel, BoxedTabs, Tag, TruncatedText } from '../../../shared/ui';
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
import { DiagramLegend } from '../components/DiagramLines';
import { ListFilter } from '../components/ListFilter';
import { StructureDiagram } from '../components/StructureDiagram';
import { useAreaTree, useWritableAreaIds } from '../hooks/queries';
import { useSelectorOffers } from '../hooks/navigationQueries';
import { useAreaRowLink } from '../hooks/useAreaRowLink';
import { useDiagramView } from '../hooks/useDiagramView';
import { useStructureSettings, type StructureView } from '../hooks/useStructureSettings';
import { commandKinds, commandLabel } from '../lib/areaCommands';
import { areasPage, structureDiagram } from '../lib/areaTexts';
import { expandableIds, filterTree, hasChildren, initialExpansion } from '../lib/areaTree';
import { notFoundLevel } from '../lib/notFoundLevel';
import { organizationsPermissions } from '../permissions';

/** A node as the tree registry shows it: its name is the label. */
type AreaRow = AreaNode & { label: string };

/**
 * «Структура» (mod-018 design § 3; US-1, US-2, US-6): the organization's typed tree on the tree
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
  const rowLink = useAreaRowLink(organizationId);
  const tabsId = useId();
  const settings = useStructureSettings(organizationId);
  const [diagramView, patchDiagram] = useDiagramView(organizationId);
  const showDiagram =
    settings.view === 'diagram' && query.isSuccess && (query.data?.length ?? 0) > 0;
  // The offers are read only for the diagram (feat-049 BR-SD-3): the tree needs none.
  const offers = useSelectorOffers(organizationId, { enabled: showDiagram });
  const access = useAccessCheck();
  // `area.write` is decided per node (mod-019 BR-SA-11, X-4): the tree read's sibling serves, once, the ids
  // where the caller holds it, and the row commands and the dialogs' lock follow those ids.
  const writable = useWritableAreaIds(organizationId);
  const writableIds = useMemo(() => new Set(writable.data ?? []), [writable.data]);
  const canWrite = writableIds.size > 0;
  const canCreate = access(organizationsPermissions.areaCreate, 'organization');
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

  const level =
    (query.isError ? notFoundLevel(query.error) : null) ??
    (showDiagram && offers.isError ? notFoundLevel(offers.error) : null);
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

  function actions(area: AreaNode): TreeRegistryAction[] {
    return commandKinds(area, { canWrite: writableIds.has(area.id), canCreate }).map((kind) => ({
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
    query.isSuccess && expandable.size > 0 && !showDiagram
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
  // The open dialog's lock: the write code is held where the node (or the parent of a new sub-area) is writable.
  const dialogNodeId =
    dialog === null || dialog.kind === 'createDepartment'
      ? null
      : dialog.kind === 'createSub'
        ? dialog.parentId
        : dialog.node.id;
  const dialogCodes =
    dialogNodeId !== null && writableIds.has(dialogNodeId)
      ? [organizationsPermissions.areaWrite]
      : [];

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
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 2,
            flexWrap: 'wrap',
          }}
        >
          <BoxedTabs
            idBase={tabsId}
            aria-label={structureDiagram.viewLabel}
            value={settings.view}
            onChange={(value) => settings.setView(value as StructureView)}
            tabs={[
              { value: 'tree', label: structureDiagram.views.tree },
              { value: 'diagram', label: structureDiagram.views.diagram },
            ]}
          />
          {showDiagram && <DiagramLegend />}
        </Box>
      )}
      {showDiagram ? (
        <BoxedTabPanel idBase={tabsId} value="diagram" selected>
          <StructureDiagram
            organizationId={organizationId}
            areas={areas}
            offers={offers.data ?? null}
            offersStatus={offers.isError ? 'failed' : offers.data ? 'ready' : 'loading'}
            onRetryOffers={() => offers.refetch()}
            view={diagramView}
            patch={patchDiagram}
            mode={settings.mode}
            onMode={settings.setMode}
            orientation={settings.layout}
            onOrientation={settings.setLayout}
          />
        </BoxedTabPanel>
      ) : (
        <>
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
        </>
      )}
      <ObjectAccess codes={dialogCodes}>
        <AreaNodeDialogs
          organizationId={organizationId}
          dialog={dialog}
          canCreate={canCreate}
          onDone={done}
          onClose={closeDialog}
        />
      </ObjectAccess>
    </>
  );
}
