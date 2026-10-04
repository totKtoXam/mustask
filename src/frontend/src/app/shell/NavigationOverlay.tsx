import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Drawer from '@mui/material/Drawer';
import useMediaQuery from '@mui/material/useMediaQuery';

export interface NavigationOverlayProps {
  /** The id «Меню» points at with `aria-controls`. */
  id: string;
  open: boolean;
  /** Escape or a click outside: MUI returns focus to «Меню» (BR-10). */
  onClose: () => void;
  /** After the overlay has gone: moves focus into the main region when an entry was chosen. */
  onExited: () => void;
  children: ReactNode;
}

/**
 * The navigation below 1024 px (US-4, BR-10; design § 4.5): a temporary
 * drawer from the left, not kept mounted while closed, so its navigation
 * regions exist only while it is open. Without animation under
 * `prefers-reduced-motion: reduce`.
 *
 * It is a modal and stands at the modal level, above the top bar, which is
 * raised over the permanent drawers (`zIndex.drawer + 1`, design § 3.2): the
 * top of the overlay is not covered, and the backdrop lies over the top bar
 * too, so a click there is a click outside (BR-10; the prototype stacks it
 * the same way).
 */
export function NavigationOverlay({
  id,
  open,
  onClose,
  onExited,
  children,
}: NavigationOverlayProps) {
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)', { noSsr: true });

  return (
    <Drawer
      variant="temporary"
      anchor="left"
      open={open}
      onClose={onClose}
      sx={{ zIndex: (theme) => theme.zIndex.modal }}
      transitionDuration={reducedMotion ? 0 : undefined}
      ModalProps={{ keepMounted: false }}
      slotProps={{
        transition: { onExited },
        paper: { id, sx: { bgcolor: 'shell.navigation' } },
      }}
    >
      <Box sx={{ display: 'flex', height: '100%' }}>{children}</Box>
    </Drawer>
  );
}
