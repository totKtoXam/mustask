import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import { cardContainerSx } from '../card/cardContainer';
import { surfaceSx } from '../layout/surfaceStyles';

export interface PanelShellProps {
  /** `usePageHead(...).trail`: the trail above the panel, when the shell wants it there. */
  trail?: ReactNode;
  /** A `CardHeaderBar`. */
  header?: ReactNode;
  /** A `PropertyBand`. */
  band?: ReactNode;
  /** An `InlineNotice`. */
  alert?: ReactNode;
  children: ReactNode;
}

/**
 * The same card on a page (tech-015 design § 5.10): the shell's trail above, then one
 * white panel on the application background, with the slots of `DialogShell` — header,
 * band, alert, body. The panel is a card surface (the `Surface` look) that also declares
 * the `kit-card` container its header and band read.
 *
 * **The page scrolls, not the card**: the header, the band and the tab strip scroll with
 * it, nothing is fixed, and the body has no inner scrollbar. No «Развернуть на страницу»
 * and no «Закрыть» — the trail leads back. The space above is the shell's, not the host's:
 * `layout.pageHead.panelTop`, then the trail with `trailGap` under it.
 */
export function PanelShell({ trail, header, band, alert, children }: PanelShellProps) {
  return (
    <Box sx={(theme) => ({ paddingTop: `${theme.layout.pageHead.panelTop}px` })}>
      {trail && (
        <Box
          data-panel-trail=""
          sx={(theme) => ({ marginBottom: `${theme.layout.pageHead.trailGap}px` })}
        >
          {trail}
        </Box>
      )}
      <Paper
        elevation={0}
        data-panel-shell=""
        sx={(theme) => ({ ...surfaceSx(theme, 'card'), ...cardContainerSx })}
      >
        {header}
        {band}
        {alert}
        {children}
      </Paper>
    </Box>
  );
}
