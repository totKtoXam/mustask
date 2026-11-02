import { useEffect, useMemo, useRef } from 'react';
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom';
import Link from '@mui/material/Link';
import { useTheme } from '@mui/material/styles';
import { organizationOverviewPath } from '../../features/organizations';
import { useAccessCheck } from '../../shared/lib/access';
import { objectsOf, useWorkContext } from '../../shared/lib/workContext';
import { useNotice } from '../../shared/ui';
import { targetAfterChoice } from '../navigation/choose';
import { fallbackNotice, readAddressSelection } from '../navigation/selection';
import {
  NO_SELECTION,
  resetNotice,
  resetUnfitted,
  type ObjectSelection,
} from '../navigation/selectorFit';
import { ContextSelectors } from './ContextSelectors';

/**
 * Moves focus to the `h1` of the page on screen, trying for about half a second since the page
 * may still be drawing its head; returns the cancel for the effect.
 */
function focusHeadingWhenReady(): () => void {
  let tries = 0;
  let timer: number | undefined;
  const attempt = () => {
    const heading = document.querySelector<HTMLElement>('main h1');
    if (heading) {
      heading.tabIndex = -1;
      heading.focus();
      return;
    }
    tries += 1;
    if (tries < 10) timer = window.setTimeout(attempt, 50);
  };
  timer = window.setTimeout(attempt, 0);
  return () => window.clearTimeout(timer);
}

/**
 * The header's four selectors on the shell's context (feat-043 design § 3; US-8): reads the
 * source, moves the address on a choice (the address is the selection, ADR-0032 § 1) and says
 * the two notices of the context — «Выбор сброшен: …» after a choice from «Остальные» and the
 * fallback of a remembered selection that is gone.
 */
export function ContextHost() {
  const source = useWorkContext();
  const navigate = useNavigate();
  const { pathname, search } = useLocation();
  const access = useAccessCheck();
  const { show } = useNotice();
  const theme = useTheme();
  const { organizationId } = source.selection;
  const objects = useMemo(() => objectsOf(source.selection), [source.selection]);

  const { fallback, dismissFallback } = source;
  const fallbackId = fallback?.id;
  const fallbackNames = fallback?.names;
  useEffect(() => {
    if (fallbackId === undefined || fallbackNames === undefined) return undefined;
    show(fallbackNotice(fallbackNames));
    // The shell's other notice waits while this one is up (mod-016 DC-11).
    const timer = window.setTimeout(() => dismissFallback?.(), theme.noticeDurationMs);
    return () => window.clearTimeout(timer);
  }, [fallbackId, fallbackNames, show, dismissFallback, theme.noticeDurationMs]);

  // A row or a link that reset a selector says so (D-3): the notice rides in the router state,
  // and is taken off the history entry once said, so a reload or a remount of the bar does not
  // say it again.
  const { key, state } = useLocation();
  const linkNotice =
    typeof state === 'object' && state !== null && 'resetNotice' in state
      ? String((state as { resetNotice: unknown }).resetNotice)
      : null;
  useEffect(() => {
    if (!linkNotice) return;
    show(linkNotice);
    const rest = { ...(state as Record<string, unknown>) };
    delete rest.resetNotice;
    navigate({ pathname, search }, { replace: true, state: rest });
    // One notice per navigation: the location's key tells one from the next.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  // After a choice or a row link focus lands on the page's `h1` (design § 9).
  const focusAfterChoice = useRef(false);
  const linkWantsFocus =
    typeof state === 'object' &&
    state !== null &&
    'focusHeading' in state &&
    (state as { focusHeading: unknown }).focusHeading === true;
  useEffect(() => {
    if (!focusAfterChoice.current && !linkWantsFocus) return undefined;
    focusAfterChoice.current = false;
    return focusHeadingWhenReady();
    // One move per navigation: the location's key tells one from the next.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  // A link change that unfits what is chosen (a team unlinked from the chosen project, a project
  // moved out of the chosen department) resets the more specific value with the notice (BR-NV-17;
  // ADR-0032 § 5; US-7). Judged only when the offers change under a selection that fitted before:
  // an address that unfits at its first read is not found for its level instead.
  const offers = source.offers;
  const seen = useRef<{ organizationId: string; offers: typeof offers } | null>(null);
  const latest = useRef({ objects, pathname });
  useEffect(() => {
    latest.current = { objects, pathname };
  });
  useEffect(() => {
    const before = seen.current;
    seen.current = offers && organizationId ? { organizationId, offers } : null;
    if (!before?.offers || !offers || !organizationId || before.organizationId !== organizationId) {
      return;
    }
    if (before.offers === offers) return;
    const current = latest.current;
    const result = resetUnfitted(before.offers, offers, current.objects);
    const text = resetNotice(result.reset);
    if (text === null) return;
    source.remember?.(organizationId, result.selection);
    if (readAddressSelection(current.pathname, '').organizationId !== undefined) {
      navigate(
        targetAfterChoice({
          organizationId,
          pathname: current.pathname,
          from: current.objects,
          to: result.selection,
          canOpen: (code) => access(code, 'organization', organizationId),
        }),
        { replace: true },
      );
    }
    show(text);
    // The offers are the trigger; the rest is read when they change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [offers]);

  function change(next: ObjectSelection, notice: string | null) {
    if (organizationId === undefined) return;
    source.remember?.(organizationId, next);
    focusAfterChoice.current = true;
    navigate(
      targetAfterChoice({
        organizationId,
        pathname,
        from: objects,
        to: next,
        canOpen: (code) => access(code, 'organization', organizationId),
      }),
    );
    if (notice) show(notice);
  }

  if (source.status === 'ready' && source.organizations.length === 0) {
    return source.organizationListPath ? (
      <Link component={RouterLink} to={source.organizationListPath} variant="body2">
        Нет организаций
      </Link>
    ) : null;
  }

  const offersStatus = source.levelStatus?.project ?? 'ready';
  return (
    <ContextSelectors
      organizations={source.organizations}
      organizationId={organizationId}
      data={source.offers}
      dataStatus={offersStatus === 'failed' ? 'error' : offersStatus}
      onRetry={() => source.retryOffers?.()}
      organizationsStatus={source.status === 'failed' ? 'error' : source.status}
      onRetryOrganizations={() => source.retry?.()}
      selection={objects}
      onOrganization={(id) => {
        // Another organization opens with «все» in the other three (BR-NV-10): a choice, so remembered.
        source.remember?.(id, NO_SELECTION);
        focusAfterChoice.current = true;
        navigate(organizationOverviewPath(id));
      }}
      onAllOrganizations={() => {
        source.clearOrganizations?.();
        navigate(source.organizationListPath ?? '/organizations');
      }}
      onChange={change}
    />
  );
}
