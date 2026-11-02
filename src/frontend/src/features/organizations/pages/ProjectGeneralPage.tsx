import { useState } from 'react';
import { Link as RouterLink, useLocation, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import NotesOutlinedIcon from '@mui/icons-material/NotesOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { ActionButton, InlineEmpty, Section } from '../../../shared/ui';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import { processDefinitionPath } from '../../process';
import { DefaultTeamDialog } from '../components/DefaultTeamDialog';
import { DescriptionDialog } from '../components/DescriptionDialog';
import { AreaNodeDialogs } from '../components/AreaNodeDialogs';
import { FactsPanel } from '../components/FactsPanel';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import { useDescribeProject, useRenameProject, useSetDefaultTeam } from '../hooks/mutations';
import { useProject, useProjectAreaTree, useTeams } from '../hooks/queries';
import { useObjectLink } from '../hooks/useObjectLink';
import { commandKinds } from '../lib/areaCommands';
import { placementField } from '../lib/areaTexts';
import { placeOfProjectNode } from '../lib/areaTree';
import { notFoundLevel } from '../lib/notFoundLevel';
import { descriptionDialog, projectTexts } from '../lib/teamTexts';
import { organizationsPermissions } from '../permissions';

type Dialog = 'rename' | 'describe' | 'defaultTeam' | 'move' | null;

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
  const setDefaultTeam = useSetDefaultTeam(organizationId, projectId);
  const section = useLocation().pathname.split('/').filter(Boolean).pop() ?? 'general';
  const teams = useTeams(organizationId, projectId, { enabled: section === 'settings' });
  const objectLink = useObjectLink(organizationId);
  const tree = useProjectAreaTree(organizationId, projectId);
  const node = tree.data?.find((entry) => entry.type === 'project');
  const notify = useNotify();
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

  // «Переименовать» and «Изменить описание» need `organizations.project.write` and live in
  // «Параметры»; «Переместить» belongs to «Общие» (feat-043 design § 7.1). Deactivation stays in
  // the list «Проекты».
  const commands: Command[] = [];
  if (project && !query.isError) {
    if (section === 'settings' && access(organizationsPermissions.projectWrite, 'organization')) {
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
      section === 'general' &&
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
  }

  const descriptionValue = project?.description ? (
    <Box component="span" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
      {project.description}
    </Box>
  ) : (
    <Box component="span" sx={{ color: 'text.secondary' }}>
      {projectTexts.noDescription}
    </Box>
  );
  const department = node?.parentId ? placeOfProjectNode(node).label : null;
  const defaultTeam = teams.data?.find((team) => team.defaultMark === 'named');

  return (
    <>
      <PageHeader title={section === 'settings' ? 'Параметры' : 'Общие'} commands={commands} />
      {loading ? (
        <FormSkeleton fields={4} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : section === 'settings' ? (
        <>
          <FactsPanel
            facts={[
              { label: 'Название', value: query.data.name },
              { label: projectTexts.factDescription, value: descriptionValue },
              { label: 'Ключ', value: query.data.key, note: projectTexts.keyFixed },
            ]}
          />
          <Box sx={{ mt: 4 }}>
            <Section
              title={projectTexts.defaultTeamSection}
              actions={
                access(organizationsPermissions.projectWrite, 'organization') &&
                (teams.data?.length ?? 0) > 0 ? (
                  <ActionButton
                    label={projectTexts.chooseDefaultTeam}
                    onClick={() => setDialog('defaultTeam')}
                  />
                ) : undefined
              }
            >
              <InlineEmpty>{defaultTeam?.name ?? projectTexts.noDefaultTeam}</InlineEmpty>
            </Section>
          </Box>
        </>
      ) : (
        <>
          <Section title={projectTexts.mainSection}>
            <FactsPanel
              facts={[
                { label: 'Название', value: query.data.name },
                { label: projectTexts.factDescription, value: descriptionValue },
                { label: 'Ключ', value: query.data.key },
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
          </Section>
          <Box sx={{ mt: 4 }}>
            <Section title={projectTexts.placeSection}>
              <FactsPanel
                facts={[
                  {
                    label: placementField.label,
                    value:
                      node?.parentId && department ? (
                        <Link
                          component={RouterLink}
                          to={objectLink('department', node.parentId).to}
                          state={objectLink('department', node.parentId).state}
                        >
                          {department}
                        </Link>
                      ) : (
                        projectTexts.noDepartment
                      ),
                  },
                ]}
              />
            </Section>
          </Box>
        </>
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
      {dialog === 'defaultTeam' && (
        <RequiresOrganizationCode code={organizationsPermissions.projectWrite}>
          <DefaultTeamDialog
            teams={teams.data ?? []}
            submit={(teamId) => setDefaultTeam.mutateAsync(teamId)}
            onSucceeded={() => {
              setDialog(null);
              notify('Команда по умолчанию изменена.');
            }}
            onRefused={() => void teams.refetch()}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
