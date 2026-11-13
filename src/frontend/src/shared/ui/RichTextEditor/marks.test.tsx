import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { mountEditor, positionOf, select } from '../../../test/richTextEditor';
import { FONT_SIZES, HIGHLIGHTS, TEXT_COLOURS, normaliseRichText } from '../../lib/richText';
import { DEFAULT_COPY } from './copy';

describe('copy covers the vocabulary', () => {
  it('names every palette token in Russian', () => {
    for (const token of TEXT_COLOURS)
      expect(DEFAULT_COPY.colours[token.name], token.name).toBeTruthy();
    for (const token of HIGHLIGHTS)
      expect(DEFAULT_COPY.highlights[token.name], token.name).toBeTruthy();
    for (const token of FONT_SIZES) expect(DEFAULT_COPY.sizes[token.name], token.name).toBeTruthy();
  });
});

describe('marks (US-5)', () => {
  it('underlines and strikes through, and shows them pressed', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>слово</p>');
    act(() => {
      editor().commands.selectAll();
    });

    await user.click(button('Подчёркнутый'));
    await user.click(button('Зачёркнутый'));

    expect(editor().getHTML()).toBe('<p><u><s>слово</s></u></p>');
    await waitFor(() => expect(button('Подчёркнутый')).toHaveAttribute('aria-pressed', 'true'));
    expect(button('Зачёркнутый')).toHaveAttribute('aria-pressed', 'true');
  });

  it('nests six marks in the canonical order after normalisation', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>слово</p>');
    act(() => {
      editor().commands.selectAll();
    });

    // Applied in an arbitrary order on purpose.
    await user.click(button('Зачёркнутый'));
    await user.click(button('Курсив'));
    await user.click(button('Полужирный'));
    await user.click(button('Подчёркнутый'));

    expect(normaliseRichText(editor().getHTML()).html).toBe(
      '<p><strong><em><u><s>слово</s></u></em></strong></p>',
    );
  });

  it('inline code carries only a link: the other mark controls are disabled inside it', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>a <code>код</code> b</p>');
    select(editor(), positionOf(editor(), 'код') + 1);

    await waitFor(() => expect(button('Полужирный')).toBeDisabled());
    for (const name of [
      'Курсив',
      'Подчёркнутый',
      'Зачёркнутый',
      'Цвет текста',
      'Выделение цветом',
      'Размер шрифта',
      'Очистить форматирование',
    ]) {
      expect(button(name), name).toBeDisabled();
    }
    expect(button('Код в строке')).toBeEnabled();
    expect(button('Код в строке')).toHaveAttribute('aria-pressed', 'true');

    await user.click(button('Код в строке'));
    expect(editor().getHTML()).not.toContain('<code>');
  });

  it('a heading takes no size, and the other marks stay available', async () => {
    const { button, editor } = await mountEditor('<h3>тема</h3>');
    select(editor(), positionOf(editor(), 'тема') + 1);

    await waitFor(() => expect(button('Размер шрифта')).toBeDisabled());
    expect(button('Полужирный')).toBeEnabled();
    expect(button('Цвет текста')).toBeEnabled();
    expect(button('Выделение цветом')).toBeEnabled();
  });
});

describe('palettes (BR-RT-14)', () => {
  async function openMenu(name: string) {
    const user = userEvent.setup();
    const mounted = await mountEditor('<p>слово</p>');
    act(() => {
      mounted.editor().commands.selectAll();
    });
    await user.click(mounted.button(name));
    const menu = await screen.findByRole('menu', { name });
    return { ...mounted, user, menu };
  }

  it('lists the eight colours by name, the reset item, and no free-form control', async () => {
    const { menu } = await openMenu('Цвет текста');

    expect(
      within(menu)
        .getAllByRole('menuitemradio')
        .map((item) => item.textContent),
    ).toEqual([
      'Обычный цвет',
      'Серый',
      'Красный',
      'Оранжевый',
      'Зелёный',
      'Бирюзовый',
      'Синий',
      'Фиолетовый',
      'Розовый',
    ]);
    expect(within(menu).queryByRole('textbox')).not.toBeInTheDocument();
    expect(within(menu).queryByRole('spinbutton')).not.toBeInTheDocument();
  });

  it('applies a colour as a class, checks it, and the reset removes it; focus returns to the body', async () => {
    const { menu, user, editor, button, body } = await openMenu('Цвет текста');

    await user.click(within(menu).getByRole('menuitemradio', { name: 'Красный' }));

    expect(editor().getHTML()).toBe('<p><span class="rt-color-red">слово</span></p>');
    expect(editor().getHTML()).not.toContain('style');
    await waitFor(() => expect(body).toHaveFocus());

    await user.click(button('Цвет текста'));
    const reopened = await screen.findByRole('menu', { name: 'Цвет текста' });
    expect(within(reopened).getByRole('menuitemradio', { name: 'Красный' })).toHaveAttribute(
      'aria-checked',
      'true',
    );

    await user.click(within(reopened).getByRole('menuitemradio', { name: 'Обычный цвет' }));
    expect(editor().getHTML()).toBe('<p>слово</p>');
  });

  it('applies a highlight and a size with their own classes', async () => {
    const first = await openMenu('Выделение цветом');
    await first.user.click(within(first.menu).getByRole('menuitemradio', { name: 'Жёлтый' }));
    expect(first.editor().getHTML()).toBe('<p><mark class="rt-hl-yellow">слово</mark></p>');

    await first.user.click(first.button('Размер шрифта'));
    const sizes = await screen.findByRole('menu', { name: 'Размер шрифта' });
    expect(
      within(sizes)
        .getAllByRole('menuitemradio')
        .map((item) => item.textContent),
    ).toEqual(['Обычный размер', 'Очень мелкий', 'Мелкий', 'Крупный', 'Очень крупный']);
    await first.user.click(within(sizes).getByRole('menuitemradio', { name: 'Крупный' }));

    expect(normaliseRichText(first.editor().getHTML()).html).toBe(
      '<p><mark class="rt-hl-yellow"><span class="rt-size-lg">слово</span></mark></p>',
    );
  });

  it('closes on Escape and returns focus to the opener', async () => {
    const { user, button } = await openMenu('Цвет текста');

    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    expect(button('Цвет текста')).toHaveFocus();
  });
});

describe('clear formatting (US-5)', () => {
  it('removes every mark but the link and makes a heading a paragraph', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor(
      '<h4><strong><u><mark class="rt-hl-red"><span class="rt-color-blue">' +
        '<a href="https://example.org">сайт</a></span></mark></u></strong> текст</h4>',
    );
    act(() => {
      editor().commands.selectAll();
    });

    await user.click(button('Очистить форматирование'));

    expect(editor().getHTML()).toBe(
      '<p><a href="https://example.org" rel="noopener noreferrer">сайт</a> текст</p>',
    );
  });

  it('leaves the list and the table where they are', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor(
      '<ul><li><p><strong>пункт</strong></p></li></ul>' +
        '<table><tbody><tr><td><p><em>ячейка</em></p></td></tr></tbody></table>',
    );
    act(() => {
      editor().commands.selectAll();
    });

    await user.click(button('Очистить форматирование'));

    expect(editor().getHTML()).toBe(
      '<ul><li><p>пункт</p></li></ul>' +
        '<table><tbody><tr><td><p>ячейка</p></td></tr></tbody></table>',
    );
  });
});
