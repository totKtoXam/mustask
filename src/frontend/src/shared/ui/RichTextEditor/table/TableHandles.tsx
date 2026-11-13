import { useEffect, useRef, useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import RemoveIcon from '@mui/icons-material/Remove';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import type { Editor } from '@tiptap/core';
import type { RichTextCopy } from '../copy';
import {
  HANDLE_DOT,
  HANDLE_HIT,
  measureHandles,
  runHandle,
  sameLayout,
  type HandleKind,
  type HandleLayout,
} from './handles';

export interface TableHandlesProps {
  editor: Editor;
  copy: RichTextCopy;
}

const DELETES: HandleKind[] = ['deleteRow', 'deleteColumn'];
const COLUMN_KINDS: HandleKind[] = ['columnLeft', 'columnRight', 'deleteColumn'];

/**
 * The handles on the active cell's row and column (design § Tables): «+» on the
 * boundaries above and below the row (the table's left edge) and left and right
 * of the column (its top edge), «−» for the row at the right edge and for the
 * column at the top. An overlay beside the editor, never a document node, so
 * neither the saved html nor a copy carries it. Pointer affordances: out of
 * the tab order, the toolbar's «…» menu is the keyboard path.
 */
export function TableHandles({ editor, copy }: TableHandlesProps) {
  const root = useRef<HTMLDivElement>(null);
  const [layout, setLayout] = useState<HandleLayout | null>(null);

  useEffect(() => {
    const element = root.current;
    if (!element) return undefined;
    const measure = () => {
      const next = measureHandles(editor, element);
      setLayout((previous) => (sameLayout(previous, next) ? previous : next));
    };
    // The editor's DOM is up to date when `transaction` fires; the frame after
    // catches a layout that settles later (fonts, a table's width on a resize).
    let frame = 0;
    const measureSoon = () => {
      measure();
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(measure);
    };
    const host = element.parentElement;
    editor.on('transaction', measureSoon);
    editor.on('focus', measureSoon);
    editor.on('blur', measureSoon);
    window.addEventListener('resize', measureSoon);
    // `scroll` does not bubble: the card's body and a table's own wrapper both scroll.
    host?.addEventListener('scroll', measureSoon, true);
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(measureSoon);
    observer?.observe(editor.view.dom);
    measure();
    return () => {
      editor.off('transaction', measureSoon);
      editor.off('focus', measureSoon);
      editor.off('blur', measureSoon);
      window.removeEventListener('resize', measureSoon);
      host?.removeEventListener('scroll', measureSoon, true);
      observer?.disconnect();
      cancelAnimationFrame(frame);
    };
  }, [editor]);

  return (
    <Box
      ref={root}
      data-testid="rt-table-handles"
      sx={{ position: 'absolute', inset: 0, pointerEvents: 'none', zIndex: 1 }}
    >
      {layout?.handles.map((spot) => {
        const label = copy.tableHandles[spot.kind];
        const remove = DELETES.includes(spot.kind);
        return (
          <Tooltip
            key={spot.kind}
            title={label}
            placement={COLUMN_KINDS.includes(spot.kind) ? 'top' : 'left'}
            disableInteractive
          >
            <IconButton
              aria-label={label}
              tabIndex={-1}
              disableRipple
              // The caret and the focus stay in the cell: the press must not blur the editor.
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => runHandle(editor, spot.kind)}
              sx={(theme) => ({
                position: 'absolute',
                left: spot.x - HANDLE_HIT / 2,
                top: spot.y - HANDLE_HIT / 2,
                width: HANDLE_HIT,
                height: HANDLE_HIT,
                padding: 0,
                pointerEvents: 'auto',
                color: theme.palette.text.secondary,
                '&:hover': { backgroundColor: 'transparent' },
                '& .rt-handle-dot': {
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: HANDLE_DOT,
                  height: HANDLE_DOT,
                  borderRadius: '50%',
                  boxSizing: 'border-box',
                  border: `1px solid ${theme.palette.shell.inputBorder}`,
                  backgroundColor: theme.palette.background.paper,
                },
                '&:hover .rt-handle-dot, &:focus-visible .rt-handle-dot': {
                  color: theme.palette.primary.contrastText,
                  borderColor: remove ? theme.palette.error.main : theme.palette.primary.main,
                  backgroundColor: remove ? theme.palette.error.main : theme.palette.primary.main,
                },
              })}
            >
              <span className="rt-handle-dot">
                {remove ? <RemoveIcon sx={{ fontSize: 12 }} /> : <AddIcon sx={{ fontSize: 12 }} />}
              </span>
            </IconButton>
          </Tooltip>
        );
      })}
    </Box>
  );
}
