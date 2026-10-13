import type { ReactElement, ReactNode } from 'react';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Tab, { type TabProps } from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Tooltip from '@mui/material/Tooltip';

export interface BoxedTab {
  value: string;
  /** The name; drawn unless `iconOnly`. */
  label: string;
  icon?: ReactElement;
  /** A grey pill after the label; the tab is named «Связи (1)». */
  count?: number;
  /** The label is the name and the tooltip; only the icon is drawn. */
  iconOnly?: boolean;
}

export interface BoxedTabsProps {
  /** From `useId()`: links the tabs and their panels. */
  idBase: string;
  /** The tablist's name, e.g. «Разделы рабочего элемента». */
  'aria-label': string;
  value: string;
  onChange: (value: string) => void;
  /** One tab is a valid strip. */
  tabs: BoxedTab[];
}

const tabId = (idBase: string, value: string) => `${idBase}-tab-${value}`;
const panelId = (idBase: string, value: string) => `${idBase}-panel-${value}`;

/** A tab's accessible name: its label, with « (<count>)» so the pill's bare digit is never the name. */
function tabName(tab: BoxedTab): string {
  return tab.count === undefined ? tab.label : `${tab.label} (${tab.count})`;
}

/**
 * What a tab draws: its icon, its label, the count pill — and, on the selected one, the
 * indicator. The indicator is a line of `layout.frame.thick` standing
 * `layout.card.tabIndicatorOffset` above the strip's bottom and as wide as this content,
 * the label, not the tab: the reference's underline sits directly under the label.
 */
function TabContent({ tab, selected }: { tab: BoxedTab; selected: boolean }): ReactNode {
  return (
    <Box
      component="span"
      data-tab-label=""
      sx={(theme) => ({
        position: 'relative',
        display: 'inline-flex',
        alignItems: 'center',
        // MUI's tab is a column: the content fills its height, so the indicator's
        // `bottom` is measured from the strip's bottom edge.
        flexGrow: 1,
        gap: 0.75,
        minWidth: 0,
        '& .MuiSvgIcon-root': { fontSize: theme.layout.iconAction.bar.glyph },
      })}
    >
      {tab.icon}
      {!tab.iconOnly && <span>{tab.label}</span>}
      {tab.count !== undefined && (
        <Chip
          component="span"
          label={tab.count}
          aria-hidden="true"
          data-tab-count=""
          sx={(theme) => ({
            height: theme.spacing(2.25),
            borderRadius: theme.spacing(1.125),
            backgroundColor: theme.palette.shell.buttonNeutral,
            color: theme.palette.text.primary,
            ...theme.typography.caption,
            '& .MuiChip-label': { padding: theme.spacing(0, 0.75) },
          })}
        />
      )}
      {selected && (
        <Box
          component="span"
          aria-hidden="true"
          data-tab-indicator=""
          sx={(theme) => ({
            position: 'absolute',
            left: 0,
            right: 0,
            bottom: theme.layout.card.tabIndicatorOffset,
            height: theme.layout.frame.thick,
            backgroundColor: theme.palette.primary.main,
          })}
        />
      )}
    </Box>
  );
}

/**
 * MUI's `Tabs` clones each child with `selected`, `onChange`, `value` and the rest, so
 * the item takes them as a `Tab` does and hands them on. It is a component of its own so
 * an icon-only tab can stand inside its `Tooltip` without hiding from `Tabs`.
 */
function BoxedTabItem({
  tab,
  idBase,
  ...tabProps
}: Omit<TabProps, 'label' | 'icon'> & {
  tab: BoxedTab;
  idBase: string;
  /** Handed down by `Tabs`. */
  selected?: boolean;
}) {
  const item = (
    <Tab
      {...tabProps}
      id={tabId(idBase, tab.value)}
      aria-controls={panelId(idBase, tab.value)}
      aria-label={tabName(tab)}
      label={<TabContent tab={tab} selected={tabProps.selected === true} />}
      sx={(theme) => ({
        // MUI's 90 px width floor and the theme's 40 px tab would widen a one-tab strip.
        minWidth: 0,
        minHeight: theme.layout.card.tabStripHeight,
        boxSizing: 'border-box',
        paddingLeft: `${theme.layout.card.tabPadX}px`,
        paddingRight: `${theme.layout.card.tabPadX}px`,
        paddingTop: 0,
        paddingBottom: 0,
        // The reference's lines: on the top and the sides of every tab, none at the foot.
        // `divider` is a shade darker than the reference's (a recorded difference).
        borderTop: `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`,
        borderLeft: `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`,
        '&:last-of-type': {
          borderRight: `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`,
        },
      })}
    />
  );
  return tab.iconOnly ? <Tooltip title={tab.label}>{item}</Tooltip> : item;
}

/**
 * The sections of a card — «Детали», «История», «Связи», «Вложения» — as a strip of boxed
 * tabs at the right end of the band (tech-015 design § 5.6). MUI's `Tabs`, so the keys are
 * MUI's: Left and Right move focus, Home and End jump, Enter or Space selects. Not for
 * page views: those are addresses (`PageHeader views`).
 *
 * Its own indicator replaces MUI's, which is `bottom: 0` and tab-wide (see `TabContent`).
 */
export function BoxedTabs({
  idBase,
  'aria-label': ariaLabel,
  value,
  onChange,
  tabs,
}: BoxedTabsProps) {
  return (
    <Tabs
      value={value}
      aria-label={ariaLabel}
      onChange={(_event, next: string) => onChange(next)}
      slotProps={{ indicator: { sx: { display: 'none' } } }}
      sx={(theme) => ({ minHeight: theme.layout.card.tabStripHeight })}
    >
      {tabs.map((tab) => (
        <BoxedTabItem key={tab.value} tab={tab} idBase={idBase} value={tab.value} />
      ))}
    </Tabs>
  );
}

export interface BoxedTabPanelProps {
  idBase: string;
  /** The value of the tab this panel belongs to. */
  value: string;
  /** Only the selected tab's panel is rendered. */
  selected: boolean;
  children: ReactNode;
}

/** A tab's panel: `role="tabpanel"` named by its tab. Nothing is rendered for a tab that is not selected. */
export function BoxedTabPanel({ idBase, value, selected, children }: BoxedTabPanelProps) {
  if (!selected) return null;
  return (
    <Box role="tabpanel" id={panelId(idBase, value)} aria-labelledby={tabId(idBase, value)}>
      {children}
    </Box>
  );
}
