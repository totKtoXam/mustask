import { useParams } from 'react-router-dom';
import { useObjectAccess } from '../../../shared/lib/objectAccess';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import { GrantsSection } from '../components/GrantsSection';
import { useArea } from '../hooks/queries';
import { grantsSection } from '../lib/grantTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import { organizationsPermissions } from '../permissions';

/** A department's «Участники» (mod-019 design § 2): the grants of the department, given by its managers. */
export function AreaMembersPage() {
  const { organizationId = '', areaId = '' } = useParams<{
    organizationId: string;
    areaId: string;
  }>();
  const query = useArea(organizationId, areaId);
  const access = useObjectAccess(query.data?.codes);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }
  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <>
      <PageHeader title={grantsSection.titleDepartment} />
      {loading ? (
        <FormSkeleton fields={3} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <GrantsSection
          organizationId={organizationId}
          scope="department"
          objectId={areaId}
          objectName={query.data.name}
          canGrant={access(organizationsPermissions.areaGrant, 'organization')}
          title={grantsSection.titleDepartment}
          onContextGone={() => void query.refetch()}
        />
      )}
    </>
  );
}
