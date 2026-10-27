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
import type { AreaRef } from '../api/organizationsApi';
import {
  useLinkAreasToProject,
  useLinkAreasToTeam,
  useUnlinkAreaFromProject,
  useUnlinkAreaFromTeam,
} from '../hooks/mutations';
import { useProjectAreas, useTeamAreas } from '../hooks/queries';
import { areaLinkDialog, areaUnlinkDialog, endAreasSection } from '../lib/areaTexts';
import { organizationAreaPath } from '../paths';
import { organizationsPermissions } from '../permissions';
import { AreaLinkDialog, AreaUnlinkDialog } from './AreaLinkDialog';
import { useNotify } from './notice';

/**
 * «Области» of a project's or a team's page (mod-018 design § 4.2): the areas it is in, by path, each a
 * link to the area's page, with the link and the unlink from this end. Needs `area.link` and the end's
 * own link code for the controls; without them the section is read-only. It loads and fails on its own.
 */
export function EndAreasSection({
  end,
  organizationId,
  ownerId,
  ownerName,
}: {
  end: 'project' | 'team';
  organizationId: string;
  ownerId: string;
  ownerName: string;
}) {
  const projectAreas = useProjectAreas(organizationId, ownerId, end === 'project');
  const teamAreas = useTeamAreas(organizationId, ownerId, end === 'team');
  const query = end === 'project' ? projectAreas : teamAreas;
  const linkProject = useLinkAreasToProject(organizationId, ownerId);
  const linkTeam = useLinkAreasToTeam(organizationId, ownerId);
  const unlinkProject = useUnlinkAreaFromProject(organizationId, ownerId);
  const unlinkTeam = useUnlinkAreaFromTeam(organizationId, ownerId);
  const notify = useNotify();
  const access = useAccessCheck();
  const ownCode =
    end === 'project' ? organizationsPermissions.projectLink : organizationsPermissions.teamLink;
  const canLink =
    access(organizationsPermissions.areaLink, 'organization') && access(ownCode, 'organization');
  // An unlink is the end's own link code alone (api-contract § 3).
  const canUnlink = access(ownCode, 'organization');
  const [linking, setLinking] = useState(false);
  const [unlinking, setUnlinking] = useState<AreaRef | null>(null);
  const texts = end === 'project' ? endAreasSection.project : endAreasSection.team;
  const dialogEnd = end === 'project' ? 'projectAreas' : 'teamAreas';
  const rows = query.data ?? [];

  const columns: DataColumn<AreaRef>[] = [
    {
      key: 'area',
      header: endAreasSection.column,
      render: (area) => (
        <TruncatedText to={organizationAreaPath(organizationId, area.id)} look="link">
          {area.path}
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
      render: (area) => (
        <MoreActionsMenu
          label={endAreasSection.rowMenu(area.path)}
          size="row"
          items={[
            {
              label: texts.unlink,
              icon: <LinkOffOutlinedIcon />,
              onSelect: () => setUnlinking(area),
              destructive: true,
            },
          ]}
        />
      ),
    });
  }

  const texts2 = areaLinkDialog[dialogEnd];

  return (
    <>
      <Box sx={{ mt: 4 }}>
        <Section
          title={endAreasSection.title}
          count={query.isSuccess ? rows.length : undefined}
          actions={
            canLink ? (
              <ActionButton
                label={endAreasSection.add}
                icon={<ThinAddIcon />}
                onClick={() => setLinking(true)}
              />
            ) : undefined
          }
        >
          {query.isSuccess && rows.length === 0 ? (
            <InlineEmpty>{texts.empty}</InlineEmpty>
          ) : (
            <DataTable
              aria-label={texts.tableLabel}
              columns={columns}
              rows={rows}
              rowKey={(area) => area.id}
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
            end={dialogEnd}
            organizationId={organizationId}
            ownName={ownerName}
            linkedIds={rows.map((area) => area.id)}
            submit={(ids) =>
              end === 'project' ? linkProject.mutateAsync(ids) : linkTeam.mutateAsync(ids)
            }
            onSucceeded={(chosen) => {
              setLinking(false);
              const [only] = chosen;
              notify(
                chosen.length === 1 && only
                  ? texts2.linkedOne(only.name)
                  : texts2.linkedMany(chosen.length),
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
        <RequiresOrganizationCode code={ownCode}>
          <AreaUnlinkDialog
            kind={end === 'project' ? 'projectArea' : 'teamArea'}
            name={unlinking.path}
            ownName={ownerName}
            action={() =>
              end === 'project'
                ? unlinkProject.mutateAsync(unlinking.id)
                : unlinkTeam.mutateAsync(unlinking.id)
            }
            onSucceeded={() => {
              setUnlinking(null);
              notify(areaUnlinkDialog[end === 'project' ? 'projectArea' : 'teamArea'].done);
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
