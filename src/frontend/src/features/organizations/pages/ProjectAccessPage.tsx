import { useParams } from 'react-router-dom';
import { useObjectAccess } from '../../../shared/lib/objectAccess';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import { GrantsSection } from '../components/GrantsSection';
import { useProject } from '../hooks/queries';
import { grantsSection } from '../lib/grantTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import { organizationsPermissions } from '../permissions';

/** A project's «Участники и доступ» (mod-019 design § 3): the held grants and the department's inherited rows. */
export function ProjectAccessPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useProject(organizationId, projectId);
  const access = useObjectAccess(query.data?.codes);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }
  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <>
      <PageHeader title={grantsSection.titleObject} />
      {loading ? (
        <FormSkeleton fields={3} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <GrantsSection
          organizationId={organizationId}
          scope="project"
          objectId={projectId}
          objectName={query.data.name}
          canGrant={access(organizationsPermissions.projectGrant, 'organization')}
          title={grantsSection.titleObject}
          onContextGone={() => void query.refetch()}
        />
      )}
    </>
  );
}
