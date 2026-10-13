import { Fragment, useMemo, useRef } from 'react';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import ToggleButton from '@mui/material/ToggleButton';
import Tooltip from '@mui/material/Tooltip';
import { isMacOS, type Editor } from '@tiptap/core';
import { useEditorState } from '@tiptap/react';
import type { RichTextCopy } from '../copy';
import {
  TOOLBAR_GROUPS,
  selectToolbarContext,
  type ToolbarContext,
  type ToolbarControl,
} from './model';
import { formatShortcut } from './formatShortcut';
import { useRovingToolbar } from './useRovingToolbar';

interface ToolbarProps {
  editor: Editor;
  copy: RichTextCopy;
}

/**
 * The toolbar of the editor (design § Toolbar): `role="toolbar"`, one tab stop
 * with a roving tabindex, toggles as `aria-pressed` buttons, each with its name
 * and shortcut as a tooltip and its bare name as `aria-label`. Buttons keep the
 * editor's selection (their `mousedown` default is prevented), and a keyboard
 * activation leaves focus on the control.
 */
export function Toolbar({ editor, copy }: ToolbarProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const context = useEditorState({
    editor,
    selector: ({ editor: current }) => selectToolbarContext(current),
  });

  const order = useMemo(
    () => TOOLBAR_GROUPS.flatMap((group) => group.controls.map((control) => control.id)),
    [],
  );
  const enabled = new Set(
    TOOLBAR_GROUPS.flatMap((group) => group.controls)
      .filter((control) => !control.state(context).disabled)
      .map((control) => control.id),
  );
  const roving = useRovingToolbar(containerRef, order, enabled);
  const mac = isMacOS();

  return (
    <Box
      ref={containerRef}
      role="toolbar"
      aria-label={copy.toolbarLabel}
      aria-orientation="horizontal"
      onKeyDown={roving.onKeyDown}
      sx={(theme) => ({
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: 0.5,
        px: 0.5,
        py: 0.5,
        borderBottom: `1px solid ${theme.palette.divider}`,
      })}
    >
      {TOOLBAR_GROUPS.map((group, index) => (
        <Fragment key={group.id}>
          {index > 0 && <Divider orientation="vertical" flexItem sx={{ mx: 0.5 }} />}
          <Box role="group" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            {group.controls.map((control) => (
              <ToolbarButton
                key={control.id}
                control={control}
                context={context}
                copy={copy}
                editor={editor}
                mac={mac}
                tabIndex={roving.tabStop === control.id ? 0 : -1}
                onFocusControl={roving.onFocusControl}
              />
            ))}
          </Box>
        </Fragment>
      ))}
    </Box>
  );
}

interface ToolbarButtonProps {
  control: ToolbarControl;
  context: ToolbarContext;
  copy: RichTextCopy;
  editor: Editor;
  mac: boolean;
  tabIndex: number;
  onFocusControl: (id: string) => void;
}

function ToolbarButton({
  control,
  context,
  copy,
  editor,
  mac,
  tabIndex,
  onFocusControl,
}: ToolbarButtonProps) {
  const name = control.name(copy);
  const { pressed = false, disabled } = control.state(context);
  const tooltip = control.shortcut ? `${name} (${formatShortcut(control.shortcut, mac)})` : name;
  const content = control.icon ? (
    <control.icon fontSize="small" />
  ) : (
    <Box component="span" sx={{ fontSize: 'caption.fontSize', fontWeight: 'fontWeightMedium' }}>
      {control.glyph}
    </Box>
  );

  const common = {
    'aria-label': name,
    'data-control-id': control.id,
    disabled,
    tabIndex,
    onFocus: () => onFocusControl(control.id),
    // The selection stays where it was: a button never takes focus from the body by the pointer.
    onMouseDown: (event: { preventDefault(): void }) => event.preventDefault(),
    onClick: () => control.run(editor),
    sx: (theme: { density: { controlHeight: number } }) => ({
      minWidth: theme.density.controlHeight,
      height: theme.density.controlHeight,
    }),
  };

  // A disabled button takes no pointer events, so the tooltip hangs on a span.
  return (
    <Tooltip title={tooltip} describeChild>
      <span>
        {control.kind === 'toggle' ? (
          <ToggleButton {...common} value={control.id} selected={pressed} size="small">
            {content}
          </ToggleButton>
        ) : (
          <IconButton {...common} size="small">
            {content}
          </IconButton>
        )}
      </span>
    </Tooltip>
  );
}
