import { useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { NotFound } from '../../../shared/ui/StateFrame';
import type { Team } from '../api/organizationsApi';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import { RowActionsMenu, type RowAction } from '../components/RowActionsMenu';
import { TeamDialogs, type TeamDialog } from '../components/TeamDialogs';
import { TruncatedName } from '../components/TruncatedName';
import { useAddTeam } from '../hooks/mutations';
import { useTeams } from '../hooks/queries';
import { notFoundLevel } from '../lib/notFoundLevel';
import { teamPath } from '../paths';

/** The first team's mark: text, never colour alone (design § 9.2). */
export const FIRST_TEAM_CHIP = 'Владеет корневой областью';

/**
 * «Команды» (US-4, US-7; design § 9): a project's active teams, the first
 * team first with its chip, each with its default area's path and its row
 * menu. There is no empty state: a project always has its first team
 * (BR-M1-9).
 */
export function TeamsPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useTeams(organizationId, projectId);
  const addTeam = useAddTeam(organizationId, projectId);
  const notify = useNotify();
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

  function rowActions(team: Team): RowAction[] {
    const rename: RowAction = {
      label: 'Переименовать',
      icon: <EditOutlinedIcon fontSize="small" />,
      onClick: () => setDialog({ kind: 'rename', team }),
    };
    if (team.isFirstTeam) {
      return [rename];
    }
    return [
      rename,
      {
        label: 'Изменить область по умолчанию',
        icon: <AccountTreeOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'defaultArea', team }),
      },
      {
        label: 'Удалить команду',
        icon: <DeleteOutlineOutlinedIcon fontSize="small" />,
        onClick: () => setDialog({ kind: 'delete', team }),
        destructive: true,
      },
    ];
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const firstTeamName = query.data?.find((team) => team.isFirstTeam)?.name ?? '';

  return (
    <>
      <PageHeader
        title="Команды"
        primaryAction={{
          label: 'Добавить команду',
          icon: <AddIcon />,
          onClick: () => setAdding(true),
        }}
      />
      {loading ? (
        <RegistrySkeleton columns={['Команда', 'Область по умолчанию', '']} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small" aria-label="Команды">
            <TableHead>
              <TableRow>
                <TableCell>Команда</TableCell>
                <TableCell>Область по умолчанию</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {query.data.map((team) => (
                <TableRow key={team.id} hover>
                  <TableCell sx={{ maxWidth: 0, width: '50%' }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
                      <Box sx={{ minWidth: 0 }}>
                        <TruncatedName
                          name={team.name}
                          to={teamPath(organizationId, projectId, team.id)}
                        />
                      </Box>
                      {team.isFirstTeam && <Chip size="small" label={FIRST_TEAM_CHIP} />}
                    </Box>
                  </TableCell>
                  <TableCell sx={{ maxWidth: 0, width: '50%' }}>
                    <TruncatedName name={team.defaultArea.path} />
                  </TableCell>
                  <TableCell align="right">
                    <RowActionsMenu name={team.name} actions={rowActions(team)} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {adding && (
        <NameDialog
          title="Новая команда"
          helper="От 1 до 100 символов. Под корневой областью появится область с тем же названием, и она станет областью по умолчанию команды."
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
        firstTeamName={firstTeamName}
        onClose={closeDialog}
      />
    </>
  );
}
