import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { CreatableType } from '../lib/process';

/** The menu's name: the command that opens it. */
export const NEW_WORK_ITEM_LABEL = 'Новый рабочий элемент';

/**
 * The type picker (spec § Page «Рабочие элементы»; mod-015 § 10.1): the types
 * it is given in that order, each with its icon — the five of the
 * organization's process on the project's list, a level's on a team's
 * backlog or board (mod-006). A choice opens the create dialog preset to it.
 */
export function TypePickerMenu({
  anchor,
  types,
  onChoose,
  onClose,
}: {
  anchor: HTMLElement | null;
  types: readonly CreatableType[];
  onChoose(type: CreatableType): void;
  onClose(): void;
}) {
  return (
    <Menu
      anchorEl={anchor}
      open={anchor !== null}
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
