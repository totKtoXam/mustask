import { useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import NotesOutlinedIcon from '@mui/icons-material/NotesOutlined';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { useAccessCheck } from '../../../shared/lib/access';
import {
  DataTable,
  MoreActionsMenu,
  TruncatedText,
  type DataColumn,
  type MenuEntry,
} from '../../../shared/ui';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import type { TeamListItem } from '../api/organizationsApi';
import { CreateTeamDialog } from '../components/CreateTeamDialog';
import { useNotify } from '../components/notice';
import { TeamDialogs, type TeamDialog } from '../components/TeamDialogs';
import { useCreateTeam } from '../hooks/mutations';
import { useOrganizationTeams } from '../hooks/queries';
import { notFoundLevel } from '../lib/notFoundLevel';
import { createTeamDialog, orgTeamsPage, teamCommands } from '../lib/teamTexts';
import { organizationTeamPath } from '../paths';
import { organizationsPermissions } from '../permissions';

/**
 * «Команды» of the organization (feat-028 US-1, US-3, US-5, US-7; design § 3): its active teams by
 * name, as the server orders them, each with the active projects it works on and its description.
 * «Новая команда» is the holder of `team.create`'s; a row's menu offers «Переименовать» and
 * «Изменить описание» (`team.write`) and «Удалить команду» (`team.delete`), each by its code, and is
 * drawn for a holder of any of them. **No team at all is the first state right after the upgrade
 * (US-7), a normal one**: the empty frame says so and offers the creation to the holder of the code.
 */
export function OrganizationTeamsPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useOrganizationTeams(organizationId);
  const create = useCreateTeam(organizationId);
  const notify = useNotify();
  const access = useAccessCheck();
  const canCreate = access(organizationsPermissions.teamCreate, 'organization');
  const canWrite = access(organizationsPermissions.teamWrite, 'organization');
  const canDelete = access(organizationsPermissions.teamDelete, 'organization');
  const canLink =
    access(organizationsPermissions.teamLink, 'organization') &&
    access(organizationsPermissions.projectLink, 'organization');
  const [creating, setCreating] = useState(false);
  const [dialog, setDialog] = useState<TeamDialog>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    setCreating(false);
    if (contextGone) {
      void query.refetch();
    }
  }

  function rowItems(team: TeamListItem): MenuEntry[] {
    const items: MenuEntry[] = [];
    if (canWrite) {
      items.push(
        {
          label: teamCommands.rename,
          icon: <EditOutlinedIcon />,
          onSelect: () => setDialog({ kind: 'rename', team }),
        },
        {
          label: teamCommands.describe,
          icon: <NotesOutlinedIcon />,
          onSelect: () => setDialog({ kind: 'describe', team }),
        },
      );
    }
    if (canDelete) {
      items.push({
        label: teamCommands.delete,
        icon: <DeleteOutlineOutlinedIcon />,
        onSelect: () => setDialog({ kind: 'delete', team }),
        destructive: true,
      });
    }
    return items;
  }

  const columns: DataColumn<TeamListItem>[] = [
    {
      key: 'team',
      header: orgTeamsPage.columns.team,
      render: (team) => (
        <TruncatedText to={organizationTeamPath(organizationId, team.id)} look="link">
          {team.name}
        </TruncatedText>
      ),
    },
    {
      key: 'projects',
      header: orgTeamsPage.columns.projects,
      render: (team) =>
        team.projects.length > 0 ? (
          <TruncatedText>{team.projects.map((project) => project.name).join(', ')}</TruncatedText>
        ) : (
          <Box component="span" sx={{ color: 'text.secondary' }}>
            {orgTeamsPage.noProjects}
          </Box>
        ),
    },
    {
      key: 'description',
      header: orgTeamsPage.columns.description,
      render: (team) =>
        team.description ? <TruncatedText>{team.description}</TruncatedText> : null,
    },
  ];
  const rows = query.data ?? [];
  if (canWrite || canDelete) {
    columns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (team) => (
        <MoreActionsMenu
          label={orgTeamsPage.rowMenu(team.name)}
          size="row"
          items={rowItems(team)}
        />
      ),
    });
  }

  return (
    <>
      <PageHeader
        title={orgTeamsPage.title}
        primaryAction={
          canCreate
            ? { label: orgTeamsPage.create, icon: <AddIcon />, onClick: () => setCreating(true) }
            : undefined
        }
      />
      <DataTable
        aria-label={orgTeamsPage.tableLabel}
        columns={columns}
        rows={rows}
        rowKey={(team) => team.id}
        loading={query.isPending || (query.isError && query.isFetching)}
        error={query.isError && !query.isFetching ? apiErrorText(query.error) : undefined}
        onRetry={() => void query.refetch()}
        empty={{
          icon: InboxOutlinedIcon,
          heading: orgTeamsPage.empty.heading,
          hint: canCreate ? orgTeamsPage.empty.adminHint : orgTeamsPage.empty.otherHint,
          action: canCreate
            ? { label: orgTeamsPage.create, icon: <AddIcon />, onClick: () => setCreating(true) }
            : undefined,
        }}
      />
      {creating && (
        <RequiresOrganizationCode code={organizationsPermissions.teamCreate}>
          <CreateTeamDialog
            organizationId={organizationId}
            canLink={canLink}
            canLinkAreas={
              access(organizationsPermissions.areaLink, 'organization') &&
              access(organizationsPermissions.teamLink, 'organization')
            }
            submit={(body) => create.mutateAsync(body)}
            onSucceeded={(team) => {
              setCreating(false);
              notify(createTeamDialog.created(team.name));
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
      {dialog && (
        <RequiresOrganizationCode
          code={
            dialog.kind === 'delete'
              ? organizationsPermissions.teamDelete
              : organizationsPermissions.teamWrite
          }
        >
          <TeamDialogs organizationId={organizationId} dialog={dialog} onClose={closeDialog} />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
