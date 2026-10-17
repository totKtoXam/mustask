import { useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import StarBorderIcon from '@mui/icons-material/StarBorder';
import { useAccessCheck } from '../../../shared/lib/access';
import { Tag } from '../../../shared/ui';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import type { Team } from '../api/organizationsApi';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import { RowActionsMenu, type RowAction } from '../components/RowActionsMenu';
import { TeamDialogs, type TeamDialog } from '../components/TeamDialogs';
import { TruncatedName } from '../components/TruncatedName';
import { useAddTeam } from '../hooks/mutations';
import { useTeams } from '../hooks/queries';
import { DEFAULT_MARK_LABEL } from '../lib/defaultMark';
import { notFoundLevel } from '../lib/notFoundLevel';
import { teamPath } from '../paths';
import { organizationsPermissions } from '../permissions';

/**
 * «Команды» (mod-017 US-3, US-6, US-7; design § 7): a project's active teams by
 * name, as the server orders them — no team comes first. Each shows its mark
 * «По умолчанию» when it is the project's default team, and has its row menu.
 * «Команда по умолчанию» and «Добавить команду» are the holder of
 * `organizations.team.write`'s; a project's last team has no «Удалить команду»
 * (BR-OT-11). There is no empty state: a project always has a team.
 */
export function TeamsPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useTeams(organizationId, projectId);
  const addTeam = useAddTeam(organizationId, projectId);
  const notify = useNotify();
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.teamWrite, 'organization');
  const [adding, setAdding] = useState(false);
  const [dialog, setDialog] = useState<TeamDialog>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    setAdding(false);
    if (contextGone) {
      void query.refetch();
    }
  }

  const teams = query.data ?? [];

  function rowActions(team: Team): RowAction[] {
    const actions: RowAction[] = [
      {
        label: 'Переименовать',
        icon: <EditOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'rename', team }),
      },
    ];
    // BR-OT-11: a project's last team cannot be deleted (the interim guard).
    if (teams.length > 1) {
      actions.push({
        label: 'Удалить команду',
        icon: <DeleteOutlineOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'delete', team }),
        destructive: true,
      });
    }
    return actions;
  }

  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <>
      <PageHeader
        title="Команды"
        primaryAction={
          canWrite
            ? {
                label: 'Добавить команду',
                icon: <AddIcon />,
                onClick: () => setAdding(true),
              }
            : undefined
        }
        commands={
          canWrite && query.isSuccess
            ? [
                {
                  label: 'Команда по умолчанию',
                  icon: <StarBorderIcon />,
                  onClick: () => setDialog({ kind: 'defaultTeam' }),
                },
              ]
            : undefined
        }
      />
      {loading ? (
        <RegistrySkeleton columns={canWrite ? ['Команда', ''] : ['Команда']} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small" aria-label="Команды">
            <TableHead>
              <TableRow>
                <TableCell>Команда</TableCell>
                {canWrite && <TableCell />}
              </TableRow>
            </TableHead>
            <TableBody>
              {teams.map((team) => (
                <TableRow key={team.id} hover>
                  <TableCell sx={{ maxWidth: 0, width: '100%' }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
                      <Box sx={{ minWidth: 0 }}>
                        <TruncatedName
                          name={team.name}
                          to={teamPath(organizationId, projectId, team.id)}
                        />
                      </Box>
                      {team.defaultMark && (
                        <Box sx={{ flexShrink: 0 }}>
                          <Tag label={DEFAULT_MARK_LABEL[team.defaultMark]} />
                        </Box>
                      )}
                    </Box>
                  </TableCell>
                  {canWrite && (
                    <TableCell align="right">
                      <RowActionsMenu name={team.name} actions={rowActions(team)} />
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      <RequiresOrganizationCode code={organizationsPermissions.teamWrite}>
        {adding && (
          <NameDialog
            title="Новая команда"
            helper="От 1 до 100 символов."
            submitLabel="Добавить"
            submit={(name) => addTeam.mutateAsync(name)}
            onSucceeded={(team) => {
              setAdding(false);
              notify(`Команда «${team.name}» добавлена.`);
            }}
            onClose={closeDialog}
          />
        )}
        <TeamDialogs
          organizationId={organizationId}
          projectId={projectId}
          dialog={dialog}
          teams={teams}
          onClose={closeDialog}
          onTeamsStale={() => void query.refetch()}
        />
      </RequiresOrganizationCode>
    </>
  );
}
