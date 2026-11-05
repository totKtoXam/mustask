import type { ReactNode } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import Typography from '@mui/material/Typography';
import {
  ColumnLayout,
  InlineEmpty,
  Section,
  Surface,
  Tag,
  TruncatedText,
} from '../../../shared/ui';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import { useOverview } from '../hooks/navigationQueries';
import { useOrganizations } from '../hooks/queries';
import { useObjectLink } from '../hooks/useObjectLink';
import { overviewPage } from '../lib/navigationTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import {
  ORGANIZATIONS_PATH,
  organizationAreasPath,
  organizationOverviewPath,
  organizationTeamsPath,
  projectsPath,
} from '../paths';

const FIRST_ORGANIZATIONS = 5;

function Rows({ children }: { children: ReactNode }) {
  return (
    <List dense disablePadding>
      {children}
    </List>
  );
}

/**
 * «Обзор» of the organization (feat-043 design § 5.1; US-2): the first page of an organization
 * with nothing chosen. One read of `GET …/overview` builds the figures, the first five rows of
 * each list and the user's own teams; the user's organizations stand beside it from the existing
 * list. Each figure and «Все …» opens its list.
 */
export function OrganizationOverviewPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useOverview(organizationId);
  const organizations = useOrganizations('active');
  const objectLink = useObjectLink(organizationId);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const overview = query.data;

  function objectRow(
    kind: 'department' | 'team' | 'project',
    id: string,
    name: string,
    tag?: string,
  ) {
    const link = objectLink(kind, id);
    return (
      <ListItem key={id} disableGutters sx={{ gap: 1 }}>
        <TruncatedText to={link.to} linkState={link.state} look="link">
          {name}
        </TruncatedText>
        {tag && <Tag label={tag} />}
      </ListItem>
    );
  }

  const stat = (count: number, label: string, to: string) => (
    <Link component={RouterLink} to={to} underline="none" sx={{ display: 'block', minWidth: 0 }}>
      <Typography variant="h2" component="span" sx={{ display: 'block' }}>
        {count}
      </Typography>
      <Typography variant="body2" component="span" color="text.secondary">
        {label}
      </Typography>
    </Link>
  );

  const main = overview ? (
    <>
      {overview.myTeams.length > 0 && (
        <Section title={overviewPage.myTeams}>
          <Rows>{overview.myTeams.map((team) => objectRow('team', team.id, team.name))}</Rows>
        </Section>
      )}
      <Surface variant="band" padding="dense" aria-label={overviewPage.statLabel}>
        <Box sx={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          {stat(
            overview.departmentCount,
            overviewPage.departments,
            organizationAreasPath(organizationId),
          )}
          {stat(overview.projectCount, overviewPage.projects, projectsPath(organizationId))}
          {stat(overview.teamCount, overviewPage.teams, organizationTeamsPath(organizationId))}
        </Box>
      </Surface>
      <Section title={overviewPage.departments}>
        {overview.departments.length === 0 ? (
          <InlineEmpty>{overviewPage.noDepartments}</InlineEmpty>
        ) : (
          <Rows>
            {overview.departments.map((department) =>
              objectRow('department', department.id, department.name),
            )}
          </Rows>
        )}
        {overview.departmentCount > 0 && (
          <Link component={RouterLink} to={organizationAreasPath(organizationId)}>
            {overviewPage.allDepartments(overview.departmentCount)}
          </Link>
        )}
      </Section>
      <Section title={overviewPage.projects}>
        {overview.projects.length === 0 ? (
          <InlineEmpty>{overviewPage.noProjects}</InlineEmpty>
        ) : (
          <Rows>
            {overview.projects.map((project) =>
              objectRow('project', project.id, project.name, project.key),
            )}
          </Rows>
        )}
        {overview.projectCount > 0 && (
          <Link component={RouterLink} to={projectsPath(organizationId)}>
            {overviewPage.allProjects(overview.projectCount)}
          </Link>
        )}
      </Section>
      <Section title={overviewPage.teams}>
        {overview.teams.length === 0 ? (
          <InlineEmpty>{overviewPage.noTeams}</InlineEmpty>
        ) : (
          <Rows>{overview.teams.map((team) => objectRow('team', team.id, team.name))}</Rows>
        )}
        {overview.teamCount > 0 && (
          <Link component={RouterLink} to={organizationTeamsPath(organizationId)}>
            {overviewPage.allTeams(overview.teamCount)}
          </Link>
        )}
      </Section>
    </>
  ) : null;

  const side = (
    <Section title={overviewPage.organizations}>
      <Rows>
        {(organizations.data ?? []).slice(0, FIRST_ORGANIZATIONS).map((organization) => (
          <ListItem key={organization.id} disableGutters sx={{ gap: 1 }}>
            <TruncatedText to={organizationOverviewPath(organization.id)} look="link">
              {organization.name}
            </TruncatedText>
            {organization.id === organizationId && <Tag label={overviewPage.currentOrganization} />}
          </ListItem>
        ))}
      </Rows>
      <Link component={RouterLink} to={ORGANIZATIONS_PATH}>
        {overviewPage.allOrganizations}
      </Link>
    </Section>
  );

  return (
    <>
      <PageHeader title={overviewPage.title} />
      {loading ? (
        <FormSkeleton fields={4} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <ColumnLayout main={main} side={side} />
      )}
    </>
  );
}
