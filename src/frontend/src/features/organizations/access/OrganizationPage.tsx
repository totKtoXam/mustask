import { Fragment, type ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { AccessDenied, NotFound } from '../../../shared/ui/StateFrame';
import { gateRow } from './accessAnswer';
import { useGate } from './OrganizationGate';

/** The title of the banner when the codes could not be read (design § 2.1 row 2, § 13 item 1). */
export const ACCESS_READ_FAILED = 'Не удалось загрузить права доступа';

/**
 * The registry skeleton of a page whose own headers the wrapper does not know
 * (the work-item list, the backlog, the board; plan-frontend P-13, OP-4): the
 * shape of a table with its words left to the page, which draws its own
 * skeleton when it mounts. No new string.
 */
const BLANK_COLUMNS = ['', '', ''];

export interface OrganizationPageProps {
  /** The organization code the page needs (a read code); without it the page is «Доступ ограничен». */
  code: string;
  /** The page's title: the `h1` of rows 1 and 2. */
  title: string;
  /**
   * The skeleton of row 1 (plan-frontend P-13): a registry's table with the
   * page's `columns`, or the facts' form. The page's own skeleton follows when
   * it mounts.
   */
  skeleton: 'registry' | 'form';
  /** The columns of the registry skeleton; an empty one is a blank column. */
  columns?: readonly string[];
  children: ReactNode;
}

/**
 * One page of an organization, in the one state order of mod-016 design § 2.1.
 * Under `OrganizationGate` it reads the served access of the organization in
 * the address before it shows anything of its own, and draws exactly one of:
 *
 * 1. nothing held and a read running — the page's header over its skeleton;
 * 2. nothing held, the last read failed — the header over the banner «Не
 *    удалось загрузить права доступа» with «Повторить»;
 * 3. a 404 held — «Организация не найдена», full page, held through any
 *    re-read that runs, fails or answers 404 again;
 * 4. codes held and the page's code missing — «Доступ ограничен»;
 * 5. codes held and the code held — the page, whose data reads start now.
 *
 * Row 1 never shows «Доступ ограничен»: unknown is not denied. The content is
 * keyed by the organization, so page state never survives a change of
 * organization.
 */
export function OrganizationPage({
  code,
  title,
  skeleton,
  columns = [],
  children,
}: OrganizationPageProps) {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const gate = useGate();
  const holds = gate.answer?.kind === 'member' && gate.answer.permissions.includes(code);

  switch (gateRow(gate, holds)) {
    case 'loading':
      return (
        <>
          <PageHeader title={title} />
          {skeleton === 'registry' ? (
            <RegistrySkeleton columns={columns.length > 0 ? [...columns] : BLANK_COLUMNS} />
          ) : (
            <FormSkeleton />
          )}
        </>
      );
    case 'failed':
      return (
        <>
          <PageHeader title={title} />
          <ErrorBanner error={gate.error} onRetry={gate.retry} title={ACCESS_READ_FAILED} />
        </>
      );
    case 'notFound':
      return <NotFound level="organization" />;
    case 'denied':
      return <AccessDenied />;
    case 'content':
      return <Fragment key={organizationId}>{children}</Fragment>;
  }
}
