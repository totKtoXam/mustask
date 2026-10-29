import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';
import { useAccessCheck } from '../../../shared/lib/access';
import { Tag, TruncatedText } from '../../../shared/ui';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import {
  TreeRegistry,
  type TreeRegistryAction,
  type TreeRegistryHandle,
} from '../../../shared/ui/TreeRegistry';
import { InlineEmpty } from '../../../shared/ui';
import type { AreaNode } from '../api/organizationsApi';
import {
  AreaNodeDialogs,
  type NodeDialog,
  type NodeDialogResult,
} from '../components/AreaNodeDialogs';
import { AreaTypeMark, commandIcon } from '../components/AreaTypeMark';
import { useProjectAreaTree } from '../hooks/queries';
import { commandKinds, commandLabel } from '../lib/areaCommands';
import { areaCommands, areasPage, projectAreasPage } from '../lib/areaTexts';
import { hasChildren } from '../lib/areaTree';
import { notFoundLevel } from '../lib/notFoundLevel';
import { organizationAreaPath } from '../paths';
import { organizationsPermissions } from '../permissions';

/** A node as the tree registry shows it. The project node is the root: its department is outside the subtree. */
type AreaRow = AreaNode & { label: string };

/**
 * The project's «Области» (mod-018 design § 7, US-3): the project's own subtree — its node and the
 * sub-areas below it — on the tree registry. «Новая вложенная область» is `area.write`'s. A project
 * with no sub-area is a normal state and says so; a deactivated project's tree is read-only. Read
 * again after every write.
 */
export function ProjectAreasPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useProjectAreaTree(organizationId, projectId);
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.areaWrite, 'organization');
  const canCreate = access(organizationsPermissions.areaCreate, 'organization');
  const treeRef = useRef<TreeRegistryHandle>(null);
  const [dialog, setDialog] = useState<NodeDialog>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const [focusRequest, setFocusRequest] = useState<{ id: string } | null>(null);

  useEffect(() => {
    if (focusRequest && dialog === null) {
      treeRef.current?.focusRow(focusRequest.id);
    }
  }, [focusRequest, dialog]);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  const nodes = query.data ?? [];
  const root = nodes.find((node) => node.type === 'project');
  const rows: AreaRow[] = nodes.map((node) => ({
    ...node,
    parentId: node.id === root?.id ? null : node.parentId,
    label: node.name,
  }));
  const expanded = new Set(nodes.filter((node) => hasChildren(nodes, node.id)).map((n) => n.id));
  for (const id of collapsed) {
    expanded.delete(id);
  }
  const readOnly = root?.projectActive === false;
  const canAdd = canWrite && !readOnly && root !== undefined;

  function done(result: NodeDialogResult) {
    setDialog(null);
    const id =
      result.kind === 'created'
        ? result.area.id
        : result.kind === 'deleted'
          ? result.node.parentId
          : result.node.id;
    if (id) {
      setSelectedId(result.kind === 'deleted' || result.kind === 'created' ? id : selectedId);
      setFocusRequest({ id });
    }
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
    }
  }

  function actions(row: AreaRow): TreeRegistryAction[] {
    const node = nodes.find((entry) => entry.id === row.id) ?? row;
    return commandKinds(node, { canWrite, canCreate }).map((kind) => ({
      label: commandLabel(kind, node.type),
      icon: commandIcon(kind, true),
      destructive: kind === 'delete',
      onClick: () => {
        if (kind === 'createChild') {
          setDialog({ kind: 'createSub', projectId, parentId: node.id });
        } else if (kind === 'delete') {
          setDialog({ kind, node, hasChildren: hasChildren(nodes, node.id) });
        } else {
          setDialog({ kind, node });
        }
      },
    }));
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const onlyProject = query.isSuccess && nodes.length <= 1;

  return (
    <>
      <PageHeader
        title={projectAreasPage.title}
        primaryAction={
          canAdd && root
            ? {
                label: areaCommands.createChild,
                icon: <AddIcon />,
                onClick: () => setDialog({ kind: 'createSub', projectId, parentId: root.id }),
              }
            : undefined
        }
      />
      <TreeRegistry<AreaRow>
        ref={treeRef}
        aria-label={projectAreasPage.title}
        firstColumn={areasPage.columns.name}
        columns={[
          { header: areasPage.columns.type, render: (row) => <AreaTypeMark type={row.type} /> },
          {
            header: areasPage.columns.description,
            render: (row) => <TruncatedText>{row.description ?? ''}</TruncatedText>,
          },
        ]}
        nodes={rows}
        adornment={(row) =>
          row.projectActive === false ? <Tag label={areasPage.deactivated} /> : null
        }
        labelTo={(row) => organizationAreaPath(organizationId, row.id)}
        actions={actions}
        showActions={canWrite && !readOnly ? undefined : false}
        selectedId={selectedId}
        onSelect={setSelectedId}
        expanded={expanded}
        onToggle={(id) =>
          setCollapsed((current) => {
            const next = new Set(current);
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
      {onlyProject && (
        <InlineEmpty>
          {canAdd ? projectAreasPage.emptyChildren : projectAreasPage.emptyChildrenReader}
        </InlineEmpty>
      )}
      <AreaNodeDialogs
        organizationId={organizationId}
        dialog={dialog}
        canCreate={canCreate}
        canLinkTeams={false}
        onDone={done}
        onClose={closeDialog}
      />
    </>
  );
}
