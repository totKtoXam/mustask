import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { useWorkContext } from '../../../shared/lib/workContext';
import {
  ActionButton,
  DataTable,
  InlineEmpty,
  MoreActionsMenu,
  Section,
  ThinAddIcon,
  TruncatedText,
  type DataColumn,
} from '../../../shared/ui';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import type { ProjectTeam } from '../api/organizationsApi';
import { useLinkTeamsToProject, useUnlinkTeam } from '../hooks/mutations';
import { useProject, useTeams } from '../hooks/queries';
import { areaPage } from '../lib/areaTexts';
import { linkDialog, unlinkDialog } from '../lib/teamTexts';
import { organizationTeamPath } from '../paths';
import { organizationsPermissions } from '../permissions';
import { LinkDialog, type LinkOption } from './LinkDialog';
import { useNotify } from './notice';
import { UnlinkDialog } from './UnlinkDialog';

/**
 * «Команды» of a project node's page (mod-018 design § 4): the teams of the project, which are the
 * feat-028 project–team links and not a second kind, said so on the section. The link and the
 * unlink are the project page's own dialogs and need the same codes. Loads and fails on its own.
 */
export function ProjectNodeTeams({
  organizationId,
  projectId,
  active = true,
}: {
  organizationId: string;
  projectId: string;
  /** False for a deactivated project: its node is read-only (design § 4), no link or unlink. */
  active?: boolean;
}) {
  const query = useTeams(organizationId, projectId);
  const project = useProject(organizationId, projectId);
  const link = useLinkTeamsToProject(organizationId, projectId);
  const unlink = useUnlinkTeam(organizationId);
  const notify = useNotify();
  const context = useWorkContext();
  const access = useAccessCheck();
  const canLink =
    active &&
    access(organizationsPermissions.teamLink, 'organization') &&
    access(organizationsPermissions.projectLink, 'organization');
  const [linking, setLinking] = useState(false);
  const [unlinking, setUnlinking] = useState<ProjectTeam | null>(null);
  const rows = query.data ?? [];
  const projectName = project.data?.name ?? '';

  const columns: DataColumn<ProjectTeam>[] = [
    {
      key: 'team',
      header: 'Команда',
      render: (team) => (
        <TruncatedText to={organizationTeamPath(organizationId, team.id)} look="link">
          {team.name}
        </TruncatedText>
      ),
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
          label={areaPage.rowMenu(team.name)}
          size="row"
          items={[
            {
              label: areaPage.projectTeams.unlink,
              icon: <LinkOffOutlinedIcon />,
              onSelect: () => setUnlinking(team),
              destructive: true,
            },
          ]}
        />
      ),
    });
  }

  function close(contextGone: boolean) {
    setLinking(false);
    setUnlinking(null);
    if (contextGone) {
      void query.refetch();
    }
  }

  return (
    <>
      <Box sx={{ mt: 4 }}>
        <Section
          title={areaPage.projectTeams.title}
          count={query.isSuccess ? rows.length : undefined}
          actions={
            canLink ? (
              <ActionButton
                label={areaPage.projectTeams.add}
                icon={<ThinAddIcon />}
                onClick={() => setLinking(true)}
              />
            ) : undefined
          }
        >
          <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1 }}>
            {areaPage.projectTeams.sameLinks}
          </Typography>
          {query.isSuccess && rows.length === 0 ? (
            <InlineEmpty>{areaPage.teams.empty}</InlineEmpty>
          ) : (
            <DataTable
              aria-label={areaPage.projectTeams.tableLabel}
              columns={columns}
              rows={rows}
              rowKey={(team) => team.id}
              loading={query.isPending || (query.isError && query.isFetching)}
              error={query.isError && !query.isFetching ? apiErrorText(query.error) : undefined}
              onRetry={() => void query.refetch()}
            />
          )}
        </Section>
      </Box>
      {linking && (
        <RequiresOrganizationCode code={organizationsPermissions.projectLink}>
          <LinkDialog
            end="project"
            organizationId={organizationId}
            ownName={projectName}
            linkedIds={rows.map((team) => team.id)}
            submit={(ids) => link.mutateAsync(ids)}
            onSucceeded={(chosen: LinkOption[]) => {
              setLinking(false);
              const [only] = chosen;
              notify(
                chosen.length === 1 && only
                  ? linkDialog.project.linkedOne(only.name)
                  : linkDialog.project.linkedMany(chosen.length),
              );
            }}
            onClose={close}
          />
        </RequiresOrganizationCode>
      )}
      {unlinking && (
        <RequiresOrganizationCode code={organizationsPermissions.projectLink}>
          <UnlinkDialog
            organizationId={organizationId}
            projectId={projectId}
            projectName={projectName}
            teamId={unlinking.id}
            teamName={unlinking.name}
            submit={async (destinationTeamId) => {
              const { selection } = context;
              const fallsBack =
                selection.organizationId === organizationId &&
                selection.projectId === projectId &&
                selection.teamId === unlinking.id;
              await unlink.mutateAsync({
                projectId,
                teamId: unlinking.id,
                destinationTeamId,
              });
              return fallsBack;
            }}
            onSucceeded={(fallsBack) => {
              setUnlinking(null);
              if (!fallsBack) {
                notify(unlinkDialog.unlinked);
              }
            }}
            onClose={close}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
