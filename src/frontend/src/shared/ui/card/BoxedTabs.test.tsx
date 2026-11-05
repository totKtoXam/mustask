import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rgb, theme } from '../../../test/kitTesting';
import { ThinRefreshIcon } from '../icons/ThinIcons';
import { BoxedTabPanel, BoxedTabs, type BoxedTab } from './BoxedTabs';

// tech-015 design § 5.6 «BoxedTabs»: the sections of a card, a strip 36 high against the
// band's lower edge; a tab with a count is named «Связи (1)»; the selected tab's indicator is
// 2 thick, as wide as its label and 4 above the strip's bottom; the panel is linked to its tab.

const TABS: BoxedTab[] = [
  { value: 'details', label: 'Детали' },
  { value: 'history', label: 'История', icon: <ThinRefreshIcon /> },
  { value: 'links', label: 'Связи', count: 1 },
  { value: 'files', label: 'Вложения', icon: <ThinRefreshIcon />, iconOnly: true },
];

function Host({
  tabs = TABS,
  initial = 'details',
  onChange = () => {},
}: {
  tabs?: BoxedTab[];
  initial?: string;
  onChange?: (value: string) => void;
}) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <BoxedTabs
        idBase="card"
        aria-label="Разделы рабочего элемента"
        value={value}
        onChange={(next) => {
          setValue(next);
          onChange(next);
        }}
        tabs={tabs}
      />
      {tabs.map((tab) => (
        <BoxedTabPanel
          key={tab.value}
          idBase="card"
          value={tab.value}
          selected={tab.value === value}
        >
          Раздел «{tab.label}»
        </BoxedTabPanel>
      ))}
    </>
  );
}

describe('tech-015 Scenario: BoxedTabs (design § 5.6)', () => {
  describe('Scenario: names', () => {
    it('names the tablist by its aria-label', () => {
      render(<Host />);
      expect(
        screen.getByRole('tablist', { name: 'Разделы рабочего элемента' }),
      ).toBeInTheDocument();
    });

    it('names a tab by its label, and a tab with a count «Связи (1)» — never by the bare digit', () => {
      render(<Host />);
      expect(screen.getByRole('tab', { name: 'Детали' })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: 'История' })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: 'Связи (1)' })).toBeInTheDocument();
      expect(screen.queryByRole('tab', { name: '1' })).toBeNull();
    });

    it('names an icon-only tab by its label and shows the label as a tooltip', async () => {
      const user = userEvent.setup();
      render(<Host />);
      const tab = screen.getByRole('tab', { name: 'Вложения' });
      expect(tab).not.toHaveTextContent('Вложения');
      await user.hover(tab);
      expect(await screen.findByRole('tooltip')).toHaveTextContent('Вложения');
    });

    it('draws a count as a pill after the label, hidden from assistive technology', () => {
      render(<Host />);
      const tab = screen.getByRole('tab', { name: 'Связи (1)' });
      const pill = tab.querySelector('[data-tab-count]') as HTMLElement;
      expect(pill).toHaveTextContent('1');
      expect(pill).toHaveAttribute('aria-hidden', 'true');
      expect(getComputedStyle(pill).backgroundColor).toBe(rgb(theme.palette.shell.buttonNeutral));
    });

    it('is a valid strip with one tab', () => {
      render(<Host tabs={[{ value: 'details', label: 'Детали' }]} />);
      expect(screen.getAllByRole('tab')).toHaveLength(1);
      expect(screen.getByRole('tab', { name: 'Детали' })).toHaveAttribute('aria-selected', 'true');
    });
  });

  describe('Scenario: the panel is linked to its tab', () => {
    it('gives the selected panel role tabpanel named by its tab, and the tab aria-controls the panel', () => {
      render(<Host />);
      const tab = screen.getByRole('tab', { name: 'Детали' });
      const panel = screen.getByRole('tabpanel', { name: 'Детали' });
      expect(panel).toHaveAttribute('aria-labelledby', tab.id);
      expect(tab).toHaveAttribute('aria-controls', panel.id);
      expect(panel).toHaveTextContent('Раздел «Детали»');
    });

    it('renders no panel for a tab that is not selected', () => {
      render(<Host />);
      expect(screen.getAllByRole('tabpanel')).toHaveLength(1);
      expect(screen.queryByText('Раздел «История»')).toBeNull();
    });

    it('swaps the panel when another tab is chosen, and links the new pair', async () => {
      const user = userEvent.setup();
      render(<Host />);
      await user.click(screen.getByRole('tab', { name: 'Связи (1)' }));
      const panel = screen.getByRole('tabpanel');
      expect(panel).toHaveTextContent('Раздел «Связи»');
      expect(panel).toHaveAttribute(
        'aria-labelledby',
        screen.getByRole('tab', { name: 'Связи (1)' }).id,
      );
    });

    it('keeps the ids of two strips apart through idBase', () => {
      render(
        <>
          <BoxedTabs
            idBase="a"
            aria-label="Первый"
            value="x"
            onChange={() => {}}
            tabs={[{ value: 'x', label: 'Один' }]}
          />
          <BoxedTabs
            idBase="b"
            aria-label="Второй"
            value="x"
            onChange={() => {}}
            tabs={[{ value: 'x', label: 'Два' }]}
          />
        </>,
      );
      expect(screen.getByRole('tab', { name: 'Один' }).id).not.toBe(
        screen.getByRole('tab', { name: 'Два' }).id,
      );
    });
  });

  describe('Scenario: keyboard', () => {
    it('moves focus with Left and Right, wrapping, and with Home and End', async () => {
      const user = userEvent.setup();
      render(<Host />);
      screen.getByRole('tab', { name: 'Детали' }).focus();
      await user.keyboard('{ArrowRight}');
      expect(screen.getByRole('tab', { name: 'История' })).toHaveFocus();
      await user.keyboard('{End}');
      expect(screen.getByRole('tab', { name: 'Вложения' })).toHaveFocus();
      await user.keyboard('{ArrowRight}');
      expect(screen.getByRole('tab', { name: 'Детали' })).toHaveFocus();
      await user.keyboard('{ArrowLeft}');
      expect(screen.getByRole('tab', { name: 'Вложения' })).toHaveFocus();
      await user.keyboard('{Home}');
      expect(screen.getByRole('tab', { name: 'Детали' })).toHaveFocus();
    });

    it('selects on Enter and on Space — moving focus alone selects nothing', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Host onChange={onChange} />);
      screen.getByRole('tab', { name: 'Детали' }).focus();
      await user.keyboard('{ArrowRight}');
      expect(onChange).not.toHaveBeenCalled();
      await user.keyboard('{Enter}');
      expect(onChange).toHaveBeenLastCalledWith('history');
      await user.keyboard('{ArrowRight}{ }');
      expect(onChange).toHaveBeenLastCalledWith('links');
      expect(screen.getByRole('tab', { name: 'Связи (1)' })).toHaveAttribute(
        'aria-selected',
        'true',
      );
    });
  });

  describe('Scenario: the strip’s look', () => {
    it('is layout.card.tabStripHeight high, a tab with no MUI width floor and the theme’s 40 px height', () => {
      render(<Host />);
      for (const tab of screen.getAllByRole('tab')) {
        const style = getComputedStyle(tab);
        expect(parseFloat(style.minWidth)).toBe(0);
        expect(px(style.minHeight)).toBe(theme.layout.card.tabStripHeight);
        expect(px(style.paddingLeft)).toBe(theme.layout.card.tabPadX);
        expect(px(style.paddingRight)).toBe(theme.layout.card.tabPadX);
        expect(style.boxSizing).toBe('border-box');
      }
      const list = screen.getByRole('tablist').closest('.MuiTabs-root') as HTMLElement;
      expect(px(getComputedStyle(list).minHeight)).toBe(theme.layout.card.tabStripHeight);
    });

    it('draws 1 px divider lines on the top and the sides of every tab, none at the foot', () => {
      render(<Host />);
      const tab = screen.getByRole('tab', { name: 'История' });
      const style = getComputedStyle(tab);
      expect(px(style.borderTopWidth)).toBe(theme.layout.dividerWidth);
      expect(style.borderTopColor).toBe(rgb(theme.palette.divider));
      expect(px(style.borderLeftWidth)).toBe(theme.layout.dividerWidth);
      expect(style.borderBottomWidth).not.toBe('1px');
    });

    it('sets the selected label in text.primary and the others in text.secondary', () => {
      render(<Host />);
      expect(getComputedStyle(screen.getByRole('tab', { name: 'Детали' })).color).toBe(
        rgb(theme.palette.text.primary),
      );
      expect(getComputedStyle(screen.getByRole('tab', { name: 'История' })).color).toBe(
        rgb(theme.palette.text.secondary),
      );
    });

    it('draws an icon at the bar glyph’s 18 px', () => {
      render(<Host />);
      const label = screen
        .getByRole('tab', { name: 'История' })
        .querySelector('[data-tab-label]') as HTMLElement;
      expect(label.querySelector('svg')).toBeInTheDocument();
    });
  });

  describe('Scenario: the indicator is 2 thick, as wide as the label, 4 above the strip’s bottom', () => {
    function indicator(name: string) {
      return screen
        .getByRole('tab', { name })
        .querySelector('[data-tab-indicator]') as HTMLElement | null;
    }

    it('is drawn on the selected tab only, in the accent', () => {
      render(<Host />);
      expect(indicator('Детали')).toBeInTheDocument();
      expect(indicator('История')).toBeNull();
      expect(getComputedStyle(indicator('Детали') as HTMLElement).backgroundColor).toBe(
        rgb(theme.palette.primary.main),
      );
    });

    it('is layout.frame.thick high with its foot layout.card.tabIndicatorOffset above the bottom', () => {
      render(<Host />);
      const style = getComputedStyle(indicator('Детали') as HTMLElement);
      expect(style.position).toBe('absolute');
      expect(px(style.height)).toBe(theme.layout.frame.thick);
      expect(px(style.bottom)).toBe(theme.layout.card.tabIndicatorOffset);
      expect(theme.layout.card.tabIndicatorOffset + theme.layout.frame.thick).toBeLessThan(
        theme.layout.card.tabStripHeight,
      );
    });

    it('is as wide as the label, not the tab: it spans the label’s own box', () => {
      render(<Host />);
      const mark = indicator('Детали') as HTMLElement;
      const label = mark.parentElement as HTMLElement;
      expect(label).toHaveAttribute('data-tab-label');
      expect(getComputedStyle(label).position).toBe('relative');
      expect(getComputedStyle(mark).left).toBe('0px');
      expect(getComputedStyle(mark).right).toBe('0px');
      // The tab's padding sits outside the label, so the line stops short of the tab's edges.
      expect(label.parentElement).toBe(screen.getByRole('tab', { name: 'Детали' }));
    });

    it('moves with the selection', async () => {
      const user = userEvent.setup();
      render(<Host />);
      await user.click(screen.getByRole('tab', { name: 'История' }));
      expect(indicator('Детали')).toBeNull();
      expect(indicator('История')).toBeInTheDocument();
    });

    it('replaces MUI’s own indicator, which is tab-wide at the bottom', () => {
      const { container } = render(<Host />);
      const own = container.querySelector('.MuiTabs-indicator') as HTMLElement | null;
      if (own) expect(getComputedStyle(own).display).toBe('none');
    });
  });

  describe('Scenario: the strip is not a navigation', () => {
    it('renders buttons with role tab, not links', () => {
      render(<Host />);
      const strip = screen.getByRole('tablist');
      expect(within(strip).queryAllByRole('link')).toHaveLength(0);
      expect(within(strip).getAllByRole('tab')).toHaveLength(TABS.length);
    });
  });
});
