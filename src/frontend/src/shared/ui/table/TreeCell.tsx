import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import { IconAction } from '../actions/IconAction';
import { ThinChevronDownIcon, ThinChevronRightIcon } from '../icons/ThinIcons';
import { TruncatedText } from '../text/TruncatedText';
import { Cell, type CellAttributes } from './Cell';

interface TreeCellBase extends Omit<CellAttributes, 'role'> {
  /** The row's depth: 0 at the top. Each level is `layout.table.treeIndent`. */
  depth: number;
  /** A mark before the title — a compact `TypeLabel`'s icon. */
  icon?: ReactNode;
  /** A 24 px slot at the right edge — the row's «⋮». Reserved even when empty, so a long title never runs under a control. */
  trailing?: ReactNode;
  /** The title: a string is cut to one line with an ellipsis; an element stands as given. */
  children: ReactNode;
  role?: CellAttributes['role'];
}

export type TreeCellProps = TreeCellBase &
  (
    | {
        expandable: true;
        expanded?: boolean;
        onToggle: () => void;
        /** The chevron's name, the host's — «Развернуть MSK-EPIC-1» or «Свернуть MSK-EPIC-1», by the state. */
        toggleLabel: string;
        /** A treegrid passes -1: its row is the stop, the chevron is out of the Tab order. */
        toggleTabIndex?: number;
      }
    | {
        expandable?: false;
        expanded?: never;
        onToggle?: never;
        toggleLabel?: never;
        toggleTabIndex?: never;
      }
  );

/**
 * The title cell of a tree (tech-015 design § 5.11): the depth's indent, a chevron slot of
 * `layout.iconAction.chevron.box`, the type's mark, the title, and a slot at the right
 * edge. A leaf keeps an empty `aria-hidden` box where the chevron would stand, so the
 * icons of one depth line up.
 *
 * The chevron is an `IconAction` — `aria-expanded`, no tooltip (its row names it) — and
 * its `tabIndex` is the host's. Row and cell attributes pass through to the `td`.
 */
export function TreeCell({
  depth,
  expandable,
  expanded = false,
  onToggle,
  toggleLabel,
  toggleTabIndex,
  icon,
  trailing,
  children,
  ...attributes
}: TreeCellProps) {
  return (
    <Cell padding="tree" {...attributes}>
      <Box
        sx={(theme) => ({
          display: 'flex',
          alignItems: 'center',
          minWidth: 0,
          paddingLeft: `${depth * theme.layout.table.treeIndent}px`,
        })}
      >
        {expandable ? (
          <IconAction
            size="chevron"
            tooltip={false}
            label={toggleLabel as string}
            icon={expanded ? <ThinChevronDownIcon /> : <ThinChevronRightIcon />}
            aria-expanded={expanded ? 'true' : 'false'}
            tabIndex={toggleTabIndex}
            onClick={onToggle}
          />
        ) : (
          <Box
            aria-hidden="true"
            data-tree-leaf=""
            sx={(theme) => ({
              width: theme.layout.iconAction.chevron.box,
              height: theme.layout.iconAction.chevron.box,
              flexShrink: 0,
            })}
          />
        )}
        {icon && (
          <Box
            component="span"
            data-tree-icon=""
            sx={{ display: 'inline-flex', flexShrink: 0, marginLeft: 0.5, marginRight: 0.5 }}
          >
            {icon}
          </Box>
        )}
        <Box sx={{ flex: '0 1 auto', minWidth: 0, display: 'flex' }}>
          {typeof children === 'string' ? <TruncatedText>{children}</TruncatedText> : children}
        </Box>
        <Box sx={{ flex: '1 1 0', minWidth: 0 }} />
        <Box
          data-tree-trailing=""
          sx={(theme) => ({
            flexShrink: 0,
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: theme.layout.iconAction.row.box,
            height: theme.layout.iconAction.row.box,
          })}
        >
          {trailing}
        </Box>
      </Box>
    </Cell>
  );
}
