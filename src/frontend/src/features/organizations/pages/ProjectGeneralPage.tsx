import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined';
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import NotesOutlinedIcon from '@mui/icons-material/NotesOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { useWorkContext } from '../../../shared/lib/workContext';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import { processDefinitionPath } from '../../process';
import { ConfirmAction } from '../components/ConfirmAction';
import { DescriptionDialog } from '../components/DescriptionDialog';
import { AreaNodeDialogs } from '../components/AreaNodeDialogs';
import { FactsPanel } from '../components/FactsPanel';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import { useDeactivateProject, useDescribeProject, useRenameProject } from '../hooks/mutations';
import { useProject, useProjectAreaTree } from '../hooks/queries';
import { commandKinds } from '../lib/areaCommands';
import { placementField } from '../lib/areaTexts';
import { placeOfProjectNode } from '../lib/areaTree';
import { notFoundLevel } from '../lib/notFoundLevel';
import { descriptionDialog, projectTexts } from '../lib/teamTexts';
import { deactivatedProjectsPath } from '../paths';
import { organizationsPermissions } from '../permissions';

type Dialog = 'rename' | 'describe' | 'deactivate' | 'move' | null;

/**
 * Project «Общие» (US-3; design § 8; feat-028 design § 8): the project's name, its «Описание»
 * (kept as typed, with its line breaks; «Описание не задано» when none), its key — set at
 * creation and never changed, so no field or action edits it (BR-M1-1) — and the read-only «Процесс»
 * link to mod-001's screen (D-c). Renamed through «Переименование проекта», described through
 * «Описание проекта» (both `project.write`'s), deactivated after a confirmation. A deactivated project
 * keeps its description and its links to teams.
 */
export function ProjectGeneralPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useProject(organizationId, projectId);
  const rename = useRenameProject(organizationId, projectId);
  const describe = useDescribeProject(organizationId, projectId);
  const deactivate = useDeactivateProject(organizationId, projectId);
  const tree = useProjectAreaTree(organizationId, projectId);
  const node = tree.data?.find((entry) => entry.type === 'project');
  const notify = useNotify();
  const navigate = useNavigate();
  const context = useWorkContext();
  const access = useAccessCheck();
  const [dialog, setDialog] = useState<Dialog>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
    }
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const project = query.data;

  // mod-016 design § 6.2: «Переименовать» needs `organizations.project.write`,
  // «Деактивировать» `organizations.project.deactivate`; each hidden without it.
  const commands: Command[] = [];
  if (project && !query.isError) {
    if (access(organizationsPermissions.projectWrite, 'organization')) {
      commands.push({
        label: 'Переименовать',
        icon: <EditOutlinedIcon />,
        onClick: () => setDialog('rename'),
      });
      commands.push({
        label: projectTexts.describe,
        icon: <NotesOutlinedIcon />,
        onClick: () => setDialog('describe'),
      });
    }
    if (
      node &&
      commandKinds(node, {
        canWrite: access(organizationsPermissions.areaWrite, 'organization'),
        canCreate: access(organizationsPermissions.areaCreate, 'organization'),
      }).includes('move')
    ) {
      commands.push({
        label: placementField.move,
        icon: <DriveFileMoveOutlinedIcon />,
        onClick: () => setDialog('move'),
      });
    }
    if (access(organizationsPermissions.projectDeactivate, 'organization')) {
      commands.push({
        label: 'Деактивировать',
        icon: <ArchiveOutlinedIcon />,
        onClick: () => setDialog('deactivate'),
      });
    }
  }

  return (
    <>
      <PageHeader title="Общие" commands={commands} />
      {loading ? (
        <FormSkeleton fields={4} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <FactsPanel
          facts={[
            { label: 'Название', value: query.data.name },
            {
              label: projectTexts.factDescription,
              value: query.data.description ? (
                <Box component="span" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                  {query.data.description}
                </Box>
              ) : (
                <Box component="span" sx={{ color: 'text.secondary' }}>
                  {projectTexts.noDescription}
                </Box>
              ),
            },
            ...(node
              ? [{ label: placementField.label, value: placeOfProjectNode(node).label }]
              : []),
            { label: 'Ключ', value: query.data.key, note: 'Задан при создании и не меняется' },
            {
              label: 'Типы рабочих элементов',
              value: (
                <>
                  {'Задаются процессом организации: '}
                  <Link component={RouterLink} to={processDefinitionPath(organizationId)}>
                    Процесс
                  </Link>
                </>
              ),
            },
          ]}
        />
      )}
      {dialog === 'move' && node && (
        <AreaNodeDialogs
          organizationId={organizationId}
          dialog={{ kind: 'move', node }}
          canCreate={access(organizationsPermissions.areaCreate, 'organization')}
          canLinkTeams={false}
          onDone={() => setDialog(null)}
          onClose={closeDialog}
        />
      )}
      {dialog === 'rename' && project && (
        <RequiresOrganizationCode code={organizationsPermissions.projectWrite}>
          <NameDialog
            title="Переименование проекта"
            text={projectTexts.rename.text}
            helper="От 1 до 100 символов"
            initialName={project.name}
            submitLabel="Сохранить"
            submit={(name) => rename.mutateAsync(name)}
            onSucceeded={() => {
              setDialog(null);
              notify('Проект переименован.');
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
      {dialog === 'describe' && project && (
        <RequiresOrganizationCode code={organizationsPermissions.projectWrite}>
          <DescriptionDialog
            title={descriptionDialog.project.title}
            initial={project.description}
            submit={(description) => describe.mutateAsync(description)}
            onSucceeded={() => {
              setDialog(null);
              notify(descriptionDialog.project.saved);
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
      {dialog === 'deactivate' && project && (
        <RequiresOrganizationCode code={organizationsPermissions.projectDeactivate}>
          <ConfirmAction
            title="Деактивировать проект?"
            text={projectTexts.deactivate.text(project.name, project.key)}
            confirmLabel="Деактивировать"
            action={async () => {
              // DC-7, judged before the switcher sees the change: deactivating
              // the switcher's project while another is active makes the
              // switcher fall back, and its notice is the only one.
              const organization = context.organizations.find(
                (entry) => entry.id === context.selection.organizationId,
              );
              const fallsBack =
                context.selection.projectId === project.id &&
                (organization?.projects.some((entry) => entry.id !== project.id) ?? false);
              await deactivate.mutateAsync();
              return fallsBack;
            }}
            onSucceeded={(fallsBack) => {
              setDialog(null);
              navigate(deactivatedProjectsPath(organizationId));
              if (!fallsBack) {
                notify(`Проект «${project.name}» деактивирован.`);
              }
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
