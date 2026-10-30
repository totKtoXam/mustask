import {
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactElement,
  type ReactNode,
  type Ref,
} from 'react';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Link from '@mui/material/Link';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Tooltip from '@mui/material/Tooltip';
import { Link as RouterLink } from 'react-router-dom';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import MoreHorizIcon from '@mui/icons-material/MoreHoriz';
import { ErrorBanner } from './ErrorBanner';
import { RegistrySkeleton } from './RegistrySkeleton';

/** One node of the tree: its id, its parent's (`null` at the top) and its label. */
export interface TreeRegistryNode {
  id: string;
  parentId: string | null;
  label: string;
}

/** One entry of a row's action menu. */
export interface TreeRegistryAction {
  label: string;
  icon: ReactElement;
  onClick(): void;
  /** Cannot be undone: the entry takes the error colour (mod-015 § 10.3). */
  destructive?: boolean;
}

/** A column after the first, which holds the label. */
export interface TreeRegistryColumn<N> {
  header: string;
  render(node: N): ReactNode;
}

/** What the caller holds on the tree, for its focus rules after a change. */
export interface TreeRegistryHandle {
  /** Focuses the row once it is rendered (now, or after the next read). */
  focusRow(id: string): void;
}

export interface TreeRegistryProps<N extends TreeRegistryNode> {
  'aria-label': string;
  /** The first column's header. */
  firstColumn: string;
  columns: readonly TreeRegistryColumn<N>[];
  /** Every node; siblings are shown in the order given. */
  nodes: readonly N[];
  /** Shown after the label, e.g. a chip. */
  adornment?(node: N): ReactNode;
  /**
   * An address the label leads to (mod-018 design D-7): the label becomes a router `Link` outside
   * the Tab order (the row is the tab stop), Enter on a row follows it and Space selects. Absent:
   * nothing changes.
   */
  labelTo?(node: N): string;
  actions(node: N): readonly TreeRegistryAction[];
  /**
   * Whether the actions column — its header cell, a cell and a «Действия»
   * button per row — is drawn. Absent: the table draws it when some node has
   * an action, so a user with no write code gets no column of dead buttons
   * (mod-016 DC-2), and the loading skeleton keeps its blank column. `false`
   * leaves the column out of the skeleton too, for a user who will not get it.
   */
  showActions?: boolean;
  selectedId: string | null;
  onSelect(id: string): void;
  /** The expanded set is the caller's: it decides what starts expanded and what a change expands. */
  expanded: ReadonlySet<string>;
  onToggle(id: string): void;
  loading?: boolean;
  /** A failed read: the banner with «Повторить» replaces the tree. */
  error?: unknown;
  onRetry?(): void;
  ref?: Ref<TreeRegistryHandle>;
}

interface VisibleRow<N> {
  node: N;
  /** 1 at the top (`aria-level`). */
  level: number;
  setSize: number;
  position: number;
  hasChildren: boolean;
  isExpanded: boolean;
}

/** The rows shown: the top nodes, then the children of every expanded node, depth first. */
function visibleRows<N extends TreeRegistryNode>(
  nodes: readonly N[],
  expanded: ReadonlySet<string>,
): VisibleRow<N>[] {
  const ids = new Set(nodes.map((node) => node.id));
  const childrenOf = new Map<string | null, N[]>();
  for (const node of nodes) {
    const parent = node.parentId !== null && ids.has(node.parentId) ? node.parentId : null;
    const siblings = childrenOf.get(parent) ?? [];
    siblings.push(node);
    childrenOf.set(parent, siblings);
  }
  const rows: VisibleRow<N>[] = [];
  const visit = (parent: string | null, level: number) => {
    const siblings = childrenOf.get(parent) ?? [];
    siblings.forEach((node, index) => {
      const hasChildren = (childrenOf.get(node.id)?.length ?? 0) > 0;
      const isExpanded = hasChildren && expanded.has(node.id);
      rows.push({
        node,
        level,
        setSize: siblings.length,
        position: index + 1,
        hasChildren,
        isExpanded,
      });
      if (isExpanded) {
        visit(node.id, level + 1);
      }
    });
  };
  visit(null, 1);
  return rows;
}

/**
 * The tree registry (mod-015 § 10.7, mod-002 design § 11.2, § 11.6): a
 * WAI-ARIA treegrid with row focus. It knows nodes, labels, columns and
 * actions and nothing of what they are, so any hierarchy reuses it. The
 * caller owns the selection and the expanded set; the pattern owns the
 * markup, the roving `tabIndex` and the keyboard model:
 *
 * - Tab enters on the selected row, else the first; from a row, its
 *   «Действия»; then out of the tree.
 * - ArrowDown / ArrowUp, Home / End move between visible rows.
 * - ArrowRight expands, then goes to the first child; ArrowLeft collapses,
 *   then goes to the parent.
 * - Enter and Space select (with `labelTo`, Enter follows the label's link); Shift+F10 and ContextMenu open the row's menu;
 *   Escape in the menu returns to «Действия».
 * - A click on a row selects and focuses it.
 */
export function TreeRegistry<N extends TreeRegistryNode>({
  'aria-label': ariaLabel,
  firstColumn,
  columns,
  nodes,
  adornment,
  labelTo,
  actions,
  showActions,
  selectedId,
  onSelect,
  expanded,
  onToggle,
  loading = false,
  error,
  onRetry,
  ref,
}: TreeRegistryProps<N>) {
  const rows = useMemo(() => visibleRows(nodes, expanded), [nodes, expanded]);
  const hasActions = showActions ?? nodes.some((node) => actions(node).length > 0);
  const [focusedId, setFocusedId] = useState<string | null>(null);
  const rowElements = useRef(new Map<string, HTMLTableRowElement>());
  const actionButtons = useRef(new Map<string, HTMLButtonElement>());
  const pendingFocus = useRef<string | null>(null);
  const [menu, setMenu] = useState<{ id: string; anchor: HTMLElement } | null>(null);

  const visibleIds = rows.map((row) => row.node.id);
  const tabbableId =
    (focusedId !== null && visibleIds.includes(focusedId) && focusedId) ||
    (selectedId !== null && visibleIds.includes(selectedId) && selectedId) ||
    visibleIds[0] ||
    null;

  const focusNow = useCallback((id: string) => {
    setFocusedId(id);
    rowElements.current.get(id)?.focus();
  }, []);

  useImperativeHandle(
    ref,
    () => ({
      focusRow(id: string) {
        pendingFocus.current = id;
        setFocusedId(id);
        const element = rowElements.current.get(id);
        if (element) {
          pendingFocus.current = null;
          element.focus();
        }
      },
    }),
    [],
  );

  // A row asked for before it was rendered (a create or a move, then the
  // tree's next read) takes focus once it is there.
  useEffect(() => {
    const id = pendingFocus.current;
    const element = id !== null ? rowElements.current.get(id) : undefined;
    if (element) {
      pendingFocus.current = null;
      element.focus();
    }
  });

  if (loading) {
    return (
      <RegistrySkeleton
        columns={[
          firstColumn,
          ...columns.map((column) => column.header),
          ...(showActions === false ? [] : ['']),
        ]}
      />
    );
  }
  if (error !== undefined && error !== null) {
    return <ErrorBanner error={error} onRetry={onRetry} />;
  }

  function openMenu(id: string) {
    const button = actionButtons.current.get(id);
    if (button) {
      button.focus();
      setMenu({ id, anchor: button });
    }
  }

  function handleRowKey(event: KeyboardEvent<HTMLTableRowElement>, index: number) {
    if (event.target !== event.currentTarget) {
      return;
    }
    const row = rows[index];
    if (!row) {
      return;
    }
    const move = (target: VisibleRow<N> | undefined) => {
      event.preventDefault();
      if (target) {
        focusNow(target.node.id);
      }
    };
    switch (event.key) {
      case 'ArrowDown':
        move(rows[index + 1]);
        return;
      case 'ArrowUp':
        move(rows[index - 1]);
        return;
      case 'Home':
        move(rows[0]);
        return;
      case 'End':
        move(rows[rows.length - 1]);
        return;
      case 'ArrowRight':
        event.preventDefault();
        if (row.hasChildren && !row.isExpanded) {
          onToggle(row.node.id);
        } else if (row.isExpanded) {
          move(rows[index + 1]);
        }
        return;
      case 'ArrowLeft':
        event.preventDefault();
        if (row.isExpanded) {
          onToggle(row.node.id);
        } else {
          move(rows.find((candidate) => candidate.node.id === row.node.parentId));
        }
        return;
      case 'Enter':
        event.preventDefault();
        if (labelTo) {
          event.currentTarget.querySelector<HTMLAnchorElement>('a[data-tree-label]')?.click();
        } else {
          onSelect(row.node.id);
        }
        return;
      case ' ':
        event.preventDefault();
        onSelect(row.node.id);
        return;
      case 'ContextMenu':
        event.preventDefault();
        openMenu(row.node.id);
        return;
      case 'F10':
        if (event.shiftKey) {
          event.preventDefault();
          openMenu(row.node.id);
        }
        return;
      default:
        return;
    }
  }

  const menuNode = menu ? nodes.find((node) => node.id === menu.id) : undefined;
  const menuActions = menuNode ? actions(menuNode) : [];

  return (
    <>
      <TableContainer component={Paper} variant="outlined">
        <Table size="small" role="treegrid" aria-label={ariaLabel}>
          <TableHead>
            <TableRow role="row">
              <TableCell role="columnheader">{firstColumn}</TableCell>
              {columns.map((column) => (
                <TableCell key={column.header} role="columnheader">
                  {column.header}
                </TableCell>
              ))}
              {hasActions && <TableCell role="columnheader" />}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row, index) => {
              const { node } = row;
              const selected = node.id === selectedId;
              const tabbable = node.id === tabbableId;
              return (
                <TableRow
                  key={node.id}
                  role="row"
                  hover
                  ref={(element: HTMLTableRowElement | null) => {
                    if (element) {
                      rowElements.current.set(node.id, element);
                    } else {
                      rowElements.current.delete(node.id);
                    }
                  }}
                  tabIndex={tabbable ? 0 : -1}
                  aria-level={row.level}
                  aria-setsize={row.setSize}
                  aria-posinset={row.position}
                  aria-selected={selected}
                  aria-expanded={row.hasChildren ? row.isExpanded : undefined}
                  onFocus={(event) => {
                    if (event.target === event.currentTarget) {
                      setFocusedId(node.id);
                    }
                  }}
                  onKeyDown={(event) => handleRowKey(event, index)}
                  onClick={() => {
                    onSelect(node.id);
                    focusNow(node.id);
                  }}
                  sx={{
                    cursor: 'pointer',
                    ...(selected && {
                      backgroundColor: (theme) => theme.palette.shell.rowSelected,
                      '&.MuiTableRow-hover:hover': {
                        backgroundColor: (theme) => theme.palette.shell.rowSelected,
                      },
                    }),
                    // The accent focus ring (2 px, BR-5), drawn inside the
                    // row so the table's scrolling container never clips it.
                    '&:focus-visible': {
                      outline: (theme) =>
                        `${theme.spacing(0.25)} solid ${theme.palette.primary.main}`,
                      outlineOffset: (theme) => `-${theme.spacing(0.25)}`,
                    },
                  }}
                >
                  <TableCell role="gridcell" sx={{ maxWidth: 0, width: '50%' }}>
                    <Box
                      sx={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: 0.5,
                        pl: (theme) => theme.spacing(3 * (row.level - 1)),
                        minWidth: 0,
                      }}
                    >
                      {row.hasChildren ? (
                        <IconButton
                          size="small"
                          tabIndex={-1}
                          aria-label={`${row.isExpanded ? 'Свернуть' : 'Развернуть'} «${node.label}»`}
                          onClick={(event) => {
                            event.stopPropagation();
                            onToggle(node.id);
                            focusNow(node.id);
                          }}
                          sx={{
                            width: (theme) => theme.spacing(3.5),
                            height: (theme) => theme.spacing(3.5),
                          }}
                        >
                          {row.isExpanded ? (
                            <ExpandMoreIcon fontSize="small" />
                          ) : (
                            <ChevronRightIcon fontSize="small" />
                          )}
                        </IconButton>
                      ) : (
                        <Box
                          aria-hidden
                          sx={{ width: (theme) => theme.spacing(3.5), flexShrink: 0 }}
                        />
                      )}
                      <Tooltip title={node.label}>
                        <Box
                          component="span"
                          sx={{
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                            minWidth: 0,
                          }}
                        >
                          {labelTo ? (
                            <Link
                              component={RouterLink}
                              to={labelTo(node)}
                              tabIndex={-1}
                              data-tree-label=""
                              color="inherit"
                              underline="hover"
                              onClick={(event) => event.stopPropagation()}
                            >
                              {node.label}
                            </Link>
                          ) : (
                            node.label
                          )}
                        </Box>
                      </Tooltip>
                      {adornment?.(node)}
                    </Box>
                  </TableCell>
                  {columns.map((column) => (
                    <TableCell key={column.header} role="gridcell">
                      {column.render(node)}
                    </TableCell>
                  ))}
                  {hasActions && (
                    <TableCell
                      role="gridcell"
                      align="right"
                      sx={{ width: (theme) => theme.spacing(6) }}
                    >
                      {actions(node).length > 0 && (
                        <IconButton
                          size="small"
                          ref={(element: HTMLButtonElement | null) => {
                            if (element) {
                              actionButtons.current.set(node.id, element);
                            } else {
                              actionButtons.current.delete(node.id);
                            }
                          }}
                          tabIndex={tabbable ? 0 : -1}
                          aria-label={`Действия: ${node.label}`}
                          aria-haspopup="menu"
                          aria-expanded={menu?.id === node.id ? 'true' : 'false'}
                          onClick={(event) => {
                            event.stopPropagation();
                            setFocusedId(node.id);
                            setMenu({ id: node.id, anchor: event.currentTarget });
                          }}
                          onKeyDown={(event) => event.stopPropagation()}
                        >
                          <MoreHorizIcon fontSize="small" />
                        </IconButton>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
      <Menu
        anchorEl={menu?.anchor ?? null}
        open={menu !== null && menuActions.length > 0}
        onClose={() => setMenu(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {menuActions.map((action) => (
          <MenuItem
            key={action.label}
            onClick={() => {
              setMenu(null);
              action.onClick();
            }}
            sx={action.destructive ? { color: 'error.main' } : undefined}
          >
            <ListItemIcon sx={action.destructive ? { color: 'error.main' } : undefined}>
              {action.icon}
            </ListItemIcon>
            <ListItemText>{action.label}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}
