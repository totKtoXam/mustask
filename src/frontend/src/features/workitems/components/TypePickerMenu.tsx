import { useEffect } from 'react';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import { focusMainRegion } from '../../../shared/lib/mainRegion';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { CreatableType } from '../lib/process';

/** The menu's name: the command that opens it. */
export const NEW_WORK_ITEM_LABEL = 'Новый рабочий элемент';

/**
 * The type picker (spec § Page «Рабочие элементы»; mod-015 § 10.1): the types
 * it is given in that order, each with its icon — the five of the
 * organization's process on the project's list, a level's on a team's
 * backlog or board (mod-006). A choice opens the create dialog preset to it.
 *
 * It belongs to the control that opens it, which needs `workitems.workitem.write`
 * (mod-016 design § 6.2): when `allowed` turns false with the menu open — the
 * served codes were read again — the menu closes with its control, focus goes to
 * the main region (the control it would return to is gone), and no type can be
 * chosen into a create dialog that is already locked (design § 2.2).
 */
export function TypePickerMenu({
  anchor,
  allowed,
  types,
  onChoose,
  onClose,
}: {
  anchor: HTMLElement | null;
  /** The user may still create items here. */
  allowed: boolean;
  types: readonly CreatableType[];
  onChoose(type: CreatableType): void;
  onClose(): void;
}) {
  const stale = anchor !== null && !allowed;
  useEffect(() => {
    if (stale) {
      onClose();
      focusMainRegion();
    }
  }, [stale, onClose]);

  return (
    <Menu
      anchorEl={anchor}
      open={anchor !== null && allowed}
      onClose={onClose}
      slotProps={{ list: { 'aria-label': NEW_WORK_ITEM_LABEL } }}
    >
      {types.map((type) => (
        <MenuItem key={type.id} onClick={() => onChoose(type)}>
          <ListItemIcon>
            <TypeMark appearance={type.appearance} />
          </ListItemIcon>
          <ListItemText>{type.name}</ListItemText>
        </MenuItem>
      ))}
    </Menu>
  );
}
