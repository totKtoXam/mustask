import { useEffect, useMemo } from 'react';
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom';
import Link from '@mui/material/Link';
import { useTheme } from '@mui/material/styles';
import { organizationOverviewPath } from '../../features/organizations';
import { useAccessCheck } from '../../shared/lib/access';
import { objectsOf, useWorkContext } from '../../shared/lib/workContext';
import { useNotice } from '../../shared/ui';
import { targetAfterChoice } from '../navigation/choose';
import { fallbackNotice } from '../navigation/selection';
import type { ObjectSelection } from '../navigation/selectorFit';
import { ContextSelectors } from './ContextSelectors';

/**
 * The header's four selectors on the shell's context (feat-043 design § 3; US-8): reads the
 * source, moves the address on a choice (the address is the selection, ADR-0032 § 1) and says
 * the two notices of the context — «Выбор сброшен: …» after a choice from «Остальные» and the
 * fallback of a remembered selection that is gone.
 */
export function ContextHost() {
  const source = useWorkContext();
  const navigate = useNavigate();
  const { pathname } = useLocation();
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

  function change(next: ObjectSelection, notice: string | null) {
    if (organizationId === undefined) return;
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
      onOrganization={(id) => navigate(organizationOverviewPath(id))}
      onAllOrganizations={() => {
        source.clearOrganizations?.();
        navigate(source.organizationListPath ?? '/organizations');
      }}
      onChange={change}
    />
  );
}
