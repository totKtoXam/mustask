import { useLayoutEffect, type MouseEvent, type ReactElement, type Ref } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import type { SvgIconComponent } from '@mui/icons-material';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import SearchOffIcon from '@mui/icons-material/SearchOff';
import { HOME_PATH } from '../config/paths';
import { useDocumentTitle } from '../lib/documentTitle';
import { usePageChrome } from './pageChrome';

/** The frame's one action: a button with `onClick`, or a link with `to`. */
export interface StateFrameAction {
  label: string;
  /** A leading icon, where one fits (design § 7.1). */
  icon?: ReactElement;
  /** Receives the click, so an action that opens a menu anchors it on `currentTarget` (mod-003). */
  onClick?: (event: MouseEvent<HTMLElement>) => void;
  to?: string;
}

export interface StateFrameProps {
  icon: SvgIconComponent;
  heading: string;
  hint?: string;
  /** At most one action, and only one the user may take (BR-9). */
  action?: StateFrameAction;
  /**
   * The frame replaces the whole page (access denied, not found): its heading
   * is the page's `h1` and the browser tab repeats it. Otherwise it stands
   * under a page header and its heading is an `h2`.
   */
  fullPage?: boolean;
  /**
   * A frame inside a dialog or a panel (tech-015 design § 5.12): `pt 2`, `pb 2`,
   * an `h2` and **no tab title** — the page behind keeps its own. It implies not
   * `fullPage`: given both, the frame is compact.
   */
  compact?: boolean;
  /**
   * The heading's level for a frame that is not full page and stands under an
   * `h2` — the empty team's frame inside «Участники команды» (mod-016 design
   * § 4.3): the element is an `h3`, the look is the `h2`'s. Absent: `h2`. A
   * full-page frame is always the page's `h1`.
   */
  headingLevel?: 2 | 3;
  /**
   * A handle on the heading, for a page that moves focus to it — after a
   * retry that ended in not found (mod-001 review round 1 F4). Given, the
   * heading takes the ref and `tabIndex={-1}`; absent, it is as before.
   */
  headingRef?: Ref<HTMLHeadingElement>;
}

/** The class every canon frame carries, whatever its variant (US-7). */
export const STATE_FRAME_CLASS = 'StateFrame-root';

/**
 * The screen-state canon's one frame (US-7, BR-12, design § 7): an icon, a
 * heading, a hint and at most one action, centred in the area it replaces.
 * Empty, empty after a filter, access denied and not found all use it. It is
 * not an alert: the user is told where they are, not interrupted.
 */
export function StateFrame({
  icon: Icon,
  heading,
  hint,
  action,
  fullPage: fullPageRequested = false,
  compact = false,
  headingLevel = 2,
  headingRef,
}: StateFrameProps) {
  // A compact frame is the dialog's or the panel's: it never replaces the page.
  const fullPage = fullPageRequested && !compact;
  useDocumentTitle(fullPage ? heading : undefined);

  return (
    <Box
      className={STATE_FRAME_CLASS}
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: 1,
        pt: compact ? 2 : 8,
        pb: compact ? 2 : 4,
        px: 2,
      }}
    >
      <Icon fontSize="large" sx={{ color: 'text.secondary', mb: 1 }} />
      <Typography
        variant={fullPage ? 'h1' : 'h2'}
        component={fullPage ? 'h1' : headingLevel === 3 ? 'h3' : 'h2'}
        ref={headingRef}
        tabIndex={headingRef ? -1 : undefined}
      >
        {heading}
      </Typography>
      {hint && (
        <Typography variant="body1" color="text.secondary">
          {hint}
        </Typography>
      )}
      {action && (
        <Box sx={{ mt: 1 }}>
          {action.to ? (
            <Button
              variant="outlined"
              startIcon={action.icon}
              component={RouterLink}
              to={action.to}
            >
              {action.label}
            </Button>
          ) : (
            <Button variant="outlined" startIcon={action.icon} onClick={action.onClick}>
              {action.label}
            </Button>
          )}
        </Box>
      )}
    </Box>
  );
}

/**
 * «Доступ ограничен» (BR-7, design § 7.2): the page the user lacks the code
 * for. Its texts are the canon's, unchanged since ADR-0007 (US-11 C-14).
 */
export function AccessDenied() {
  return (
    <StateFrame
      fullPage
      icon={LockOutlinedIcon}
      heading="Доступ ограничен"
      hint="У вас нет прав для просмотра этого раздела. Обратитесь к администратору, чтобы запросить доступ."
    />
  );
}

/**
 * What was not found (BR-12, BR-13, BR-18, D-15): an unknown panel address,
 * or the organization, project or team a context address names. A context
 * that does not exist and one the user may not see get the same frame.
 * mod-003 adds the work item its form's address names (its spec § Not-found
 * answers), with the same icon, hint and action.
 */
export type NotFoundLevel = 'page' | 'organization' | 'project' | 'team' | 'area' | 'workItem';

const NOT_FOUND_HEADINGS: Record<NotFoundLevel, string> = {
  page: 'Страница не найдена',
  organization: 'Организация не найдена',
  project: 'Проект не найден',
  team: 'Команда не найдена',
  area: 'Область не найдена',
  workItem: 'Рабочий элемент не найден',
};

export function NotFound({
  level,
  compact = false,
  hint = 'Проверьте адрес или вернитесь на главную.',
  action = { label: 'На главную', icon: <HomeOutlinedIcon />, to: HOME_PATH },
  headingRef,
  keepNavigation = false,
}: {
  level: NotFoundLevel;
  /**
   * The frame stands inside a dialog or a panel over a page that is found
   * (tech-015 design § 5.12): an `h2`, no tab title and no registration with the
   * shell — the chrome is the host page's. It implies `keepNavigation`.
   */
  compact?: boolean;
  /** The host's own words — the dialog's «Проверьте адрес или закройте окно.». */
  hint?: string;
  /** The host's own action — the dialog's «Закрыть». Absent: «На главную». */
  action?: StateFrameAction;
  /** A handle on the frame's `h1`, as `StateFrame`'s `headingRef`. */
  headingRef?: Ref<HTMLHeadingElement>;
  /**
   * The frame answers an entity beneath a page the shell serves — a team on
   * its page beneath «Команды», whose organization and project are found
   * (mod-002 design § 12): the shell keeps the settings navigation and the
   * entry's marker. Absent: the frame registers not-found, as before.
   */
  keepNavigation?: boolean;
}) {
  // Whatever the address looks like, there is no page here: the shell marks
  // nothing current and shows no settings navigation (the spec's BPMN answers
  // «… не найдена» before that decision). A compact frame is not the page's
  // answer, so it leaves the shell alone.
  const { registerNotFound } = usePageChrome();
  const leaveShellAlone = keepNavigation || compact;
  useLayoutEffect(
    () => (leaveShellAlone ? undefined : registerNotFound()),
    [registerNotFound, leaveShellAlone],
  );

  return (
    <StateFrame
      fullPage={!compact}
      compact={compact}
      icon={SearchOffIcon}
      heading={NOT_FOUND_HEADINGS[level]}
      hint={hint}
      action={action}
      headingRef={headingRef}
    />
  );
}
