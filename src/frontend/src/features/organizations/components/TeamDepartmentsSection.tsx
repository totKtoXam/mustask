import { useState } from 'react';
import Box from '@mui/material/Box';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { useWorkContext } from '../../../shared/lib/workContext';
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
import { useObjectLink } from '../hooks/useObjectLink';
import { unlinkResetsDepartmentTeam } from '../lib/linkResets';
import { areaLinkDialog, areaUnlinkDialog, teamDepartmentsSection } from '../lib/areaTexts';
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
  const objectLink = useObjectLink(organizationId);
  const notify = useNotify();
  const access = useAccessCheck();
  const context = useWorkContext();
  // The team's end; the department end is the picker's filter (`linkableFor`, api-contract § 4). A department-team
  // link is removed from either end (BR-SA-15), so each row says whether the caller may: `canUnlink`.
  const canLink = access(organizationsPermissions.teamLink, 'organization');
  const holdsTeamLink = canLink;
  const mayUnlink = (row: TeamArea) => row.canUnlink ?? holdsTeamLink;
  const [linking, setLinking] = useState(false);
  const [unlinking, setUnlinking] = useState<TeamArea | null>(null);
  const rows = query.data ?? [];

  const columns: DataColumn<TeamArea>[] = [
    {
      key: 'department',
      header: teamDepartmentsSection.column,
      render: (department) => {
        // The department becomes the chosen one; the more specific team and project reset to «все»
        // (`openObject`, BR-NV-19).
        const link = objectLink('department', department.id);
        return (
          <TruncatedText to={link.to} linkState={link.state} look="link">
            {department.path}
          </TruncatedText>
        );
      },
    },
  ];
  if (rows.some(mayUnlink)) {
    columns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (department) =>
        mayUnlink(department) ? (
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
        ) : null,
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
            action={async () => {
              // Judged before the shell sees the change: when the unlink resets the chosen
              // team, the shell's notice is the only one (D-4).
              const fallsBack = unlinkResetsDepartmentTeam(
                context,
                organizationId,
                unlinking.id,
                teamId,
              );
              await unlink.mutateAsync(unlinking.id);
              return fallsBack;
            }}
            onSucceeded={(fallsBack) => {
              setUnlinking(null);
              if (!fallsBack) {
                notify(areaUnlinkDialog.teamDepartment.done);
              }
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
