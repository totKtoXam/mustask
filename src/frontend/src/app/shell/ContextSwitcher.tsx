import { useId, useState, type KeyboardEvent, type MouseEvent, type ReactNode } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Divider from '@mui/material/Divider';
import Link from '@mui/material/Link';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import ListSubheader from '@mui/material/ListSubheader';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Skeleton from '@mui/material/Skeleton';
import Snackbar from '@mui/material/Snackbar';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import CheckIcon from '@mui/icons-material/Check';
import ErrorOutlineOutlinedIcon from '@mui/icons-material/ErrorOutlineOutlined';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ListAltOutlinedIcon from '@mui/icons-material/ListAltOutlined';
import {
  resolveWorkContext,
  useWorkContext,
  type WorkContextLevel,
  type WorkContextLowerLevel,
} from '../../shared/lib/workContext';
import { useWidthClass } from '../navigation/useWidthClass';

/** Each level's name prefix in its accessible name (US-5). */
const LEVEL_NAMES: Record<WorkContextLevel, string> = {
  organization: 'Организация',
  project: 'Проект',
  team: 'Команда',
};

/** Each level's menu subheader (design § 5.3). */
const LEVEL_SUBHEADERS: Record<WorkContextLevel, string> = {
  organization: 'Организации',
  project: 'Проекты',
  team: 'Команды',
};

const FAILED_TEXT = 'Не удалось загрузить организации';

/** A lower level's failed row (mod-002 design § 3.2, D-n; mod-015 § 5.4 as amended). */
const LEVEL_FAILED_TEXTS: Record<WorkContextLowerLevel, string> = {
  project: 'Не удалось загрузить проекты',
  team: 'Не удалось загрузить команды',
};

/** Skeletons a loading level and the levels below it take (mod-015 § 5.4 as amended). */
const LEVEL_SKELETONS: Record<WorkContextLevel, number> = {
  organization: 3,
  project: 2,
  team: 1,
};

/** How long the fallback notice stays (mod-015 § 5.4, mod-002 design § 3.6). */
const NOTICE_DURATION_MS = 6000;

interface LevelOption {
  id: string;
  name: string;
}

function Separator() {
  return (
    <Typography component="span" aria-hidden color="text.secondary">
      /
    </Typography>
  );
}

/** «Нет проектов», «Нет команд»: a level with nothing to choose says so (BR-11). */
function NothingToChoose({ children }: { children: ReactNode }) {
  return (
    <Typography variant="body2" color="text.secondary" noWrap>
      {children}
    </Typography>
  );
}

/**
 * One level: a text button showing the current name, cut with an ellipsis at
 * the width class's limit and kept whole in its accessible name and tooltip
 * (BR-11, D-22), and a menu of the level's entries with the current one
 * checked. Enter, Space or ArrowDown opens the menu on the current entry;
 * Escape returns focus here (US-12).
 */
function ContextLevel({
  level,
  current,
  options,
  onSelect,
  footer,
}: {
  level: WorkContextLevel;
  current: LevelOption;
  options: readonly LevelOption[];
  onSelect: (id: string) => void;
  /** Entries after the level's choices; `close` closes the menu. */
  footer?: (close: () => void) => ReactNode;
}) {
  const widthClass = useWidthClass();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const menuId = useId();
  const open = anchor !== null;

  function openMenu(event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>) {
    setAnchor(event.currentTarget);
  }

  return (
    <>
      <Tooltip title={current.name}>
        <Button
          variant="text"
          color="inherit"
          endIcon={<ExpandMoreIcon />}
          aria-label={`${LEVEL_NAMES[level]}: ${current.name}`}
          aria-haspopup="menu"
          aria-expanded={open ? 'true' : 'false'}
          aria-controls={open ? menuId : undefined}
          onClick={openMenu}
          onKeyDown={(event) => {
            if (event.key === 'ArrowDown') {
              event.preventDefault();
              openMenu(event);
            }
          }}
          sx={{ minWidth: 0, flexShrink: 1, fontWeight: 'fontWeightRegular' }}
        >
          <Box
            component="span"
            sx={{
              display: 'block',
              maxWidth: (theme) => theme.layout.contextNameMaxWidth[widthClass],
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {current.name}
          </Box>
        </Button>
      </Tooltip>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={() => setAnchor(null)}
        variant="selectedMenu"
      >
        <ListSubheader>{LEVEL_SUBHEADERS[level]}</ListSubheader>
        {options.map((option) => {
          const selected = option.id === current.id;
          return (
            <MenuItem
              key={option.id}
              role="menuitemradio"
              aria-checked={selected}
              selected={selected}
              onClick={() => {
                setAnchor(null);
                onSelect(option.id);
              }}
            >
              <ListItemIcon>
                {selected ? <CheckIcon fontSize="small" sx={{ color: 'text.primary' }} /> : null}
              </ListItemIcon>
              <ListItemText>{option.name}</ListItemText>
            </MenuItem>
          );
        })}
        {footer?.(() => setAnchor(null))}
      </Menu>
    </>
  );
}

/** «Загрузка контекста»: a loading level and every level below it (mod-015 § 5.4). */
function LoadingLevels({ count }: { count: number }) {
  return (
    <Box role="status" aria-label="Загрузка контекста" sx={{ display: 'flex', gap: 1 }}>
      {Array.from({ length: count }, (_, index) => (
        <Skeleton
          key={index}
          variant="rounded"
          sx={{
            width: (theme) => theme.layout.contextSkeletonWidth,
            height: (theme) => theme.layout.contextSkeletonHeight,
          }}
        />
      ))}
    </Box>
  );
}

/**
 * A level that failed to load: the icon, the text (kept in the icon's
 * tooltip and visually hidden below 1024 px) and «Повторить» (mod-015 § 5.4).
 */
function FailedLevel({ text, onRetry }: { text: string; onRetry?: () => void }) {
  const widthClass = useWidthClass();
  return (
    <>
      <Tooltip title={text}>
        <ErrorOutlineOutlinedIcon color="error" fontSize="small" />
      </Tooltip>
      <Typography
        variant="body2"
        noWrap
        sx={widthClass === 'narrow' ? (theme) => theme.mixins.visuallyHidden : undefined}
      >
        {text}
      </Typography>
      {onRetry && (
        <Button size="small" variant="text" onClick={onRetry}>
          Повторить
        </Button>
      )}
    </>
  );
}

/**
 * The fallback notice (mod-015 § 5.4, mod-002 design § 3.6): a remembered or
 * chosen entry is gone and the switcher shows its replacement. The source
 * decides when; the switcher words it.
 */
function FallbackNotice() {
  const { fallback, dismissFallback } = useWorkContext();
  if (!fallback) {
    return null;
  }
  return (
    <Snackbar
      key={fallback.id}
      open
      autoHideDuration={NOTICE_DURATION_MS}
      onClose={() => dismissFallback?.()}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
    >
      <Alert severity="info" onClose={() => dismissFallback?.()}>
        {`Ранее выбранный контекст недоступен. Выбрано: ${fallback.names.join(' / ')}.`}
      </Alert>
    </Snackbar>
  );
}

/**
 * The context switcher (US-5; BR-11, BR-18; design § 5): organization,
 * project and team in the top bar, read from the shell's context source. The
 * placeholder source sends nothing and keeps nothing; mod-002 supplies the
 * real lists, a status and a retry per level, the organization list page and
 * the fallback notice (mod-002 D-n).
 */
export function ContextSwitcher() {
  return (
    <>
      <SwitcherLevels />
      <FallbackNotice />
    </>
  );
}

/**
 * The levels, drawn from the top and stopping at the first level that is not
 * ready: a loading level and every level below it are skeletons in one
 * `role="status"`, a failed level is the failed block with that level's text
 * (mod-015 § 5.4 as amended by mod-002 D-n).
 */
function SwitcherLevels() {
  const source = useWorkContext();

  if (source.status === 'loading') {
    return <LoadingLevels count={LEVEL_SKELETONS.organization} />;
  }

  if (source.status === 'failed') {
    return (
      <Box
        role="group"
        aria-label="Контекст работы"
        sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}
      >
        <FailedLevel
          text={FAILED_TEXT}
          onRetry={source.retry ? () => source.retry?.() : undefined}
        />
      </Box>
    );
  }

  const { organization, project, team } = resolveWorkContext(
    source.organizations,
    source.selection,
  );
  const listPath = source.organizationListPath;
  const projectStatus = source.levelStatus?.project ?? 'ready';
  const teamStatus = source.levelStatus?.team ?? 'ready';

  function lowerLevel(level: WorkContextLowerLevel, status: 'loading' | 'failed') {
    return status === 'loading' ? (
      <LoadingLevels count={LEVEL_SKELETONS[level]} />
    ) : (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
        <FailedLevel
          text={LEVEL_FAILED_TEXTS[level]}
          onRetry={source.retry ? () => source.retry?.(level) : undefined}
        />
      </Box>
    );
  }

  return (
    <Box
      role="group"
      aria-label="Контекст работы"
      sx={{ display: 'flex', alignItems: 'center', gap: 0.5, minWidth: 0 }}
    >
      {!organization ? (
        listPath ? (
          <Link component={RouterLink} to={listPath} variant="body2">
            Нет организаций
          </Link>
        ) : (
          <NothingToChoose>Нет организаций</NothingToChoose>
        )
      ) : (
        <>
          <ContextLevel
            level="organization"
            current={organization}
            options={source.organizations}
            onSelect={(id) => source.select('organization', id)}
            footer={
              listPath
                ? (close) => [
                    <Divider key="divider" />,
                    // A link, not a choice: it opens the list and closes the
                    // menu, the selection unchanged (mod-002 design § 3.3).
                    <MenuItem key="all" component={RouterLink} to={listPath} onClick={close}>
                      <ListItemIcon>
                        <ListAltOutlinedIcon fontSize="small" />
                      </ListItemIcon>
                      <ListItemText>Все организации</ListItemText>
                    </MenuItem>,
                  ]
                : undefined
            }
          />
          <Separator />
          {projectStatus !== 'ready' ? (
            lowerLevel('project', projectStatus)
          ) : !project ? (
            <NothingToChoose>Нет проектов</NothingToChoose>
          ) : (
            <>
              <ContextLevel
                level="project"
                current={project}
                options={organization.projects}
                onSelect={(id) => source.select('project', id)}
              />
              <Separator />
              {teamStatus !== 'ready' ? (
                lowerLevel('team', teamStatus)
              ) : !team ? (
                <NothingToChoose>Нет команд</NothingToChoose>
              ) : (
                <ContextLevel
                  level="team"
                  current={team}
                  options={project.teams}
                  onSelect={(id) => source.select('team', id)}
                />
              )}
            </>
          )}
        </>
      )}
    </Box>
  );
}
