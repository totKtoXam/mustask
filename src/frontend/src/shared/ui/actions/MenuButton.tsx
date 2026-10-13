import {
  useId,
  useState,
  type KeyboardEvent,
  type MouseEvent,
  type ReactElement,
  type RefCallback,
} from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Divider from '@mui/material/Divider';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import ListSubheader from '@mui/material/ListSubheader';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import CheckOutlinedIcon from '@mui/icons-material/CheckOutlined';

/** A choice in a menu. */
export interface MenuChoice {
  label: string;
  /** A Material `*Outlined` icon or a thin one; drawn neutral (`text.secondary`). */
  icon?: ReactElement;
  onSelect?: () => void;
  /** The entry is a link. */
  to?: string;
  /** Cannot be undone: the text and the icon read in the error colour — and the label says what it does. */
  destructive?: boolean;
  disabled?: boolean;
  /** The chosen one of a `radio` or `page` menu; the menu opens with focus on it. */
  current?: boolean;
}

/** One entry of a menu: a choice, a group with its heading, or a rule. */
export type MenuEntry = MenuChoice | { group: string; items: readonly MenuEntry[] } | 'divider';

/** What a trigger — an `ActionButton` or an `IconAction` — takes, spread on it. */
export interface MenuTriggerProps {
  ref: RefCallback<HTMLElement>;
  onClick: (event: MouseEvent<HTMLElement>) => void;
  onKeyDown: (event: KeyboardEvent<HTMLElement>) => void;
  'aria-haspopup': 'menu';
  'aria-expanded': boolean;
  'aria-controls': string | undefined;
}

export interface MenuButtonProps {
  /** The menu's name (the trigger is named by its host). Never «Меню». */
  label: string;
  trigger: (props: MenuTriggerProps) => ReactElement;
  items: readonly MenuEntry[];
  /** `end`: the menu's right edge on the trigger's. Default `start`. */
  align?: 'start' | 'end';
  /**
   * `none`: plain `menuitem`s (default). `radio`: `menuitemradio` with a check mark
   * on the current one. `page`: the current entry is `selected` and
   * `aria-current="page"` — a team or level drop-down.
   */
  selection?: 'none' | 'radio' | 'page';
  /** The host moves focus itself after a choice. */
  disableRestoreFocus?: boolean;
}

/**
 * A button that opens a list of choices (tech-015 design § 5.3, mod-015 § 12.4):
 * the one menu of the panel. It owns the anchor, the keys, the attributes, the
 * name and the focus return — Enter, Space or ArrowDown on the trigger opens it
 * on the current entry (or the first); the arrows, Home/End and typing move;
 * Enter chooses; Escape closes and gives focus back to the trigger.
 */
export function MenuButton({
  label,
  trigger,
  items,
  align = 'start',
  selection = 'none',
  disableRestoreFocus = false,
}: MenuButtonProps) {
  // The trigger's element, by callback ref: the menu anchors on it.
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [open, setOpen] = useState(false);
  const menuId = useId();
  const headingBase = useId();
  const edge = align === 'end' ? 'right' : 'left';
  const shown = open && anchor !== null;

  return (
    <>
      {trigger({
        ref: setAnchor,
        onClick: () => setOpen(true),
        onKeyDown: (event) => {
          if (event.key === 'ArrowDown') {
            event.preventDefault();
            setOpen(true);
          }
        },
        'aria-haspopup': 'menu',
        'aria-expanded': shown,
        'aria-controls': shown ? menuId : undefined,
      })}
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={shown}
        onClose={() => setOpen(false)}
        variant="selectedMenu"
        anchorOrigin={{ vertical: 'bottom', horizontal: edge }}
        transformOrigin={{ vertical: 'top', horizontal: edge }}
        disableRestoreFocus={disableRestoreFocus}
        slotProps={{ list: { 'aria-label': label } }}
      >
        {renderEntries(items, {
          selection,
          headingBase,
          choose: (choice) => {
            setOpen(false);
            choice.onSelect?.();
          },
        })}
      </Menu>
    </>
  );
}

interface RenderContext {
  selection: 'none' | 'radio' | 'page';
  headingBase: string;
  choose: (choice: MenuChoice) => void;
}

/** Flat on purpose: MUI's `MenuList` walks its direct children and warns on a fragment. */
function renderEntries(
  entries: readonly MenuEntry[],
  context: RenderContext,
  describedBy?: string,
  path = '',
): ReactElement[] {
  return entries.flatMap((entry, index): ReactElement[] => {
    const key = `${path}${index}`;
    if (entry === 'divider') return [<Divider key={key} component="li" />];
    if ('group' in entry) {
      const headingId = `${context.headingBase}-${key}`;
      return [
        // A screen reader skips the heading while the arrows walk the items, so each
        // entry under it names it as its description (feat-027 F-12).
        <ListSubheader key={key} id={headingId} role="presentation">
          {entry.group}
        </ListSubheader>,
        ...renderEntries(entry.items, context, headingId, `${key}-`),
      ];
    }
    return [<ChoiceItem key={key} choice={entry} context={context} describedBy={describedBy} />];
  });
}

function ChoiceItem({
  choice,
  context,
  describedBy,
}: {
  choice: MenuChoice;
  context: RenderContext;
  describedBy: string | undefined;
}) {
  const { selection } = context;
  const current = choice.current === true && selection !== 'none';
  const colour = choice.destructive ? 'error.main' : undefined;
  const content = (
    <>
      {choice.icon && (
        <ListItemIcon sx={{ color: colour ?? 'text.secondary' }}>{choice.icon}</ListItemIcon>
      )}
      <ListItemText>{choice.label}</ListItemText>
      {selection === 'radio' && current && (
        <CheckOutlinedIcon fontSize="small" aria-hidden="true" sx={{ ml: 2 }} />
      )}
    </>
  );
  const common = {
    role: selection === 'radio' ? 'menuitemradio' : undefined,
    'aria-checked': selection === 'radio' ? current : undefined,
    'aria-current': selection === 'page' && current ? ('page' as const) : undefined,
    'aria-describedby': describedBy,
    selected: current,
    disabled: choice.disabled,
    onClick: () => context.choose(choice),
    sx: colour ? { color: colour } : undefined,
  };
  return choice.to ? (
    <MenuItem component={RouterLink} to={choice.to} {...common}>
      {content}
    </MenuItem>
  ) : (
    <MenuItem {...common}>{content}</MenuItem>
  );
}
