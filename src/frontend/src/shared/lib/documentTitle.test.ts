import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { formatDocumentTitle, useDocumentTitle } from './documentTitle';

describe('US-2 Scenario Outline: One title, and the tab says the same (BR-6) — the tab title', () => {
  it('reads «<title> — Mustask» for a page title', () => {
    expect(formatDocumentTitle('Языки')).toBe('Языки — Mustask');
    expect(formatDocumentTitle('Язык: ru')).toBe('Язык: ru — Mustask');
  });

  it('reads «Mustask» alone without a page title', () => {
    expect(formatDocumentTitle(null)).toBe('Mustask');
  });

  it('sets the tab title while mounted and follows a new title', () => {
    const { rerender } = renderHook(({ title }) => useDocumentTitle(title), {
      initialProps: { title: 'Главная' as string | null | undefined },
    });
    expect(document.title).toBe('Главная — Mustask');

    rerender({ title: 'Добавить язык' });
    expect(document.title).toBe('Добавить язык — Mustask');

    rerender({ title: null });
    expect(document.title).toBe('Mustask');
  });

  it('leaves the tab alone for an undefined title', () => {
    document.title = 'Языки — Mustask';

    renderHook(() => useDocumentTitle(undefined));

    expect(document.title).toBe('Языки — Mustask');
  });
});
