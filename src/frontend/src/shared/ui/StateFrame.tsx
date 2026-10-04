import { useLayoutEffect, type ReactElement, type Ref } from 'react';
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
  onClick?: () => void;
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
  fullPage = false,
  headingRef,
}: StateFrameProps) {
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
        pt: 8,
        pb: 4,
        px: 2,
      }}
    >
      <Icon fontSize="large" sx={{ color: 'text.secondary', mb: 1 }} />
      <Typography
        variant={fullPage ? 'h1' : 'h2'}
        component={fullPage ? 'h1' : 'h2'}
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
 */
export type NotFoundLevel = 'page' | 'organization' | 'project' | 'team';

const NOT_FOUND_HEADINGS: Record<NotFoundLevel, string> = {
  page: 'Страница не найдена',
  organization: 'Организация не найдена',
  project: 'Проект не найден',
  team: 'Команда не найдена',
};

export function NotFound({
  level,
  headingRef,
}: {
  level: NotFoundLevel;
  /** A handle on the frame's `h1`, as `StateFrame`'s `headingRef`. */
  headingRef?: Ref<HTMLHeadingElement>;
}) {
  // Whatever the address looks like, there is no page here: the shell marks
  // nothing current and shows no settings navigation (the spec's BPMN answers
  // «… не найдена» before that decision).
  const { registerNotFound } = usePageChrome();
  useLayoutEffect(() => registerNotFound(), [registerNotFound]);

  return (
    <StateFrame
      fullPage
      icon={SearchOffIcon}
      heading={NOT_FOUND_HEADINGS[level]}
      hint="Проверьте адрес или вернитесь на главную."
      action={{ label: 'На главную', icon: <HomeOutlinedIcon />, to: HOME_PATH }}
      headingRef={headingRef}
    />
  );
}
