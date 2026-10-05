import { describe, expect, it } from 'vitest';
import { MemoryRouter, Route, Routes, useNavigate } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WorkContextContext, type WorkContextSource } from '../../../shared/lib/workContext';
import { contextSource } from '../testRender';
import { useNotify } from './notice';
import { OrganizationsSection } from './OrganizationsSection';

function Acting() {
  const notify = useNotify();
  const navigate = useNavigate();
  return (
    <>
      <button type="button" onClick={() => notify('Команда «Ядро» удалена.')}>
        Успех
      </button>
      <button type="button" onClick={() => notify('Организация не найдена.', 'error')}>
        Отказ
      </button>
      <button
        type="button"
        onClick={() => {
          notify('Проект «Ядро» создан.');
          navigate('/next');
        }}
      >
        Дальше
      </button>
    </>
  );
}

function tree(source: WorkContextSource) {
  return (
    <WorkContextContext.Provider value={source}>
      <MemoryRouter initialEntries={['/first']}>
        <Routes>
          <Route element={<OrganizationsSection />}>
            <Route path="/first" element={<Acting />} />
            <Route path="/next" element={<p>Следующая страница</p>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </WorkContextContext.Provider>
  );
}

describe('mod-002 design § 4.5: the slice’s one notice', () => {
  it('shows a completed action as plain text and a refusal as an error alert', async () => {
    render(tree(contextSource()));

    await userEvent.click(screen.getByRole('button', { name: 'Успех' }));
    expect(screen.getByText('Команда «Ядро» удалена.')).toBeInTheDocument();
    expect(document.querySelector('.MuiAlert-root')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Отказ' }));
    expect(document.querySelector('.MuiAlert-root')).toHaveTextContent('Организация не найдена.');
    expect(document.querySelector('.MuiAlert-root')?.className).toMatch(
      /MuiAlert-(standard|color)Error/,
    );
    expect(screen.queryByText('Команда «Ядро» удалена.')).not.toBeInTheDocument();
  });

  it('outlives the move to the page the action opens', async () => {
    render(tree(contextSource()));

    await userEvent.click(screen.getByRole('button', { name: 'Дальше' }));

    expect(screen.getByText('Следующая страница')).toBeInTheDocument();
    expect(screen.getByText('Проект «Ядро» создан.')).toBeInTheDocument();
  });
});

describe('mod-002 DC-7: one notice at a time, the switcher’s fallback wins', () => {
  it('closes its notice when the switcher raises a fallback', async () => {
    const { rerender } = render(tree(contextSource()));
    await userEvent.click(screen.getByRole('button', { name: 'Успех' }));
    expect(screen.getByText('Команда «Ядро» удалена.')).toBeInTheDocument();

    rerender(tree(contextSource({ fallback: { id: 1, names: ['Альфа'] } })));

    expect(screen.queryByText('Команда «Ядро» удалена.')).not.toBeInTheDocument();
  });
});
