import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { mountEditor } from '../../../test/richTextEditor';
import { normaliseRichText } from '../../lib/richText';
import { fold, searchEmoji } from './toolbar/emoji/search';

/** The picker's emoji buttons: a DOM query, a role query over some 1 600 buttons is slow in jsdom. */
function emojiButtons(dialog: HTMLElement): HTMLElement[] {
  return Array.from(dialog.querySelectorAll<HTMLElement>('button[data-emoji-index]'));
}

describe('emoji data (BR-RT-19, ADR-0039)', () => {
  it('is bundled with Russian names, without flags and the component group', async () => {
    const { emoji, groups } = await import('virtual:rich-text-emoji');

    expect(emoji.length).toBeGreaterThan(1000);
    expect(groups.some((group) => /флаг/i.test(group))).toBe(false);
    expect(groups.some((group) => /компонент/i.test(group))).toBe(false);
    expect(emoji.some(([, label]) => /^флаг/i.test(label))).toBe(false);
    for (const [character, label, , group] of emoji.slice(0, 50)) {
      expect(character.length).toBeGreaterThan(0);
      expect(label).toMatch(/[а-яё]/i);
      expect(groups[group]).toBeTruthy();
    }
  });

  it('searches by folded substring of the label or tags', async () => {
    const { emoji } = await import('virtual:rich-text-emoji');

    expect(fold('Ёлка')).toBe('елка');
    expect(searchEmoji(emoji, 'улыб').length).toBeGreaterThan(0);
    expect(searchEmoji(emoji, 'ЕЛКА').length).toBe(searchEmoji(emoji, 'ёлка').length);
    expect(searchEmoji(emoji, '  ').length).toBe(emoji.length);
    expect(searchEmoji(emoji, 'яяяяяя')).toEqual([]);
  });
});

describe('emoji picker (US-5)', () => {
  async function openPicker() {
    const user = userEvent.setup();
    const mounted = await mountEditor('<p>до</p>');
    await user.click(mounted.body);
    await user.click(mounted.button('Эмодзи'));
    const dialog = await screen.findByRole('dialog', { name: 'Эмодзи' });
    const search = await within(dialog).findByRole('textbox', { name: 'Поиск эмодзи' });
    return { ...mounted, user, dialog, search };
  }

  it('finds a face by name with the keyboard, inserts it as a character, closes and returns focus', async () => {
    const { user, search, editor, body, dialog } = await openPicker();
    await waitFor(() => expect(emojiButtons(dialog).length).toBeGreaterThan(0));

    await user.type(search, 'улыб');
    await waitFor(() => expect(emojiButtons(dialog).length).toBeLessThan(200));
    await user.keyboard('{Enter}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const html = editor().getHTML();
    expect(html).toMatch(/\p{Extended_Pictographic}/u);
    expect(html).not.toContain('<img');
    expect(editor().state.doc.textContent.length).toBeGreaterThan('до'.length);
    expect(normaliseRichText(html).html).toBe(html);
    await waitFor(() => expect(body).toHaveFocus());
  });

  it('moves over the grid with the arrows and back to the search with Up from the first row', async () => {
    const { user, search, dialog } = await openPicker();
    await waitFor(() => expect(emojiButtons(dialog).length).toBeGreaterThan(0));

    await user.type(search, 'улыб');
    await waitFor(() => expect(emojiButtons(dialog).length).toBeLessThan(200));
    await user.keyboard('{ArrowDown}');
    const buttons = emojiButtons(dialog);
    expect(buttons[0]).toHaveFocus();
    expect(buttons[0]?.tabIndex).toBe(0);

    await user.keyboard('{ArrowRight}');
    expect(buttons[1]).toHaveFocus();
    await user.keyboard('{ArrowDown}');
    expect(buttons[9]).toHaveFocus();
    await user.keyboard('{ArrowUp}{ArrowUp}');
    expect(search).toHaveFocus();
  });

  it('picks a focused emoji with Enter', async () => {
    const { user, search, editor, dialog } = await openPicker();
    await waitFor(() => expect(emojiButtons(dialog).length).toBeGreaterThan(0));

    await user.type(search, 'улыб');
    await waitFor(() => expect(emojiButtons(dialog).length).toBeLessThan(200));
    await user.keyboard('{ArrowDown}{ArrowRight}{Enter}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(editor().getHTML()).toMatch(/\p{Extended_Pictographic}/u);
  });

  it('says «Ничего не найдено» when nothing matches', async () => {
    const { user, search, dialog } = await openPicker();

    await user.type(search, 'яяяяяя');

    expect(await within(dialog).findByText('Ничего не найдено')).toBeInTheDocument();
  });

  it('closes on Escape and returns focus to its button, changing nothing', async () => {
    const { user, button, editor } = await openPicker();

    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(button('Эмодзи')).toHaveFocus();
    expect(editor().getHTML()).toBe('<p>до</p>');
  });

  it('counts an inserted emoji as one character (BR-RT-8)', () => {
    expect(normaliseRichText('<p>до😀</p>').visibleLength).toBe(3);
  });
});
