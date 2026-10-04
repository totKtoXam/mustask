import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemText from '@mui/material/ListItemText';
import ListSubheader from '@mui/material/ListSubheader';
import Typography from '@mui/material/Typography';
import { useWorkContext } from '../../shared/lib/workContext';
import { SETTINGS_LABEL } from '../navigation/model';
import { entryMarker, type VisibleNavigation } from '../navigation/visibility';
import { navigationEntryClass } from '../theme/classNames';

/**
 * «Меню настроек» (US-3, BR-19; design § 4.4): titled «Настройки» — text, not
 * a heading, so the page's `h1` stays the first heading — with its sections
 * and the visible entries under them.
 */
export function SettingsNavigation({
  visible,
  currentPage,
  onEntryChosen,
}: {
  visible: VisibleNavigation;
  /** The address of the page on screen; `null` while a not-found frame answers it (BR-13). */
  currentPage: string | null;
  onEntryChosen?: () => void;
}) {
  const { selection } = useWorkContext();

  return (
    <Box
      component="nav"
      aria-label="Меню настроек"
      sx={{ height: '100%', overflowY: 'auto', py: 1 }}
    >
      <Typography variant="h2" component="p" sx={{ px: 2, py: 1 }}>
        {SETTINGS_LABEL}
      </Typography>
      {visible.settings.map(({ section, entries }) => (
        <List
          key={section.id}
          subheader={
            <ListSubheader
              sx={{
                bgcolor: 'shell.navigation',
                color: 'text.primary',
                typography: 'body1',
                fontWeight: 'fontWeightMedium',
                lineHeight: (theme) => theme.spacing(5),
              }}
            >
              {section.label}
            </ListSubheader>
          }
        >
          {entries.map((entry) => {
            const landing = entry.landing(selection);
            if (!landing) {
              return null;
            }
            const marker = entryMarker(currentPage, entry, selection);
            return (
              <ListItemButton
                key={entry.id}
                component={RouterLink}
                to={landing}
                selected={marker !== null}
                aria-current={marker ?? undefined}
                className={navigationEntryClass}
                onClick={onEntryChosen}
                sx={{ pl: 4 }}
              >
                <ListItemText primary={entry.label} />
              </ListItemButton>
            );
          })}
        </List>
      ))}
    </Box>
  );
}
