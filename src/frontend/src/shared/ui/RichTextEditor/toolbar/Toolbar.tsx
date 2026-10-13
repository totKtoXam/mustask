import { Fragment, useMemo, useRef, useState } from 'react';
import CheckIcon from '@mui/icons-material/Check';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ToggleButton from '@mui/material/ToggleButton';
import Tooltip from '@mui/material/Tooltip';
import { isMacOS, type Editor } from '@tiptap/core';
import { useEditorState } from '@tiptap/react';
import type { RichTextCopy } from '../copy';
import { EmojiPopover } from './EmojiPopover';
import { formatShortcut } from './formatShortcut';
import {
  TOOLBAR_GROUPS,
  selectToolbarContext,
  type ToolbarContext,
  type ToolbarControl,
  type ToolbarUi,
} from './model';
import { useRovingToolbar } from './useRovingToolbar';

interface ToolbarProps {
  editor: Editor;
  copy: RichTextCopy;
  ui: ToolbarUi;
}

interface Opened {
  control: ToolbarControl;
  anchor: HTMLElement;
}

/**
 * The toolbar of the editor (design § Toolbar): `role="toolbar"`, one tab stop
 * with a roving tabindex, toggles as `aria-pressed` buttons, menus and the
 * emoji popover opened from their buttons, each control with its name and
 * shortcut as a tooltip and its bare name as `aria-label`. Buttons keep the
 * editor's selection (their `mousedown` default is prevented); a keyboard
 * activation leaves focus on the control, a menu pick or an emoji returns it to
 * the body.
 */
export function Toolbar({ editor, copy, ui }: ToolbarProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const context = useEditorState({
    editor,
    selector: ({ editor: current }) => selectToolbarContext(current),
  });
  const [opened, setOpened] = useState<Opened | null>(null);

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

  const closeOpened = (focus: 'opener' | 'body' | 'none') => {
    const anchor = opened?.anchor;
    setOpened(null);
    // After the menu or popover has left: its focus trap would take the focus back.
    window.setTimeout(() => {
      if (focus === 'opener') anchor?.focus();
      if (focus === 'body') editor.commands.focus();
    }, 0);
  };

  const menuControl = opened?.control.kind === 'menu' ? opened.control : null;
  const emojiOpen = opened?.control.kind === 'popover';

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
                mac={mac}
                expanded={opened?.control.id === control.id}
                tabIndex={roving.tabStop === control.id ? 0 : -1}
                onFocusControl={roving.onFocusControl}
                onActivate={(anchor) => {
                  if (control.kind === 'menu' || control.kind === 'popover') {
                    setOpened({ control, anchor });
                  } else {
                    control.run?.(editor, ui);
                  }
                }}
              />
            ))}
          </Box>
        </Fragment>
      ))}

      {menuControl && opened && (
        <ControlMenu
          control={menuControl}
          anchor={opened.anchor}
          context={context}
          copy={copy}
          onChoose={(value) => {
            menuControl.choose?.(editor, value);
            closeOpened('body');
          }}
          onClose={(reason) => closeOpened(reason === 'escapeKeyDown' ? 'opener' : 'none')}
        />
      )}

      <EmojiPopover
        anchorEl={emojiOpen && opened ? opened.anchor : null}
        copy={copy.emoji}
        onPick={(emoji) => {
          editor.chain().insertContent({ type: 'text', text: emoji }).run();
          closeOpened('body');
        }}
        onClose={(reason) => closeOpened(reason === 'escape' ? 'opener' : 'none')}
      />
    </Box>
  );
}

interface ToolbarButtonProps {
  control: ToolbarControl;
  context: ToolbarContext;
  copy: RichTextCopy;
  mac: boolean;
  expanded: boolean;
  tabIndex: number;
  onFocusControl: (id: string) => void;
  onActivate: (anchor: HTMLElement) => void;
}

function ToolbarButton({
  control,
  context,
  copy,
  mac,
  expanded,
  tabIndex,
  onFocusControl,
  onActivate,
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
    onClick: (event: { currentTarget: HTMLElement }) => onActivate(event.currentTarget),
    sx: (theme: { density: { controlHeight: number } }) => ({
      minWidth: theme.density.controlHeight,
      height: theme.density.controlHeight,
    }),
    ...(control.kind === 'menu'
      ? { 'aria-haspopup': 'menu' as const, 'aria-expanded': expanded }
      : control.kind === 'popover'
        ? { 'aria-haspopup': 'dialog' as const, 'aria-expanded': expanded }
        : {}),
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
          <IconButton {...common} size="small" color={pressed ? 'primary' : 'default'}>
            {content}
          </IconButton>
        )}
      </span>
    </Tooltip>
  );
}

interface ControlMenuProps {
  control: ToolbarControl;
  anchor: HTMLElement;
  context: ToolbarContext;
  copy: RichTextCopy;
  onChoose: (value: string | null) => void;
  onClose: (reason: 'escapeKeyDown' | 'backdropClick' | 'tabKeyDown') => void;
}

/** A radio-style `Menu` over a control's options: swatch, name, and a check on the current one. */
function ControlMenu({ control, anchor, context, copy, onChoose, onClose }: ControlMenuProps) {
  const options = control.options?.(copy, context) ?? [];
  const current = control.state(context).value ?? null;

  return (
    <Menu
      open
      anchorEl={anchor}
      onClose={(_event, reason) => onClose(reason)}
      disableRestoreFocus
      slotProps={{ list: { 'aria-label': control.name(copy) } }}
    >
      {options.map((option) => {
        const checked = option.value === current;
        const colour = option.swatch
          ? (theme: { richText: { colour(className: string): string | null } }) =>
              theme.richText.colour(option.swatch?.className ?? '') ?? 'transparent'
          : null;
        return (
          <MenuItem
            key={option.value ?? 'reset'}
            role="menuitemradio"
            aria-checked={checked}
            selected={checked}
            disabled={option.disabled}
            divider={option.divided}
            onClick={() => onChoose(option.value)}
          >
            {colour && (
              <ListItemIcon>
                <Box
                  aria-hidden="true"
                  sx={(theme) => ({
                    width: 16,
                    height: 16,
                    borderRadius: `${theme.shape.borderRadius}px`,
                    border: `1px solid ${theme.palette.divider}`,
                    backgroundColor: colour(theme),
                  })}
                />
              </ListItemIcon>
            )}
            <ListItemText>{option.label}</ListItemText>
            {checked && <CheckIcon fontSize="small" sx={{ ml: 2 }} />}
          </MenuItem>
        );
      })}
    </Menu>
  );
}
