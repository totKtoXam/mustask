import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import Box from '@mui/material/Box';
import { ColumnLayout } from '../../../../shared/ui';
import { rulesFor } from '../../../../test/kitTesting';
import { COLUMNS_CONTAINER, oneColumnCap } from './oneColumn';

// jsdom evaluates no container query, so the cap's one-column rule is read as the rule the
// stylesheet holds; the cap itself is measured on a stand (E2E 25.1 at 640 px). What can
// break it silently is the container's name: the kit's `ColumnLayout` declares it, the card
// copies it (R1-8).

describe('feat-029 US-3: the one-column cap queries the container the kit declares', () => {
  it('names the same container as ColumnLayout', () => {
    render(
      <ColumnLayout
        main={
          <Box data-testid="cap" sx={oneColumnCap}>
            Поля
          </Box>
        }
      />,
    );
    const container = document.querySelector('[data-column-layout]') as HTMLElement;
    const declared = rulesFor(container, '').find((rule) => rule.declarations['container-name']);
    expect(declared?.declarations['container-name']).toBe(COLUMNS_CONTAINER);

    const cap = rulesFor(screen.getByTestId('cap'), '').find(
      (rule) => rule.declarations['max-width'],
    );
    expect(cap?.media).toContain(COLUMNS_CONTAINER);
    expect(cap?.media).toContain('max-width');
  });
});
