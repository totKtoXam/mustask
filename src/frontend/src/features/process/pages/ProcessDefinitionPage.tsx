import { useParams } from 'react-router-dom';
import Grid from '@mui/material/Grid';
import Typography from '@mui/material/Typography';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import { ProcessDefinitionSkeleton } from '../components/ProcessDefinitionSkeleton';
import { ProcessTypeCard } from '../components/ProcessTypeCard';
import { useProcessDefinition } from '../hooks/useProcessDefinition';
import { useRetryFocus } from '../hooks/useRetryFocus';
import { isOrganizationNotFound } from '../lib/readFailure';

/**
 * The read-only process definition of an organization (mod-001 US-2,
 * design.md layout B): the title, two sentences of description and a card
 * per type. Four states — loading, content, read failed with «Повторить»,
 * and «Организация не найдена» — and no empty or access-denied state (design
 * DD-5). The page makes no permission check (DD-6): the organization gate
 * above it does (mod-016 design § 2.1) — a non-member meets «Организация не
 * найдена», a member without the read code «Доступ ограничен» — and the API's
 * not-found answer, which this page already renders, is the same frame for a
 * read that races a removal.
 *
 * The header stays mounted across loading, content and a failed read; only
 * the not-found frame replaces the whole page, title included. The page
 * passes no ancestors: the shell derives the trail from the navigation model.
 */
export function ProcessDefinitionPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useProcessDefinition(organizationId);
  const notFound = query.isError && isOrganizationNotFound(query.error);
  const { titleRef, notFoundHeadingRef, retryRef, requestRetryFocus } = useRetryFocus({
    isFetching: query.isFetching,
    isSuccess: query.isSuccess,
    isError: query.isError,
    isNotFound: notFound,
  });

  if (notFound) {
    return <NotFound level="organization" headingRef={notFoundHeadingRef} />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);

  function retry() {
    requestRetryFocus();
    void query.refetch();
  }

  return (
    <>
      <PageHeader title="Процесс" titleRef={titleRef} />
      <Typography variant="body2" color="text.secondary">
        Типы рабочих элементов организации, их статусы и уровни бэклога. Процесс задан системой и
        доступен только для просмотра.
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1, mb: 2 }}>
        Категория показывает, к какой стадии работы относится статус.
      </Typography>
      {loading ? (
        <ProcessDefinitionSkeleton />
      ) : query.isError ? (
        <ErrorBanner
          title="Не удалось загрузить процесс"
          error={query.error}
          onRetry={retry}
          retryRef={retryRef}
        />
      ) : (
        <Grid container spacing={2}>
          {query.data.workItemTypes.map((type) => (
            <Grid key={type.id} size={{ xs: 12, lg: 6 }}>
              <ProcessTypeCard type={type} />
            </Grid>
          ))}
        </Grid>
      )}
    </>
  );
}
