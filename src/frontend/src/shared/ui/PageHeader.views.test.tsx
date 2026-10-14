import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import { PageHeader, type PageHeaderProps } from './PageHeader';

// mod-016 DC-2 (design § 6.2): a tab strip of one view is a control in all but
// name, so a plain header draws tabs for two or more views; the team head of
// «Бэклоги» and «Канбан» keeps its one tab.

function renderHeader(props: PageHeaderProps) {
  return render(
    <MemoryRouter initialEntries={['/organizations/o/settings/projects']}>
      <PageHeader {...props} />
    </MemoryRouter>,
  );
}

const active = { label: 'Активные', to: '/organizations/o/settings/projects' };
const deactivated = { label: 'Деактивированные', to: '/organizations/o/settings/projects/x' };

describe('mod-016 DC-2 Scenario: a one-view tab strip is not drawn', () => {
  it('draws no tab for a plain header with a single view', () => {
    renderHeader({ title: 'Проекты', views: [active] });

    expect(screen.getByRole('heading', { level: 1, name: 'Проекты' })).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
    expect(screen.queryByRole('tab')).not.toBeInTheDocument();
  });

  it('draws the tabs for two views, the current one selected', () => {
    renderHeader({
      title: 'Проекты',
      views: [active, deactivated],
      viewsLabel: 'Проекты по статусу',
    });

    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      'Активные',
      'Деактивированные',
    ]);
    expect(screen.getByRole('tab', { name: 'Активные' })).toHaveAttribute('aria-selected', 'true');
  });

  it('keeps the one tab of the team head, which has the level drop-down beside it', () => {
    renderHeader({
      title: 'Платформа',
      variant: 'team',
      views: [{ label: 'Бэклог', to: '/organizations/o/settings/projects' }],
      viewsEnd: <button type="button">Эпики</button>,
    });

    expect(screen.getAllByRole('tab')).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Эпики' })).toBeInTheDocument();
  });

  it('keeps a single view that has something at the end of the row', () => {
    renderHeader({
      title: 'Платформа',
      views: [active],
      viewsEnd: <button type="button">Эпики</button>,
    });

    expect(screen.getAllByRole('tab')).toHaveLength(1);
  });

  it('draws nothing for no views', () => {
    renderHeader({ title: 'Проекты', views: [] });

    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
  });
});
