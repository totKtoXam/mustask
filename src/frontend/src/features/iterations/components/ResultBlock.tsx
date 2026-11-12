import Box from '@mui/material/Box';
import {
  ActionButton,
  DataTable,
  ErrorBanner,
  InlineEmpty,
  InlineNotice,
  Section,
  TruncatedText,
  type DataColumn,
} from '../../../shared/ui';
import type { OutcomeItem, OutcomeItemsPage } from '../api/iterationsApi';
import { sprintTexts } from '../lib/sprintTexts';
import { outcomeItemPath } from '../paths';

const t = sprintTexts.outcome.result;

/** The read the block takes: the infinite query's own fields, no more. */
export interface ResultRead {
  data: { pages: OutcomeItemsPage[] } | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  hasNextPage: boolean;
  isFetchingNextPage: boolean;
  isFetchNextPageError: boolean;
  refetch(): unknown;
  fetchNextPage(): unknown;
}

/**
 * «Итог спринта» (design § 6.5, US-9): the done items the sprint ended with, whatever the level.
 * A purged item has no title and no page: its identifier stands plain with «Удалён навсегда».
 */
export function ResultBlock({
  organizationId,
  read,
}: {
  organizationId: string;
  read: ResultRead;
}) {
  const pages = read.data?.pages ?? [];
  const rows = pages.flatMap((page) => page.items);
  const last = pages.at(-1);

  const columns: DataColumn<OutcomeItem>[] = [
    {
      key: 'identifier',
      header: t.columns.identifier,
      render: (row) =>
        row.purged ? (
          row.identifier
        ) : (
          <TruncatedText
            to={outcomeItemPath(organizationId, row.project.id, row.identifier)}
            look="link"
          >
            {row.identifier}
          </TruncatedText>
        ),
    },
    {
      key: 'title',
      header: t.columns.title,
      render: (row) => (row.purged ? t.purged : <TruncatedText>{row.title ?? ''}</TruncatedText>),
    },
    {
      key: 'project',
      header: t.columns.project,
      render: (row) => (row.purged ? '' : <TruncatedText>{row.project.name}</TruncatedText>),
    },
    {
      key: 'points',
      header: t.columns.storyPoints,
      align: 'right',
      render: (row) => row.storyPoints,
    },
  ];

  const failedFirst = read.isError && rows.length === 0;
  return (
    <Section title={t.title}>
      {failedFirst ? (
        <ErrorBanner error={read.error} title={t.failed} onRetry={() => void read.refetch()} />
      ) : (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          {last && <InlineEmpty>{t.count(last.totalItems, last.totalStoryPoints)}</InlineEmpty>}
          {read.isPending || rows.length > 0 ? (
            <DataTable
              aria-label={t.tableLabel}
              columns={columns}
              rows={rows}
              rowKey={(row) => row.identifier}
              loading={read.isPending}
            />
          ) : (
            <InlineEmpty>{t.empty}</InlineEmpty>
          )}
          {read.isFetchNextPageError && (
            <InlineNotice
              severity="error"
              action={{
                label: sprintTexts.outcome.retry,
                onClick: () => void read.fetchNextPage(),
              }}
            >
              {t.failed}
            </InlineNotice>
          )}
          {read.hasNextPage && (
            <ActionButton
              label={sprintTexts.outcome.more}
              busy={read.isFetchingNextPage}
              onClick={() => void read.fetchNextPage()}
              fullWidth
            />
          )}
        </Box>
      )}
    </Section>
  );
}
