// Specs 17-18: the pristine seed state every one of these specs must leave
// the stand in, taken verbatim from `contracts/dictionaries/languages.json` —
// the seeder's own source (`DictionarySeeder.cs`). The seeder inserts only
// what is missing and never updates what exists (ADR-0021 §5), so an edit a
// spec leaves behind survives every future stand restart forever unless the
// spec itself restores it. Kept in one place, not copied into several spec
// files, so a restore in one spec and an assertion in another read the same
// numbers.

export interface SeedLanguage {
  endonym: string;
  sortOrder: number;
  isDefault: boolean;
  isActive: boolean;
}

export const SEED_LANGUAGES: Record<'ru' | 'kk' | 'en', SeedLanguage> = {
  ru: { endonym: 'Русский', sortOrder: 10, isDefault: true, isActive: true },
  kk: { endonym: 'Қазақша', sortOrder: 20, isDefault: false, isActive: true },
  en: { endonym: 'English', sortOrder: 30, isDefault: false, isActive: true },
};
