import { describe, expect, it } from 'vitest';
import { describeDefaultAssignment } from './describeDefaultAssignment';

// "Assigning the current default again is a no-op" is a required scenario —
// `LanguagesRegistryPage`'s row action that opens the dialog is hidden on an
// already-default row, so the no-op branch is unreachable through
// `userEvent` without simulating a genuine cross-client race the mocked
// list can't produce on its own. Tested here as the pure decision function
// `handleAssigned` delegates to, rather than left uncovered.
describe('describeDefaultAssignment ("changed" vs "already default, no-op")', () => {
  it('код совпадает с уже известным умолчальным — текст "уже является"', () => {
    expect(describeDefaultAssignment('kk', 'Қазақша', 'kk')).toBe('«Қазақша» уже является языком по умолчанию.');
  });

  it('код отличается от ранее известного умолчального — текст "изменён на"', () => {
    expect(describeDefaultAssignment('kk', 'Қазақша', 'ru')).toBe('Язык по умолчанию изменён на «Қазақша» (kk).');
  });

  it('умолчальный язык ранее не был известен (пустой кэш) — тоже "изменён на", а не выдуманная третья формулировка', () => {
    expect(describeDefaultAssignment('kk', 'Қазақша', undefined)).toBe('Язык по умолчанию изменён на «Қазақша» (kk).');
  });
});
