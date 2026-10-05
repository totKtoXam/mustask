import { describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AddIcon from '@mui/icons-material/Add';
import { PageHeader } from './PageHeader';

// mod-003 spec § Page «Рабочие элементы»: the primary «Новый рабочий элемент»
// (`Add`, `ExpandMore`) opens the type picker, so a command may announce a
// menu (mod-015 § 10.1: `aria-haspopup="menu"`).

describe('mod-003: a header command that opens a menu', () => {
  it('announces the menu, carries `ExpandMore` and hands the click to its handler', async () => {
    // React resets `currentTarget` once the handler returns: read it inside.
    let anchor: unknown = null;
    const onClick = vi.fn((event?: { currentTarget: unknown }) => {
      anchor = event?.currentTarget ?? null;
    });
    const { rerender } = render(
      <MemoryRouter>
        <PageHeader
          title="Рабочие элементы"
          primaryAction={{
            label: 'Новый рабочий элемент',
            icon: <AddIcon />,
            menuOpen: false,
            onClick,
          }}
        />
      </MemoryRouter>,
    );

    const button = screen.getByRole('button', { name: 'Новый рабочий элемент' });
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(
      button.querySelector('.MuiButton-endIcon [data-testid="ExpandMoreIcon"]'),
    ).not.toBeNull();

    await userEvent.click(button);
    expect(onClick).toHaveBeenCalledTimes(1);
    expect(anchor).toBe(button);

    rerender(
      <MemoryRouter>
        <PageHeader
          title="Рабочие элементы"
          primaryAction={{
            label: 'Новый рабочий элемент',
            icon: <AddIcon />,
            menuOpen: true,
            onClick,
          }}
        />
      </MemoryRouter>,
    );
    expect(screen.getByRole('button', { name: 'Новый рабочий элемент' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
  });

  it('leaves a plain command as before: no popup, no trailing icon', () => {
    render(
      <MemoryRouter>
        <PageHeader
          title="Проекты"
          primaryAction={{ label: 'Создать проект', icon: <AddIcon />, onClick: () => {} }}
        />
      </MemoryRouter>,
    );

    const button = screen.getByRole('button', { name: 'Создать проект' });
    expect(button).not.toHaveAttribute('aria-haspopup');
    expect(button).not.toHaveAttribute('aria-expanded');
    expect(button.querySelector('.MuiButton-endIcon')).toBeNull();
  });
});
