import { useLayoutEffect } from 'react';
import Box from '@mui/material/Box';
import { useDocumentTitle } from '../../../shared/lib/documentTitle';
import { usePageChrome } from '../../../shared/ui/pageChrome';
import { PageTrail } from '../../../shared/ui/PageTrail';

/**
 * The form's header seam with the shell, which `PageHeader` holds for every
 * other page (mod-015 plan § 4.5): the band is the form's header (mod-015
 * § 10.3), so it registers itself — the shell then derives the trail
 * «Рабочие элементы» from its navigation model — names the browser tab, and
 * shows the trail above itself where the shell asks for it there (below
 * 1024 px). `title` absent: the tab is left alone (while loading).
 */
export function HeaderChrome({ title }: { title?: string }) {
  useDocumentTitle(title);
  const { registerHeader, trail, trailPlacement } = usePageChrome();
  useLayoutEffect(() => registerHeader([]), [registerHeader]);

  return trailPlacement === 'header' && trail.length > 0 ? (
    <Box sx={{ pt: 2 }}>
      <PageTrail segments={trail} />
    </Box>
  ) : null;
}
