import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rgb, rulesFor, theme } from '../../../test/kitTesting';
import type { FieldProps } from './fieldTypes';
import { InlineDateField } from './InlineDateField';
import { InlineNumberField } from './InlineNumberField';
import { InlinePickerField } from './InlinePickerField';
import { InlineSelectField } from './InlineSelectField';
import { InlineTextArea } from './InlineTextArea';
import { InlineTextField } from './InlineTextField';
import { PersonField } from './PersonField';

// tech-015 design § 5.7 and § 12 (part 4): one suite, run over the seven fields. Every
// field shares one anatomy — a real label, the value, a hint, an error, the same states,
// the same names — so what is true of one is true of all. What only a kind has is in its
// own test file. jsdom evaluates no `:hover`, no `@media (hover: none)` and no pseudo-element:
// those are asserted as the rules the stylesheet holds (`rulesFor`), and the showcase E2E
// reads the computed look in a browser.

const LABEL = 'Поле';
const noop = () => {};

type Extra = Partial<FieldProps>;

interface Kind {
  name: string;
  /** Renders the field, empty or `filled`, with the shared props on top. */
  render: (props: Extra, filled: boolean) => ReactElement;
  /** A control a `<label for>` names (all but the select, a div). */
  labelled: boolean;
  /** The control takes a `placeholder` attribute (all but the select). */
  placeholder: boolean;
  multiline?: boolean;
  /** The field draws its own start adornment (a person's avatar), so a host's is not taken. */
  ownsAdornment?: boolean;
  /** Does something a user can see when the control is asked to open. */
  opens?: () => HTMLElement | null;
}

const options = ['Первый', 'Второй'];

const KINDS: readonly Kind[] = [
  {
    name: 'InlineTextField',
    labelled: true,
    placeholder: true,
    render: (props, filled) => (
      <InlineTextField label={LABEL} value={filled ? 'Письмо' : ''} onChange={noop} {...props} />
    ),
  },
  {
    name: 'InlineTextArea',
    labelled: true,
    placeholder: true,
    multiline: true,
    render: (props, filled) => (
      <InlineTextArea
        label={LABEL}
        value={filled ? 'Письмо со ссылкой' : ''}
        onChange={noop}
        {...(props as Omit<Extra, 'size'>)}
      />
    ),
  },
  {
    name: 'InlineNumberField',
    labelled: true,
    placeholder: true,
    render: (props, filled) => (
      <InlineNumberField
        label={LABEL}
        inputMode="decimal"
        value={filled ? '3,5' : ''}
        onChange={noop}
        {...props}
      />
    ),
  },
  {
    name: 'InlineDateField',
    labelled: true,
    placeholder: true,
    render: (props, filled) => (
      <InlineDateField
        label={LABEL}
        value={filled ? '2026-10-12' : ''}
        onChange={noop}
        {...props}
      />
    ),
  },
  {
    name: 'InlineSelectField',
    labelled: false,
    placeholder: false,
    opens: () => screen.queryByRole('listbox'),
    render: (props, filled) => (
      <InlineSelectField
        label={LABEL}
        value={filled ? 'a' : ''}
        options={[
          { value: 'a', label: 'Первый' },
          { value: 'b', label: 'Второй' },
        ]}
        onChange={noop}
        {...props}
      />
    ),
  },
  {
    name: 'InlinePickerField',
    labelled: true,
    placeholder: true,
    opens: () => screen.queryByRole('listbox'),
    render: (props, filled) => (
      <InlinePickerField
        label={LABEL}
        value={filled ? 'Первый' : null}
        options={options}
        getOptionLabel={(option) => option}
        onChange={noop}
        {...props}
      />
    ),
  },
  {
    name: 'PersonField',
    labelled: true,
    placeholder: true,
    ownsAdornment: true,
    opens: () => screen.queryByRole('listbox'),
    render: (props, filled) => (
      <PersonField
        label={LABEL}
        value={filled ? 'Первый' : null}
        options={options}
        getOptionLabel={(option) => option}
        onChange={noop}
        {...props}
      />
    ),
  },
];

const control = () => screen.getByLabelText(new RegExp(`^${LABEL}`)) as HTMLElement;
const root = () => document.querySelector('[data-field]') as HTMLElement;
const box = () => root().querySelector('[data-field-box]') as HTMLElement;
const labelElement = () => root().querySelector('label') as HTMLLabelElement;
const isDisabled = (element: HTMLElement) =>
  element.hasAttribute('disabled') || element.getAttribute('aria-disabled') === 'true';

describe.each(KINDS)('tech-015 Scenario: $name — the field family (design § 5.7)', (kind) => {
  describe('Scenario: names', () => {
    it('is named by its label only — the shown value is not part of the name', () => {
      render(kind.render({}, true));
      expect(control()).toHaveAccessibleName(LABEL);
    });

    it('has a real label: `for` the control, or `aria-labelledby` for a select', () => {
      render(kind.render({}, false));
      const label = labelElement();
      expect(label.tagName).toBe('LABEL');
      expect(label).toHaveTextContent(LABEL);
      if (kind.labelled) {
        expect(label.htmlFor).toBe(control().id);
      } else {
        expect(label.htmlFor).toBe('');
        expect(control().getAttribute('aria-labelledby')).toBe(label.id);
      }
    });

    it('keeps the host’s id on the control and derives the others from it', () => {
      render(kind.render({ id: 'my-field', hint: 'Подсказка' }, false));
      expect(control().id).toBe('my-field');
      expect(root().querySelector('#my-field-label')).toBe(labelElement());
      expect(document.getElementById('my-field-hint')).toHaveTextContent('Подсказка');
    });
  });

  describe('Scenario: required', () => {
    it('draws an asterisk that is hidden from assistive technology, so the name stays the label', () => {
      render(kind.render({ required: true }, false));
      expect(labelElement()).toHaveTextContent(`${LABEL} *`);
      expect(labelElement().querySelector('[aria-hidden="true"]')).toHaveTextContent('*');
      expect(control()).toHaveAccessibleName(LABEL);
    });

    it('is aria-required and never the native attribute', () => {
      render(kind.render({ required: true }, false));
      expect(control()).toHaveAttribute('aria-required', 'true');
      expect(root().querySelector('[required]')).toBeNull();
      expect(root().querySelector('[aria-required="true"]')).toBe(control());
    });

    it('draws nothing when it is not required', () => {
      render(kind.render({}, false));
      expect(labelElement()).not.toHaveTextContent('*');
      expect(control()).not.toHaveAttribute('aria-required');
    });
  });

  describe('Scenario: hint', () => {
    it('is the control’s description, on the label’s line, and stays in the DOM', () => {
      render(kind.render({ hint: 'От 0 до 100' }, false));
      expect(control()).toHaveAccessibleDescription('От 0 до 100');
      const hint = root().querySelector('[data-field-hint]') as HTMLElement;
      expect(hint).toHaveTextContent('От 0 до 100');
      expect(hint.parentElement).toBe(labelElement().parentElement);
    });

    it('is drawn only while the field has focus: hidden at rest, visible on `:focus-within`', () => {
      render(kind.render({ hint: 'От 0 до 100' }, false));
      const hint = root().querySelector('[data-field-hint]') as HTMLElement;
      expect(getComputedStyle(hint).visibility).toBe('hidden');
      const focused = rulesFor(root(), ':focus-within [data-field-hint]');
      expect(focused.some((rule) => rule.declarations.visibility === 'visible')).toBe(true);
    });

    it('is one line with an ellipsis, never wrapping or pushing the label', () => {
      render(
        kind.render({ hint: 'Очень длинная подсказка, которой не хватит места на строке' }, false),
      );
      const element = root().querySelector('[data-field-hint]') as HTMLElement;
      const hint = getComputedStyle(element);
      expect(hint.whiteSpace).toBe('nowrap');
      expect(hint.overflow).toBe('hidden');
      expect(hint.textOverflow).toBe('ellipsis');
      expect(parseFloat(hint.minWidth)).toBe(0);
      expect(hint.textAlign).toBe('right');
      // `flex: 1 1 0` — the hint takes the rest of the line — against the label's `0 1 auto`
      // (jsdom does not expand the shorthand: read the rule).
      const flexOf = (target: HTMLElement) =>
        rulesFor(target, '').find((rule) => rule.declarations.flex)?.declarations.flex;
      expect(flexOf(element)).toBe('1 1 0');
      expect(flexOf(labelElement())).toBe('0 1 auto');
    });

    it('has no description when there is no hint', () => {
      render(kind.render({}, false));
      expect(control()).not.toHaveAttribute('aria-describedby');
    });
  });

  describe('Scenario: error', () => {
    it('is aria-invalid, an alert line under the box, and the description', () => {
      render(kind.render({ error: 'Введите число' }, true));
      expect(control()).toHaveAttribute('aria-invalid', 'true');
      const line = screen.getByRole('alert');
      expect(line).toHaveTextContent('Введите число');
      expect(control()).toHaveAccessibleDescription('Введите число');
      expect(box().compareDocumentPosition(line) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it('replaces the hint as the description while it is set, and the hint comes back with it', () => {
      const { rerender } = render(
        kind.render({ hint: 'От 0 до 100', error: 'Введите число' }, true),
      );
      expect(control()).toHaveAccessibleDescription('Введите число');
      expect(control().getAttribute('aria-describedby')).not.toContain('hint');
      rerender(kind.render({ hint: 'От 0 до 100' }, true));
      expect(control()).toHaveAccessibleDescription('От 0 до 100');
      expect(screen.queryByRole('alert')).toBeNull();
      expect(control()).not.toHaveAttribute('aria-invalid');
    });

    it('draws the error line in the error colour at the caption type', () => {
      render(kind.render({ error: 'Введите число' }, true));
      const style = getComputedStyle(screen.getByRole('alert'));
      expect(style.color).toBe(rgb(theme.palette.error.main));
      expect(px(style.fontSize)).toBe(12);
    });
  });

  describe('Scenario: readOnly', () => {
    it('is reachable by Tab and says it is read-only', async () => {
      const user = userEvent.setup();
      render(kind.render({ readOnly: true }, true));
      await user.tab();
      expect(control()).toHaveFocus();
      expect(control()).toHaveAttribute('aria-readonly', 'true');
    });

    it('refuses input: no change, nothing opens', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(kind.render({ readOnly: true, onChange } as Extra, true));
      await user.tab();
      await user.keyboard('x{ArrowDown}{Enter}');
      expect(onChange).not.toHaveBeenCalled();
      expect(kind.opens?.() ?? null).toBeNull();
    });
  });

  describe('Scenario: busy', () => {
    it('is the read-only look with aria-busy, edits refused and the text colour unchanged', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      const { unmount } = render(kind.render({}, true));
      const idle = getComputedStyle(control()).color;
      unmount();
      render(kind.render({ busy: true, onChange } as Extra, true));
      expect(control()).toHaveAttribute('aria-busy', 'true');
      expect(control()).toHaveAttribute('aria-readonly', 'true');
      expect(getComputedStyle(control()).color).toBe(idle);
      expect(getComputedStyle(control()).color).not.toBe(rgb(theme.palette.text.disabled));
      await user.tab();
      await user.keyboard('x{ArrowDown}{Enter}');
      expect(onChange).not.toHaveBeenCalled();
      expect(kind.opens?.() ?? null).toBeNull();
    });

    it('shows a spinner at the end of the box, hidden from assistive technology', () => {
      render(kind.render({ busy: true }, true));
      const spinner = box().querySelector('.MuiCircularProgress-root') as HTMLElement;
      expect(spinner).not.toBeNull();
      expect(spinner.closest('[aria-hidden="true"]')).not.toBeNull();
      expect(px(getComputedStyle(spinner).width)).toBe(theme.layout.inlineSpinner);
    });
  });

  describe('Scenario: disabled', () => {
    it('is native-disabled where the value can never be edited, and out of the Tab order', async () => {
      const user = userEvent.setup();
      render(kind.render({ disabled: true }, true));
      expect(isDisabled(control())).toBe(true);
      await user.tab();
      expect(control()).not.toHaveFocus();
    });
  });

  describe('Scenario: a hidden label still names the control', () => {
    it('draws no label of its own, and the control keeps its name', () => {
      render(kind.render({ labelPlacement: 'hidden' }, true));
      expect(control()).toHaveAccessibleName(LABEL);
      const style = getComputedStyle(labelElement());
      expect(style.position).toBe('absolute');
      expect(style.width).toBe('1px');
      expect(style.height).toBe('1px');
      expect(style.overflow).toBe('hidden');
    });

    it('draws the label visibly above the box in every other placement', () => {
      render(kind.render({}, true));
      expect(getComputedStyle(labelElement()).position).not.toBe('absolute');
    });
  });

  describe('Scenario: no state changes the size', () => {
    it('keeps the box as high as it was whether the field is empty, filled, read-only, busy or in error', () => {
      const heights: string[] = [];
      for (const [props, filled] of [
        [{}, false],
        [{}, true],
        [{ readOnly: true }, true],
        [{ busy: true }, true],
        [{ error: 'Ошибка' }, true],
        [{ frame: 'always' as const }, true],
      ] as [Extra, boolean][]) {
        const { unmount } = render(kind.render(props, filled));
        const style = getComputedStyle(box());
        heights.push(kind.multiline ? style.minHeight : style.height);
        unmount();
      }
      expect(new Set(heights).size).toBe(1);
      expect(px(heights[0] as string)).toBe(theme.layout.field.box);
    });

    it('is label 16 + gap + box 28 + gap = 48 empty or filled, in the top placement', () => {
      const { field } = theme.layout;
      expect(16 + field.gap + field.box + field.gap).toBe(48);
      render(kind.render({}, false));
      const label = root().querySelector('[data-field-label-row]') as HTMLElement;
      expect(px(getComputedStyle(label).height)).toBe(16);
      expect(getComputedStyle(label).marginBottom).toBe(`${field.gap}px`);
      expect(getComputedStyle(box()).marginBottom).toBe(`${field.gap}px`);
    });

    it('draws its frame as an inset shadow, so a frame never changes the box', () => {
      render(kind.render({ frame: 'always' }, true));
      const base = rulesFor(root(), 'data-frame-part="whole"').filter((rule) => rule.media === '');
      expect(base.length).toBeGreaterThan(0);
      expect(base[0]?.declarations['box-shadow']).toMatch(/^inset /);
      // The frame takes no layout: the box has no border.
      expect(getComputedStyle(box()).borderTopWidth).not.toBe('1px');
    });
  });

  describe('Scenario: the frame', () => {
    const frameOf = (state: string, part = 'whole') =>
      rulesFor(root(), `${state}`).filter((rule) =>
        rule.selector.includes(`data-frame-part="${part}"`),
      );

    it('hover frame: no frame at rest, a 1 px shell.fieldHover frame on hover, a 2 px accent one on focus', () => {
      render(kind.render({}, true));
      const rest = rulesFor(root(), 'data-frame-part="whole"').find((rule) => rule.media === '');
      expect(rest?.declarations['box-shadow']).toBe('none');
      const hover = rulesFor(root(), ':hover [data-frame-part="whole"]')[0];
      expect(hover?.declarations['box-shadow']).toContain(theme.palette.shell.fieldHover);
      expect(hover?.declarations['box-shadow']).toContain(`${theme.layout.frame.thin}px`);
      const focus = rulesFor(root(), ':focus-within [data-frame-part="whole"]')[0];
      expect(focus?.declarations['box-shadow']).toContain(theme.palette.primary.main);
      expect(focus?.declarations['box-shadow']).toContain(`${theme.layout.frame.thick}px`);
      expect(frameOf(':hover').length).toBeGreaterThan(0);
    });

    it('always frame: a 1 px inputBorder frame at rest, text.primary on hover', () => {
      render(kind.render({ frame: 'always' }, true));
      const rest = rulesFor(root(), 'data-frame-part="whole"').find((rule) => rule.media === '');
      expect(rest?.declarations['box-shadow']).toContain(theme.palette.shell.inputBorder);
      const hover = rulesFor(root(), ':hover [data-frame-part="whole"]')[0];
      expect(hover?.declarations['box-shadow']).toContain(theme.palette.text.primary);
    });

    it('error frame: error.main at rest and on hover (1 px), 2 px on focus', () => {
      render(kind.render({ error: 'Ошибка' }, true));
      const rest = rulesFor(root(), 'data-frame-part="whole"').find((rule) => rule.media === '');
      expect(rest?.declarations['box-shadow']).toContain(theme.palette.error.main);
      const focus = rulesFor(root(), ':focus-within [data-frame-part="whole"]')[0];
      expect(focus?.declarations['box-shadow']).toContain(theme.palette.error.main);
      expect(focus?.declarations['box-shadow']).toContain(`${theme.layout.frame.thick}px`);
    });

    it('shows the hover look at rest when the pointer cannot hover (hover frame only)', () => {
      render(kind.render({}, true));
      const touch = rulesFor(root(), 'data-frame-part="whole"').filter((rule) =>
        rule.media.includes('hover: none'),
      );
      expect(touch.length).toBeGreaterThan(0);
      expect(touch[0]?.declarations['box-shadow']).toContain(theme.palette.shell.fieldHover);
    });

    it('draws no hover frame on a field that cannot be edited', () => {
      render(kind.render({ readOnly: true }, true));
      expect(rulesFor(root(), ':hover [data-frame-part="whole"]')).toHaveLength(0);
    });
  });

  describe('Scenario: emptyText', () => {
    it('reads at rest while the field is empty, in text.secondary, and never to assistive technology', () => {
      render(kind.render({ emptyText: 'Не указано' }, false));
      const text = root().querySelector('[data-empty-text]') as HTMLElement;
      expect(text).toHaveTextContent('Не указано');
      expect(text).toHaveAttribute('aria-hidden', 'true');
      expect(getComputedStyle(text).color).toBe(rgb(theme.palette.text.secondary));
      expect(control()).not.toHaveAccessibleDescription('Не указано');
    });

    it('is gone once there is a value', () => {
      render(kind.render({ emptyText: 'Не указано' }, true));
      expect(root().querySelector('[data-empty-text]')).toBeNull();
    });

    it('gives way to focus', () => {
      render(kind.render({ emptyText: 'Не указано' }, false));
      const hiding = rulesFor(root(), ':focus-within [data-empty-text]');
      expect(hiding.some((rule) => rule.declarations.visibility === 'hidden')).toBe(true);
    });
  });

  describe('Scenario: the value’s text', () => {
    it('is the input’s own text at the type of the size', () => {
      render(kind.render({}, true));
      expect(
        px(getComputedStyle(root().querySelector('.MuiInputBase-root') as HTMLElement).fontSize),
      ).toBe(14);
    });

    it('starts `textInset` in from the box, or `textAreaInset` for a text area', () => {
      render(kind.render({}, true));
      const style = getComputedStyle(box());
      const inset = kind.multiline
        ? theme.layout.field.textAreaInset
        : theme.layout.field.textInset;
      expect(px(style.paddingLeft)).toBe(inset);
      expect(px(style.paddingRight)).toBe(inset);
    });

    it('is as wide as its container: it sets no width of its own', () => {
      render(kind.render({}, true));
      expect(getComputedStyle(root()).width).toBe('100%');
    });
  });

  if (kind.placeholder) {
    describe('Scenario: the placeholder', () => {
      const ATTRIBUTE = 'Не оценено';

      it.each([
        ['at rest', {}],
        ['with frame="always"', { frame: 'always' as const }],
        ['when read-only', { readOnly: true }],
        ['when busy', { busy: true }],
        ['in error', { error: 'Ошибка' }],
      ] as [string, Extra][])('is the input’s own placeholder attribute %s', (_state, props) => {
        render(kind.render({ placeholder: ATTRIBUTE, ...props }, false));
        expect(control()).toHaveAttribute('placeholder', ATTRIBUTE);
      });

      it('is never the label and never the name', () => {
        render(kind.render({ placeholder: ATTRIBUTE }, false));
        expect(control()).toHaveAccessibleName(LABEL);
        expect(control()).not.toHaveAccessibleDescription(ATTRIBUTE);
      });

      it('has a transparent look at rest in the hover frame, shown on hover, on focus and with no hover', () => {
        render(kind.render({ placeholder: ATTRIBUTE }, false));
        const rest = rulesFor(root(), 'input::placeholder').find((rule) => rule.media === '');
        expect(rest?.declarations.opacity).toBe('0');
        for (const state of [':hover input::placeholder', ':focus-within input::placeholder']) {
          expect(rulesFor(root(), state)[0]?.declarations.opacity).toBe('1');
        }
        const touch = rulesFor(root(), 'input::placeholder').find((rule) =>
          rule.media.includes('hover: none'),
        );
        expect(touch?.declarations.opacity).toBe('1');
      });

      it('shows its text with frame="always", where a blank field needs it', () => {
        render(kind.render({ placeholder: ATTRIBUTE, frame: 'always' }, false));
        const rest = rulesFor(root(), 'input::placeholder').find((rule) => rule.media === '');
        expect(rest?.declarations.opacity).toBe('1');
        expect(rest?.declarations.color).toBe(theme.palette.text.secondary);
      });
    });
  }

  describe('Scenario: the band — labelPlacement="start"', () => {
    const LABEL_WIDTH = theme.layout.card.bandLabelWidth;
    const dot = (
      <span data-testid="dot" style={{ display: 'inline-block', width: 10, height: 10 }} />
    );

    it('puts the label in a column `bandLabelWidth` wide and the box beside it, 32 high', () => {
      render(kind.render({ labelPlacement: 'start' }, true));
      const cell = root().querySelector('[data-field-label-cell]') as HTMLElement;
      expect(getComputedStyle(root()).display).toBe('grid');
      expect(getComputedStyle(root()).gridTemplateColumns).toBe(`${LABEL_WIDTH}px minmax(0, 1fr)`);
      expect(cell).toContainElement(labelElement());
      expect(px(getComputedStyle(root()).minHeight)).toBe(theme.layout.card.bandRow);
      expect(getComputedStyle(labelElement()).textOverflow).toBe('ellipsis');
    });

    it('takes the column from `labelWidth`', () => {
      render(kind.render({ labelPlacement: 'start', labelWidth: 120 }, true));
      expect(getComputedStyle(root()).gridTemplateColumns).toBe('120px minmax(0, 1fr)');
    });

    // A person field draws its own avatar or glyph in the box, so a dot cannot be handed to it.
    const own = kind.ownsAdornment === true;
    const inset = kind.multiline ? theme.layout.field.textAreaInset : theme.layout.field.textInset;

    it.skipIf(own)(
      'with no adornment, every value’s text stands `bandLabelWidth` + `textInset` = 88 from the label’s edge',
      () => {
        render(kind.render({ labelPlacement: 'start' }, true));
        expect(LABEL_WIDTH + px(getComputedStyle(box()).paddingLeft)).toBe(LABEL_WIDTH + inset);
        if (!kind.multiline) expect(LABEL_WIDTH + inset).toBe(88);
        expect(root().querySelector('[data-field-adornment]')).toBeNull();
        expect(box().getAttribute('data-frame-part')).toBe('whole');
      },
    );

    it.skipIf(own)(
      'stands the adornment in the label column by default, and the box takes it in',
      () => {
        render(
          kind.render({ labelPlacement: 'start', startAdornment: dot, frame: 'always' }, true),
        );
        const cell = root().querySelector('[data-field-label-cell]') as HTMLElement;
        const lead = root().querySelector('[data-frame-part="lead"]') as HTMLElement;
        expect(cell).toContainElement(screen.getByTestId('dot'));
        expect(lead).toContainElement(screen.getByTestId('dot'));
        expect(box().getAttribute('data-frame-part')).toBe('rest');
        // The frame is two halves that meet: the lead draws its left, top and bottom sides, the box the rest.
        const shadows = rulesFor(root(), 'data-frame-part="lead"')[0]?.declarations['box-shadow'];
        expect(shadows?.split('inset ').length).toBe(4);
      },
    );

    it.skipIf(own)(
      'keeps the text at 88 with the adornment: its right edge 19 before the text, a 10 px dot’s centre at 64, the box 6 before it',
      () => {
        render(kind.render({ labelPlacement: 'start', startAdornment: dot }, true));
        const { field, mark } = theme.layout;
        const lead = getComputedStyle(
          root().querySelector('[data-frame-part="lead"]') as HTMLElement,
        );
        const leadPadLeft = px(lead.paddingLeft);
        const leadPadRight = px(lead.paddingRight);
        const dotWidth = 10;
        // The lead ends where the label column does: its right edge at `bandLabelWidth`.
        const adornmentRight = LABEL_WIDTH - leadPadRight;
        const textX = LABEL_WIDTH + px(getComputedStyle(box()).paddingLeft);
        expect(textX).toBe(LABEL_WIDTH + inset);
        // The gap is the reference's 19 whatever the box's own inset is.
        expect(textX - adornmentRight).toBe(field.adornmentGap);
        expect(leadPadLeft).toBe(field.textInset);
        // The frame begins `textInset` before the adornment's left edge.
        expect(LABEL_WIDTH - leadPadRight - dotWidth - leadPadLeft).toBe(
          textX - field.adornmentGap - dotWidth - field.textInset,
        );
        expect(mark.categoryDotField).toBe(dotWidth);
        if (!kind.multiline) {
          // The single line's numbers, as the reference sets them.
          expect(textX).toBe(88);
          expect(adornmentRight - dotWidth / 2).toBe(64);
          expect(LABEL_WIDTH - leadPadRight - dotWidth - leadPadLeft).toBe(53);
        }
      },
    );

    it.skipIf(own)('stands the adornment inside the box when told to', () => {
      render(
        kind.render(
          { labelPlacement: 'start', startAdornment: dot, adornmentPlacement: 'value' },
          true,
        ),
      );
      expect(box()).toContainElement(screen.getByTestId('dot'));
      expect(root().querySelector('[data-frame-part="lead"]')).toBeNull();
      expect(box().getAttribute('data-frame-part')).toBe('whole');
    });

    it.skipIf(own)(
      'stands the adornment inside the box in the top placement, `nameGap` before the text',
      () => {
        render(kind.render({ startAdornment: dot }, true));
        const adornment = root().querySelector('[data-field-adornment]') as HTMLElement;
        expect(box()).toContainElement(adornment);
        expect(getComputedStyle(adornment).marginRight).toBe(`${theme.layout.mark.nameGap}px`);
      },
    );

    it('floats the hint under the value on an opaque ground', () => {
      render(kind.render({ labelPlacement: 'start', hint: 'Подсказка' }, true));
      const hint = getComputedStyle(root().querySelector('[data-field-hint]') as HTMLElement);
      expect(hint.position).toBe('absolute');
      expect(hint.top).toBe('100%');
      expect(hint.left).toBe(`${LABEL_WIDTH}px`);
      expect(hint.backgroundColor).toBe(rgb(theme.palette.background.paper));
      expect(Number(hint.zIndex)).toBeGreaterThan(0);
    });
  });
});
