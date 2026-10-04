import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Tooltip from '@mui/material/Tooltip';
import type { SvgIconComponent } from '@mui/icons-material';
import KeyboardDoubleArrowLeftIcon from '@mui/icons-material/KeyboardDoubleArrowLeft';
import KeyboardDoubleArrowRightIcon from '@mui/icons-material/KeyboardDoubleArrowRight';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import { useWorkContext } from '../../shared/lib/workContext';
import { SETTINGS_LABEL } from '../navigation/model';
import type { MainNavigationEntry } from '../navigation/types';
import { entryMarker, type EntryMarker, type VisibleNavigation } from '../navigation/visibility';
import { navigationEntryClass } from '../theme/classNames';

/** A rail entry: the icon centred in the rail, no label beside it. */
const railEntrySx = { justifyContent: 'center', px: 0, '& .MuiListItemIcon-root': { minWidth: 0 } };

function NavigationLink({
  label,
  icon: Icon,
  to,
  marker,
  rail,
  onChosen,
}: {
  label: string;
  icon: SvgIconComponent;
  to: string;
  marker: EntryMarker;
  rail: boolean;
  onChosen?: () => void;
}) {
  const link = (
    <ListItemButton
      component={RouterLink}
      to={to}
      selected={marker !== null}
      aria-current={marker ?? undefined}
      aria-label={rail ? label : undefined}
      className={navigationEntryClass}
      onClick={onChosen}
      sx={rail ? railEntrySx : undefined}
    >
      <ListItemIcon>
        <Icon fontSize="small" />
      </ListItemIcon>
      {!rail && <ListItemText primary={label} />}
    </ListItemButton>
  );
  // On the rail the label is the accessible name and a tooltip on hover and
  // on keyboard focus (US-4, US-12).
  return rail ? (
    <Tooltip title={label} placement="right">
      {link}
    </Tooltip>
  ) : (
    link
  );
}

function entryLink(
  entry: MainNavigationEntry,
  currentPage: string | null,
  selection: ReturnType<typeof useWorkContext>['selection'],
  rail: boolean,
  onChosen?: () => void,
) {
  const landing = entry.landing(selection);
  if (!landing) {
    return null;
  }
  return (
    <NavigationLink
      key={entry.id}
      label={entry.label}
      icon={entry.icon}
      to={landing}
      marker={entryMarker(currentPage, entry, selection)}
      rail={rail}
      onChosen={onChosen}
    />
  );
}

export interface MainNavigationProps {
  /** The element id the collapse toggle and «Меню» point at. */
  id: string;
  variant: 'expanded' | 'rail';
  visible: VisibleNavigation;
  /** The address of the page on screen; `null` while a not-found frame answers it (BR-13). */
  currentPage: string | null;
  /** On a settings page «Настройки» is the current entry, `aria-current="true"` (US-3). */
  settingsCurrent: boolean;
  /** The collapse toggle: hidden on settings pages and in the overlay (BR-10, BR-19). */
  toggle?: { onToggle: () => void };
  /** Called when an entry is chosen: the overlay closes (BR-10). */
  onEntryChosen?: () => void;
}

/**
 * «Основное меню» (US-3, US-4; design § 4.3): «Главная», the work group,
 * «Настройки» at the bottom and the collapse toggle below it. Expanded it
 * shows labels; as a rail, icons named by their labels.
 */
export function MainNavigation({
  id,
  variant,
  visible,
  currentPage,
  settingsCurrent,
  toggle,
  onEntryChosen,
}: MainNavigationProps) {
  const { selection } = useWorkContext();
  const rail = variant === 'rail';
  const toggleLabel = rail ? 'Развернуть меню' : 'Свернуть меню';

  const toggleButton = toggle && (
    <ListItemButton
      onClick={toggle.onToggle}
      aria-expanded={rail ? 'false' : 'true'}
      aria-controls={id}
      aria-label={rail ? toggleLabel : undefined}
      className={navigationEntryClass}
      sx={rail ? railEntrySx : undefined}
    >
      <ListItemIcon>
        {rail ? (
          <KeyboardDoubleArrowRightIcon fontSize="small" />
        ) : (
          <KeyboardDoubleArrowLeftIcon fontSize="small" />
        )}
      </ListItemIcon>
      {!rail && <ListItemText primary={toggleLabel} />}
    </ListItemButton>
  );

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', overflowX: 'hidden' }}>
      <Box
        component="nav"
        id={id}
        aria-label="Основное меню"
        sx={{ flex: 1, display: 'flex', flexDirection: 'column', overflowY: 'auto' }}
      >
        <List>
          {visible.top.map((entry) =>
            entryLink(entry, currentPage, selection, rail, onEntryChosen),
          )}
        </List>
        {visible.work.length > 0 && (
          <>
            <Divider />
            <List>
              {visible.work.map((entry) =>
                entryLink(entry, currentPage, selection, rail, onEntryChosen),
              )}
            </List>
          </>
        )}
        {visible.settingsLanding !== null && (
          <List sx={{ mt: 'auto' }}>
            <NavigationLink
              label={SETTINGS_LABEL}
              icon={SettingsOutlinedIcon}
              to={visible.settingsLanding}
              marker={settingsCurrent ? 'true' : null}
              rail={rail}
              onChosen={onEntryChosen}
            />
          </List>
        )}
      </Box>
      {toggleButton && (
        <List disablePadding>
          {rail ? (
            <Tooltip title={toggleLabel} placement="right">
              {toggleButton}
            </Tooltip>
          ) : (
            toggleButton
          )}
        </List>
      )}
    </Box>
  );
}
