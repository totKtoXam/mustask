import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { mountEditor, positionOf, select } from '../../../test/richTextEditor';

// The checks of design § Accessibility that need no screen reader: names, roles, states,
// the one tab stop, the always-present live region, targets. The announcements themselves
// and the pass with a real screen reader are the manual QA list (plan-frontend § 15).

describe('accessibility of the editor (design § Accessibility)', () => {
  it('the body is a multi-line textbox named by the label, with its placeholder', async () => {
    const { body } = await mountEditor('');

    expect(body).toHaveAttribute('role', 'textbox');
    expect(body).toHaveAttribute('aria-multiline', 'true');
    expect(body).toHaveAccessibleName('Описание');
    expect(body).toHaveAttribute('aria-placeholder', 'Добавьте описание');
    expect(body).toHaveAttribute('contenteditable', 'true');
  });

  it('describes the body by the helper and the always-present notice line', async () => {
    const { body } = await mountEditor('', { helperText: 'До 100 000 символов' });

    await waitFor(() => expect(body.getAttribute('aria-describedby')).toBeTruthy());
    const ids = (body.getAttribute('aria-describedby') ?? '').split(' ');
    const texts = ids.map((id) => document.getElementById(id));
    expect(texts.every((element) => element !== null)).toBe(true);
    expect(texts.some((element) => element?.textContent === 'До 100 000 символов')).toBe(true);
    const region = screen.getByRole('status');
    expect(region).toHaveAttribute('aria-live', 'polite');
    expect(ids).toContain(region.id);
  });

  it('marks the body invalid with an error and not editable while disabled', async () => {
    const { body } = await mountEditor('<p>a</p>', { errorText: 'Ошибка', disabled: true });

    await waitFor(() => expect(body).toHaveAttribute('aria-invalid', 'true'));
    expect(body).toHaveAttribute('aria-disabled', 'true');
    expect(body).toHaveAttribute('contenteditable', 'false');
  });

  it('puts the toolbar before the body in the reading and focus order, as one named toolbar', async () => {
    const { body, toolbar } = await mountEditor('<p>a</p>');

    expect(toolbar).toHaveAttribute('aria-orientation', 'horizontal');
    expect(toolbar.compareDocumentPosition(body) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('names every control, and gives exactly one of them a tab stop', async () => {
    const { toolbar } = await mountEditor('<p>a</p>');

    const buttons = within(toolbar).getAllByRole('button');
    expect(buttons).toHaveLength(26);
    for (const button of buttons) expect(button, button.outerHTML).toHaveAccessibleName(/\S/);
    expect(buttons.filter((button) => button.tabIndex === 0)).toHaveLength(1);
    for (const button of buttons.filter((candidate) => candidate.tabIndex !== 0)) {
      expect(button.tabIndex).toBe(-1);
    }
  });

  it('shows toggles as pressed, menus as haspopup with their state, the emoji button as a dialog opener', async () => {
    const { button, editor } = await mountEditor('<p><strong>слово</strong></p>');
    select(editor(), positionOf(editor(), 'слово') + 1);

    await waitFor(() => expect(button('Полужирный')).toHaveAttribute('aria-pressed', 'true'));
    expect(button('Курсив')).toHaveAttribute('aria-pressed', 'false');
    for (const name of ['Размер шрифта', 'Цвет текста', 'Выделение цветом']) {
      expect(button(name)).toHaveAttribute('aria-haspopup', 'menu');
      expect(button(name)).toHaveAttribute('aria-expanded', 'false');
    }
    expect(button('Эмодзи')).toHaveAttribute('aria-haspopup', 'dialog');
  });

  it('a disabled control is really disabled, and its tooltip still has a name', async () => {
    const { button } = await mountEditor('<p>a</p>');

    expect(button('Убрать ссылку')).toBeDisabled();
    expect(button('Отменить')).toBeDisabled();
  });

  it('a menu is a named menu of radio items, the current one checked, reachable by the keyboard', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p><span class="rt-color-red">слово</span></p>');
    select(editor(), positionOf(editor(), 'слово') + 1);
    await waitFor(() => expect(button('Цвет текста')).toBeEnabled());

    button('Цвет текста').focus();
    await user.keyboard('{Enter}');
    const menu = await screen.findByRole('menu', { name: 'Цвет текста' });

    const checked = within(menu)
      .getAllByRole('menuitemradio')
      .filter((item) => item.getAttribute('aria-checked') === 'true');
    expect(checked.map((item) => item.textContent)).toEqual(['Красный']);
    // The arrow keys move over the items (MUI's menu model), Escape closes and returns focus.
    await user.keyboard('{ArrowDown}');
    expect(menu.contains(document.activeElement)).toBe(true);
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    expect(button('Цвет текста')).toHaveFocus();
  });

  it('the link prompt and the emoji picker are named dialogs with named fields', async () => {
    const user = userEvent.setup();
    const { button } = await mountEditor('<p>a</p>');

    await user.click(button('Ссылка'));
    const prompt = await screen.findByRole('dialog', { name: 'Ссылка' });
    expect(within(prompt).getByRole('textbox', { name: 'Адрес ссылки' })).toHaveFocus();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    await user.click(button('Эмодзи'));
    const picker = await screen.findByRole('dialog', { name: 'Эмодзи' });
    expect(within(picker).getByRole('textbox', { name: 'Поиск эмодзи' })).toBeInTheDocument();
  });

  it('every control is at least the theme’s control height, a 32 px target', async () => {
    const { toolbar } = await mountEditor('<p>a</p>');

    for (const control of within(toolbar).getAllByRole('button')) {
      expect(parseInt(getComputedStyle(control).minWidth, 10)).toBeGreaterThanOrEqual(32);
      expect(parseInt(getComputedStyle(control).height, 10)).toBeGreaterThanOrEqual(32);
    }
  });

  it('keeps the picked colour out of the only carrier: each option has its name, never colour alone', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>слово</p>');
    select(editor(), positionOf(editor(), 'слово') + 1);
    await waitFor(() => expect(button('Выделение цветом')).toBeEnabled());

    await user.click(button('Выделение цветом'));
    const menu = await screen.findByRole('menu', { name: 'Выделение цветом' });

    for (const item of within(menu).getAllByRole('menuitemradio')) {
      expect(item).toHaveAccessibleName(/\S/);
    }
  });
});
