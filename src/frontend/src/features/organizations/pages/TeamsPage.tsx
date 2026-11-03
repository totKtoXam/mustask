import { useState } from 'react';
import { useParams } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { useWorkContext } from '../../../shared/lib/workContext';
import {
  DataTable,
  MoreActionsMenu,
  Tag,
  TruncatedText,
  type DataColumn,
} from '../../../shared/ui';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import type { ProjectTeam } from '../api/organizationsApi';
import { LinkDialog, type LinkOption } from '../components/LinkDialog';
import { useNotify } from '../components/notice';
import { UnlinkDialog } from '../components/UnlinkDialog';
import { useLinkTeamsToProject, useUnlinkTeam } from '../hooks/mutations';
import { useProject, useTeams } from '../hooks/queries';
import { useObjectLink } from '../hooks/useObjectLink';
import { DEFAULT_MARK_LABEL } from '../lib/defaultMark';
import { notFoundLevel } from '../lib/notFoundLevel';
import { linkDialog, projectTeamsPage, unlinkDialog } from '../lib/teamTexts';
import { organizationsPermissions } from '../permissions';

/** What the page asks of its user in a dialog. */
type Dialog = { kind: 'link' } | { kind: 'unlink'; team: ProjectTeam } | null;

/**
 * The project's «Команды» (feat-043 design § 7.1; feat-028 US-2..US-4): the teams of the
 * organization linked to the project, by name, as the server orders them. The named default carries
 * «По умолчанию», the only linked team «По умолчанию: единственная команда». A name sets the team
 * (D-3). The row menu holds «Отключить от проекта» alone; «Добавить команды» links existing teams
 * and needs both link codes. **A project with no team is the normal first state**, not a fault. The
 * default team is named in the project's «Параметры».
 */
export function TeamsPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useTeams(organizationId, projectId);
  const project = useProject(organizationId, projectId);
  const link = useLinkTeamsToProject(organizationId, projectId);
  const unlink = useUnlinkTeam(organizationId);
  const objectLink = useObjectLink(organizationId);
  const notify = useNotify();
  const context = useWorkContext();
  const access = useAccessCheck();
  const canLink =
    access(organizationsPermissions.teamLink, 'organization') &&
    access(organizationsPermissions.projectLink, 'organization');
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

  const teams = query.data ?? [];

  function linkedNotice(chosen: LinkOption[]): string {
    const [only] = chosen;
    return chosen.length === 1 && only
      ? linkDialog.project.linkedOne(only.name)
      : linkDialog.project.linkedMany(chosen.length);
  }

  const openLink = () => setDialog({ kind: 'link' });

  const columns: DataColumn<ProjectTeam>[] = [
    {
      key: 'team',
      header: projectTeamsPage.column,
      render: (team) => {
        const target = objectLink('team', team.id);
        return (
          <>
            <TruncatedText to={target.to} linkState={target.state} look="link">
              {team.name}
            </TruncatedText>
            {team.defaultMark && <Tag label={DEFAULT_MARK_LABEL[team.defaultMark]} />}
          </>
        );
      },
    },
  ];
  if (canLink) {
    columns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (team) => (
        <MoreActionsMenu
          label={projectTeamsPage.rowMenu(team.name)}
          size="row"
          items={[
            {
              label: projectTeamsPage.unlink,
              icon: <LinkOffOutlinedIcon />,
              onSelect: () => setDialog({ kind: 'unlink', team }),
              destructive: true,
            },
          ]}
        />
      ),
    });
  }

  return (
    <>
      <PageHeader
        title={projectTeamsPage.title}
        primaryAction={
          canLink && query.isSuccess
            ? { label: projectTeamsPage.add, icon: <AddIcon />, onClick: openLink }
            : undefined
        }
      />
      <DataTable
        aria-label={projectTeamsPage.tableLabel}
        columns={columns}
        rows={teams}
        rowKey={(team) => team.id}
        loading={query.isPending || (query.isError && query.isFetching)}
        error={query.isError && !query.isFetching ? apiErrorText(query.error) : undefined}
        onRetry={() => void query.refetch()}
        empty={{
          icon: InboxOutlinedIcon,
          heading: projectTeamsPage.empty.heading,
          hint: canLink ? projectTeamsPage.empty.adminHint : projectTeamsPage.empty.otherHint,
          action: canLink
            ? { label: projectTeamsPage.add, icon: <AddIcon />, onClick: openLink }
            : undefined,
        }}
      />
      {dialog?.kind === 'link' && (
        <RequiresOrganizationCode code={organizationsPermissions.projectLink}>
          <LinkDialog
            end="project"
            organizationId={organizationId}
            ownName={project.data?.name ?? ''}
            linkedIds={teams.map((team) => team.id)}
            submit={(ids) => link.mutateAsync(ids)}
            onSucceeded={(chosen) => {
              setDialog(null);
              notify(linkedNotice(chosen));
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
      {dialog?.kind === 'unlink' && (
        <RequiresOrganizationCode code={organizationsPermissions.projectLink}>
          <UnlinkDialog
            organizationId={organizationId}
            projectId={projectId}
            projectName={project.data?.name ?? ''}
            teamId={dialog.team.id}
            teamName={dialog.team.name}
            submit={async (destinationTeamId) => {
              const { team } = dialog;
              const { selection } = context;
              const fallsBack =
                selection.organizationId === organizationId &&
                selection.projectId === projectId &&
                selection.teamId === team.id;
              await unlink.mutateAsync({ projectId, teamId: team.id, destinationTeamId });
              return fallsBack;
            }}
            onSucceeded={(fallsBack) => {
              setDialog(null);
              if (!fallsBack) {
                notify(unlinkDialog.unlinked);
              }
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
