import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rgb, theme } from '../../../test/kitTesting';
import { TagList } from './TagList';

// tech-015 design § 5.4 «TagList»: a list of tags 8 px apart, with a trailing «+» when
// the host can add — the place kept for the tags feature.

const TAGS = [
  { id: 't1', label: 'As-Is' },
  { id: 't2', label: 'Discovery', tone: 'accent' as const },
  { id: 't3', label: 'Functional' },
];

describe('tech-015 Scenario: TagList (design § 5.4)', () => {
  it('is a list with one item per tag', () => {
    render(<TagList tags={TAGS} />);
    const list = screen.getByRole('list');
    expect(
      within(list)
        .getAllByRole('listitem')
        .map((item) => item.textContent),
    ).toEqual(['As-Is', 'Discovery', 'Functional']);
  });

  it('wraps its tags 8 px apart', () => {
    render(<TagList tags={TAGS} />);
    const style = getComputedStyle(screen.getByRole('list'));
    expect(style.flexWrap).toBe('wrap');
    expect(style.columnGap || style.gap).toBe('8px');
  });

  it('passes each tag its tone', () => {
    render(<TagList tags={TAGS} />);
    expect(
      getComputedStyle(screen.getByText('Discovery').closest('.MuiChip-root')!).backgroundColor,
    ).toBe(rgb(theme.palette.shell.rowSelected));
  });

  describe('Scenario: a read-only host', () => {
    it('draws no remove button and no «+» when it passes neither callback', () => {
      render(<TagList tags={TAGS} />);
      expect(screen.queryByRole('button')).toBeNull();
    });

    it('draws nothing at all when there are no tags and no way to add one', () => {
      const { container } = render(<TagList tags={[]} />);
      expect(container).toBeEmptyDOMElement();
    });
  });

  describe('Scenario: onRemove', () => {
    it('gives each tag its remove button and passes the id of the one removed', async () => {
      const user = userEvent.setup();
      const onRemove = vi.fn();
      render(<TagList tags={TAGS} onRemove={onRemove} />);
      expect(screen.getAllByRole('button')).toHaveLength(3);
      await user.click(screen.getByRole('button', { name: 'Удалить тег «Discovery»' }));
      expect(onRemove).toHaveBeenCalledExactlyOnceWith('t2');
    });
  });

  describe('Scenario: onAdd', () => {
    it('ends the list with a «+» named «Добавить тег» by default', async () => {
      const user = userEvent.setup();
      const onAdd = vi.fn();
      render(<TagList tags={TAGS} onAdd={onAdd} />);
      const items = screen.getAllByRole('listitem');
      expect(items).toHaveLength(4);
      const add = within(items[3]!).getByRole('button', { name: 'Добавить тег' });
      expect(add.querySelector('[data-testid="ThinAddIcon"]')).not.toBeNull();
      await user.click(add);
      expect(onAdd).toHaveBeenCalledTimes(1);
    });

    it('is a 24 px ghost on the neutral grey, its radius half of the shape’s', () => {
      render(<TagList tags={[]} onAdd={() => {}} />);
      const style = getComputedStyle(screen.getByRole('button', { name: 'Добавить тег' }));
      expect(px(style.width)).toBe(theme.layout.iconAction.row.box);
      expect(px(style.height)).toBe(theme.layout.iconAction.row.box);
      expect(style.backgroundColor).toBe(rgb(theme.palette.shell.buttonNeutral));
      expect(style.borderRadius).toBe(`${Number(theme.shape.borderRadius) / 2}px`);
    });

    it('draws the «+» even for an empty list, and names it as told', () => {
      render(<TagList tags={[]} onAdd={() => {}} addLabel="Добавить метку" />);
      expect(screen.getByRole('button', { name: 'Добавить метку' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Добавить тег' })).toBeNull();
    });
  });
});
