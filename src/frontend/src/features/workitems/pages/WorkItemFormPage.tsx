import { useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import {
  CardSkeleton,
  ErrorBanner,
  NotFound,
  PageHeader,
  PanelShell,
  useNotice,
  usePageHead,
} from '../../../shared/ui';
import type { WorkItem } from '../api/workItemsApi';
import { PageFrame } from '../components/card/KitFrames';
import { WorkItemCard } from '../components/card/WorkItemCard';
import { useWorkItem } from '../hooks/queries';
import { useWorkItemAccess } from '../hooks/useWorkItemAccess';
import { cardTexts, loadingName } from '../lib/cardTexts';
import { notFoundLevel, type WorkItemsNotFoundLevel } from '../lib/notFoundLevel';
import { workItemsPath } from '../paths';

/**
 * A work item's page (feat-029 US-7; mod-003 spec § Page: a work item's form), a
 * project-level page at `…/workitems/:identifier` (D-5). It reads the item, keys the
 * card by the address — another item starts with its own values — and hosts it in the
 * kit's page panel; everything the card shows and does is `WorkItemCard`'s, the same
 * card the dialog hosts. The read validates the whole address and answers the
 * outermost level not found; so does a save that finds the item gone.
 */
export function WorkItemFormPage() {
  const {
    organizationId = '',
    projectId = '',
    identifier = '',
  } = useParams<{ organizationId: string; projectId: string; identifier: string }>();
  return (
    <WorkItemPage
      key={`${organizationId}/${projectId}/${identifier}`}
      organizationId={organizationId}
      projectId={projectId}
      identifier={identifier}
    />
  );
}

function WorkItemPage({
  organizationId,
  projectId,
  identifier,
}: {
  organizationId: string;
  projectId: string;
  identifier: string;
}) {
  const query = useWorkItem(organizationId, projectId, identifier);
  const [gone, setGone] = useState<WorkItemsNotFoundLevel | null>(null);

  const level = gone ?? (query.isError ? notFoundLevel(query.error) : null);
  if (level) {
    // The item's own frame keeps «Рабочие элементы» marked: its project is
    // found, as mod-002 keeps «Команды» for a team that is not (its § 12).
    return <NotFound level={level} keepNavigation={level === 'workItem'} />;
  }
  if (query.isPending || (query.isError && query.isFetching)) {
    return <PageLoading identifier={identifier} />;
  }
  if (query.isError) {
    return (
      <>
        <PageHeader title={cardTexts.frame.errorTitle} />
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      </>
    );
  }
  return (
    <PageCard
      organizationId={organizationId}
      projectId={projectId}
      item={query.data}
      onGone={setGone}
    />
  );
}

/**
 * Loading (design § 6): the card's frame at once with the kit's skeleton in its shape,
 * announced once as «Загрузка данных». The page has its `h1` meanwhile — the type is
 * not known yet — so it is named «Рабочий элемент <ID>».
 */
function PageLoading({ identifier }: { identifier: string }) {
  const { trail } = usePageHead({});
  return (
    <PanelShell trail={trail}>
      <CardSkeleton heading={{ text: loadingName(identifier), level: 1 }} />
    </PanelShell>
  );
}

/** The loaded card in the page's panel: the type line is the `h1` and the browser tab. */
function PageCard({
  organizationId,
  projectId,
  item,
  onGone,
}: {
  organizationId: string;
  projectId: string;
  item: WorkItem;
  onGone: (level: WorkItemsNotFoundLevel) => void;
}) {
  const { show } = useNotice();
  // The role views (mod-016 design § 6.5): the card is read-only without the write
  // code, and «Родитель» alone is without the link code.
  const { readOnly, parentReadOnly, canDelete, canPurge } = useWorkItemAccess();
  const navigate = useNavigate();
  const location = useLocation();
  const { trail } = usePageHead({ title: `${item.type.name} ${item.identifier}` });

  // «Сохранить и закрыть» goes back in history, or to «Рабочие элементы» when the
  // page was the first the user opened (US-4).
  function goBack() {
    if (location.key === 'default') {
      navigate(workItemsPath(organizationId, projectId));
    } else {
      navigate(-1);
    }
  }

  return (
    <WorkItemCard
      organizationId={organizationId}
      projectId={projectId}
      item={item}
      mode="page"
      readOnly={readOnly}
      parentReadOnly={parentReadOnly}
      canDelete={canDelete}
      canPurge={canPurge}
      notify={show}
      onGone={onGone}
      onClose={goBack}
      // The purged address must not be one Back returns to (mod-005 design § 6).
      onPurged={() => navigate(workItemsPath(organizationId, projectId), { replace: true })}
      renderFrame={(frame) => <PageFrame frame={frame} trail={trail} />}
    />
  );
}
