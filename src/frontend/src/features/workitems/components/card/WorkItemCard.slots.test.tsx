import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { cardTexts } from '../../lib/cardTexts';
import type { WorkItemCardSlots } from './cardSlots';
import { openCard } from './testCard';

// Each user action draws the kit’s fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 60_000 });

/** The words of the reference’s parts that the product does not have yet (BR-C-18). */
const WORDS_OF_PLACES_KEPT = [
  'Следить',
  'Причина',
  'Итерация',
  'Обсуждение',
  'Развёртывание',
  'Разработка',
  'История',
  'Вложения',
  'Связи',
  'Обновил',
  'Теги',
  'Комментарии',
];

/** A probe in each slot that takes a node. */
const PROBES = {
  commentsCount: 'probe-comments',
  tags: 'probe-tags',
  follow: 'probe-follow',
  updatedBy: 'probe-updated-by',
  reason: 'probe-reason',
  iteration: 'probe-iteration',
  discussion: 'probe-discussion',
  deployment: 'probe-deployment',
  development: 'probe-development',
} as const;

const probe = (id: string) => screen.getByTestId(id);

/** Whether `later` comes after `earlier` in the document. */
function follows(later: Element, earlier: Element): boolean {
  return Boolean(earlier.compareDocumentPosition(later) & Node.DOCUMENT_POSITION_FOLLOWING);
}

describe('feat-029 BR-C-18: the places kept', () => {
  it('draw nothing and no word of theirs while their tasks are not done', async () => {
    await openCard('MSK-US-2');

    for (const word of WORDS_OF_PLACES_KEPT) {
      expect(screen.queryByText(new RegExp(word))).not.toBeInTheDocument();
    }
    expect(screen.queryByTestId(/^probe-/)).not.toBeInTheDocument();
    // The tab strip holds the one tab the product has.
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      cardTexts.tabs.details,
    ]);
    // Nothing disabled stands in for a place: the only disabled controls are the clean ones.
    const disabled = screen
      .getAllByRole('button')
      .filter((button) => button.hasAttribute('disabled'))
      .map((button) => button.getAttribute('aria-label') ?? button.textContent);
    expect(disabled.sort()).toEqual(
      ['Отменить все изменения', 'Сохранить', 'Ещё варианты сохранения'].sort(),
    );
  });

  it('are drawn where the design puts each, once their owners pass a node', async () => {
    const slots: WorkItemCardSlots = {
      ...Object.fromEntries(
        Object.entries(PROBES).map(([slot, id]) => [slot, <span key={id} data-testid={id} />]),
      ),
      tabs: [
        {
          tab: { value: 'history', label: 'История' },
          panel: <span data-testid="probe-history" />,
        },
      ],
    };
    await openCard('MSK-US-2', { card: { slots } });

    for (const id of Object.values(PROBES)) {
      expect(probe(id)).toBeInTheDocument();
    }
    const assignee = screen.getByRole('combobox', { name: 'Исполнитель' });
    const saveButton = screen.getByRole('button', { name: 'Сохранить' });
    const state = screen.getByRole('combobox', { name: 'Статус' });
    const area = screen.getByRole('combobox', { name: 'Область' });
    const detailsTab = screen.getByRole('tab', { name: cardTexts.tabs.details });
    const historyTab = screen.getByRole('tab', { name: 'История' });
    const related = screen.getByRole('region', { name: cardTexts.relatedWork });
    const lastText = screen.getByRole('region', { name: 'Критерии приёмки' });

    // Header row 3: the assignee, the comments count, the tags; then «Следить» before the Save group.
    expect(follows(probe(PROBES.commentsCount), assignee)).toBe(true);
    expect(follows(probe(PROBES.tags), probe(PROBES.commentsCount))).toBe(true);
    expect(follows(saveButton, probe(PROBES.follow))).toBe(true);
    expect(follows(probe(PROBES.follow), probe(PROBES.tags))).toBe(true);
    // The band: «Причина» under «Статус», «Итерация» under «Область»; the stamp replaces «Изменён».
    expect(follows(probe(PROBES.reason), state)).toBe(true);
    expect(follows(area, probe(PROBES.reason))).toBe(true);
    expect(follows(probe(PROBES.iteration), area)).toBe(true);
    expect(screen.queryByText(/^Изменён: /)).not.toBeInTheDocument();
    expect(probe(PROBES.updatedBy)).toBeInTheDocument();
    // The body: «Обсуждение» under the texts; the two places above «Связанная работа».
    expect(follows(probe(PROBES.discussion), lastText)).toBe(true);
    expect(related.contains(probe(PROBES.deployment))).toBe(false);
    expect(follows(related, probe(PROBES.deployment))).toBe(true);
    expect(follows(probe(PROBES.development), probe(PROBES.deployment))).toBe(true);
    expect(follows(related, probe(PROBES.development))).toBe(true);
    // The tabs: «История» stands before «Детали», and opens its own panel.
    expect(follows(detailsTab, historyTab)).toBe(true);
    expect(screen.queryByTestId('probe-history')).not.toBeInTheDocument();
    await userEvent.click(historyTab);
    expect(probe('probe-history')).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Описание' })).not.toBeInTheDocument();
  });
});
