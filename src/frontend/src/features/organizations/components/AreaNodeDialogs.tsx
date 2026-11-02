import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import type { Area, AreaNode } from '../api/organizationsApi';
import {
  useCreateArea,
  useDeleteArea,
  useDescribeArea,
  useMoveArea,
  useRenameArea,
} from '../hooks/mutations';
import {
  areaEditDialogs,
  createDepartmentDialog,
  createSubAreaDialog,
  deleteAreaDialog,
  moveAreaDialog,
} from '../lib/areaTexts';
import { organizationsPermissions } from '../permissions';
import {
  CreateDepartmentDialog,
  CreateSubAreaDialog,
  DeleteAreaDialog,
  MoveAreaDialog,
} from './AreaDialogs';
import { DescriptionDialog } from './DescriptionDialog';
import { NameDialog } from './NameDialog';
import { useNotify } from './notice';

/** The node a dialog works on: what the tree and a node's page both know. */
export type NodeRef = Pick<
  AreaNode,
  'id' | 'name' | 'type' | 'description' | 'parentId' | 'projectId'
>;

/** Which dialog a page has open. */
export type NodeDialog =
  | { kind: 'createDepartment' }
  | { kind: 'createSub'; projectId: string; parentId: string }
  | { kind: 'rename' | 'describe' | 'move'; node: NodeRef }
  | { kind: 'delete'; node: NodeRef; hasChildren: boolean }
  | null;

/** What a finished dialog reports to its page, which then selects, focuses or leaves. */
export type NodeDialogResult =
  | { kind: 'created'; area: Area }
  | { kind: 'renamed' | 'described'; node: NodeRef }
  | { kind: 'moved'; node: NodeRef; parentId: string | null }
  | { kind: 'deleted'; node: NodeRef };

/**
 * The dialogs of an area node, shared by the organization's tree, a node's page and the project's
 * «Области» (mod-018 design § 5): each mounts per opening under the code of its write, and on
 * success shows its notice and reports to the page. `onClose` carries whether the page's context
 * answered not found, so the page reads itself again.
 */
export function AreaNodeDialogs({
  organizationId,
  dialog,
  canCreate,
  canLinkTeams,
  onDone,
  onClose,
}: {
  organizationId: string;
  dialog: NodeDialog;
  /** The user holds `area.create`. */
  canCreate: boolean;
  /** The user holds `area.link` and `team.link`. */
  canLinkTeams: boolean;
  onDone(result: NodeDialogResult): void;
  onClose(contextGone: boolean): void;
}) {
  const createArea = useCreateArea(organizationId);
  const renameArea = useRenameArea(organizationId);
  const describeArea = useDescribeArea(organizationId);
  const moveArea = useMoveArea(organizationId);
  const deleteArea = useDeleteArea(organizationId);
  const notify = useNotify();

  if (dialog === null) {
    return null;
  }

  return (
    <RequiresOrganizationCode
      code={
        dialog.kind === 'createDepartment'
          ? organizationsPermissions.areaCreate
          : organizationsPermissions.areaWrite
      }
    >
      {dialog.kind === 'createDepartment' && (
        <CreateDepartmentDialog
          organizationId={organizationId}
          canLinkTeams={canLinkTeams}
          submit={(body) => createArea.mutateAsync(body)}
          onSucceeded={(area) => {
            notify(createDepartmentDialog.created(area.name));
            onDone({ kind: 'created', area });
          }}
          onClose={onClose}
        />
      )}
      {dialog.kind === 'createSub' && (
        <CreateSubAreaDialog
          organizationId={organizationId}
          projectId={dialog.projectId}
          initialParentId={dialog.parentId}
          submit={(body) => createArea.mutateAsync(body)}
          onSucceeded={(area) => {
            notify(createSubAreaDialog.created(area.name));
            onDone({ kind: 'created', area });
          }}
          onClose={onClose}
        />
      )}
      {dialog.kind === 'rename' && (
        <NameDialog
          title={
            dialog.node.type === 'department'
              ? areaEditDialogs.rename.departmentTitle
              : areaEditDialogs.rename.areaTitle
          }
          helper={areaEditDialogs.rename.helper}
          initialName={dialog.node.name}
          submitLabel={areaEditDialogs.rename.submit}
          submit={(name) => renameArea.mutateAsync({ areaId: dialog.node.id, name })}
          onSucceeded={() => {
            notify(
              dialog.node.type === 'department'
                ? areaEditDialogs.rename.departmentRenamed
                : areaEditDialogs.rename.areaRenamed,
            );
            onDone({ kind: 'renamed', node: dialog.node });
          }}
          onClose={onClose}
        />
      )}
      {dialog.kind === 'describe' && (
        <DescriptionDialog
          title={areaEditDialogs.description.title}
          initial={dialog.node.description}
          submit={(description) =>
            describeArea.mutateAsync({ areaId: dialog.node.id, description })
          }
          onSucceeded={() => {
            notify(areaEditDialogs.description.saved);
            onDone({ kind: 'described', node: dialog.node });
          }}
          onClose={onClose}
        />
      )}
      {dialog.kind === 'move' && (
        <MoveAreaDialog
          organizationId={organizationId}
          node={dialog.node}
          canTopLevel={canCreate}
          submit={(parentId) => moveArea.mutateAsync({ areaId: dialog.node.id, parentId })}
          onSucceeded={({ parentId }) => {
            notify(
              dialog.node.type === 'project'
                ? moveAreaDialog.project.moved(dialog.node.name)
                : moveAreaDialog.area.moved(dialog.node.name),
            );
            onDone({ kind: 'moved', node: dialog.node, parentId });
          }}
          onClose={onClose}
        />
      )}
      {dialog.kind === 'delete' && dialog.node.type !== 'project' && (
        <DeleteAreaDialog
          organizationId={organizationId}
          area={{ id: dialog.node.id, name: dialog.node.name, type: dialog.node.type }}
          hasChildren={dialog.hasChildren}
          action={() => deleteArea.mutateAsync(dialog.node.id)}
          onSucceeded={() => {
            notify(
              dialog.node.type === 'department'
                ? deleteAreaDialog.departmentDeleted(dialog.node.name)
                : deleteAreaDialog.areaDeleted(dialog.node.name),
            );
            onDone({ kind: 'deleted', node: dialog.node });
          }}
          onClose={onClose}
        />
      )}
    </RequiresOrganizationCode>
  );
}
