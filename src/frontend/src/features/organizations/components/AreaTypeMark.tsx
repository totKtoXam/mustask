import type { ReactElement } from 'react';
import Box from '@mui/material/Box';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import ApartmentOutlinedIcon from '@mui/icons-material/ApartmentOutlined';
import FolderOutlinedIcon from '@mui/icons-material/FolderOutlined';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import NotesOutlinedIcon from '@mui/icons-material/NotesOutlined';
import type { AreaType } from '../api/organizationsApi';
import type { AreaCommandKind } from '../lib/areaCommands';
import { typeLabels } from '../lib/areaTexts';

const TYPE_ICONS = {
  department: ApartmentOutlinedIcon,
  project: FolderOutlinedIcon,
  area: AccountTreeOutlinedIcon,
} as const;

/**
 * The type mark of a node (design § 2): a decorative icon and the label as text, so colour and
 * shape are never the only carrier.
 */
export function AreaTypeMark({ type }: { type: AreaType }) {
  const Icon = TYPE_ICONS[type];
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1 }}>
      <Icon fontSize="small" />
      {typeLabels[type]}
    </Box>
  );
}

/** The icon of a command, the size the surface draws it at. */
export function commandIcon(kind: AreaCommandKind, small = false): ReactElement {
  const size = small ? ('small' as const) : undefined;
  switch (kind) {
    case 'createChild':
      return <AddIcon fontSize={size} />;
    case 'rename':
      return <EditOutlinedIcon fontSize={size} />;
    case 'describe':
      return <NotesOutlinedIcon fontSize={size} />;
    case 'move':
      return <DriveFileMoveOutlinedIcon fontSize={size} />;
    case 'delete':
      return <DeleteOutlineOutlinedIcon fontSize={size} />;
  }
}
