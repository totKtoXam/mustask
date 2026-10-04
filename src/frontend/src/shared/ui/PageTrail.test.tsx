import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PageTrail } from './PageTrail';

describe('US-2 Scenario: Page trail (BR-6) — the trail itself', () => {
  it('is a navigation region «Путь к странице»; an ancestor with a page is a link, one without is plain text', () => {
    render(
      <MemoryRouter>
        <PageTrail
          segments={[
            { label: 'Настройки' },
            { label: 'Справочники' },
            { label: 'Языки', to: '/dictionaries/languages' },
          ]}
        />
      </MemoryRouter>,
    );

    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    expect(within(trail).getAllByRole('link')).toHaveLength(1);
    expect(within(trail).getByRole('link', { name: 'Языки' })).toHaveAttribute(
      'href',
      '/dictionaries/languages',
    );
    expect(within(trail).getByText('Настройки').closest('a')).toBeNull();
    expect(within(trail).getByText('Справочники').closest('a')).toBeNull();
  });

  it('keeps a segment’s full text in a tooltip', async () => {
    const long = 'Очень длинное название раздела, которое не помещается в один сегмент пути';
    render(
      <MemoryRouter>
        <PageTrail segments={[{ label: long, to: '/section' }]} />
      </MemoryRouter>,
    );

    await userEvent.hover(screen.getByRole('link', { name: long }));

    expect(await screen.findByRole('tooltip')).toHaveTextContent(long);
  });
});
