import { useState } from 'react';
import Box from '@mui/material/Box';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
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
import type { TeamArea } from '../api/organizationsApi';
import { useLinkAreasToTeam, useUnlinkAreaFromTeam } from '../hooks/mutations';
import { useTeamAreas } from '../hooks/queries';
import { areaLinkDialog, areaUnlinkDialog, teamDepartmentsSection } from '../lib/areaTexts';
import { organizationAreaPath } from '../paths';
import { organizationsPermissions } from '../permissions';
import { AreaLinkDialog, AreaUnlinkDialog } from './AreaLinkDialog';
import { useNotify } from './notice';

/**
 * «Департаменты» of a team's page (mod-018 design § 6, D-10): the departments it is in, by name, each
 * a link to the department's page, with the link and the unlink from the team's end. Only a department
 * takes a team link (OQ-8 A). The link needs `area.link` and `team.link`, an unlink the team's own link
 * code alone; without them the section is read-only. It loads and fails on its own.
 */
export function TeamDepartmentsSection({
  organizationId,
  teamId,
  teamName,
}: {
  organizationId: string;
  teamId: string;
  teamName: string;
}) {
  const query = useTeamAreas(organizationId, teamId);
  const link = useLinkAreasToTeam(organizationId, teamId);
  const unlink = useUnlinkAreaFromTeam(organizationId, teamId);
  const notify = useNotify();
  const access = useAccessCheck();
  const canLink =
    access(organizationsPermissions.areaLink, 'organization') &&
    access(organizationsPermissions.teamLink, 'organization');
  // The team-end unlink is `team.link` alone (api-contract § 5, BR-MX-6: no second code).
  const canUnlink = access(organizationsPermissions.teamLink, 'organization');
  const [linking, setLinking] = useState(false);
  const [unlinking, setUnlinking] = useState<TeamArea | null>(null);
  const rows = query.data ?? [];

  const columns: DataColumn<TeamArea>[] = [
    {
      key: 'department',
      header: teamDepartmentsSection.column,
      render: (department) => (
        <TruncatedText to={organizationAreaPath(organizationId, department.id)} look="link">
          {department.path}
        </TruncatedText>
      ),
    },
  ];
  if (canUnlink) {
    columns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (department) => (
        <MoreActionsMenu
          label={teamDepartmentsSection.rowMenu(department.name)}
          size="row"
          items={[
            {
              label: teamDepartmentsSection.unlink,
              icon: <LinkOffOutlinedIcon />,
              onSelect: () => setUnlinking(department),
              destructive: true,
            },
          ]}
        />
      ),
    });
  }

  const texts = areaLinkDialog.teamDepartments;

  return (
    <>
      <Box sx={{ mt: 4 }}>
        <Section
          title={teamDepartmentsSection.title}
          count={query.isSuccess ? rows.length : undefined}
          actions={
            canLink ? (
              <ActionButton
                label={teamDepartmentsSection.add}
                icon={<ThinAddIcon />}
                onClick={() => setLinking(true)}
              />
            ) : undefined
          }
        >
          {query.isSuccess && rows.length === 0 ? (
            <InlineEmpty>{teamDepartmentsSection.empty}</InlineEmpty>
          ) : (
            <DataTable
              aria-label={teamDepartmentsSection.tableLabel}
              columns={columns}
              rows={rows}
              rowKey={(department) => department.id}
              loading={query.isPending || (query.isError && query.isFetching)}
              error={query.isError && !query.isFetching ? apiErrorText(query.error) : undefined}
              onRetry={() => void query.refetch()}
            />
          )}
        </Section>
      </Box>
      {linking && (
        <RequiresOrganizationCode code={organizationsPermissions.areaLink}>
          <AreaLinkDialog
            end="teamDepartments"
            organizationId={organizationId}
            ownName={teamName}
            linkedIds={rows.map((department) => department.id)}
            submit={(ids) => link.mutateAsync(ids)}
            onSucceeded={(chosen) => {
              setLinking(false);
              const [only] = chosen;
              notify(
                chosen.length === 1 && only
                  ? texts.linkedOne(only.name)
                  : texts.linkedMany(chosen.length),
              );
            }}
            onClose={(gone) => {
              setLinking(false);
              if (gone) {
                void query.refetch();
              }
            }}
          />
        </RequiresOrganizationCode>
      )}
      {unlinking && (
        <RequiresOrganizationCode code={organizationsPermissions.teamLink}>
          <AreaUnlinkDialog
            kind="teamDepartment"
            name={unlinking.path}
            ownName={teamName}
            action={() => unlink.mutateAsync(unlinking.id)}
            onSucceeded={() => {
              setUnlinking(null);
              notify(areaUnlinkDialog.teamDepartment.done);
            }}
            onClose={(stale) => {
              setUnlinking(null);
              if (stale) {
                void query.refetch();
              }
            }}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
