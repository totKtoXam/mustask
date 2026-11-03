import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

// feat-043 step 13 and tech-019: the shell's selectors and top bar are built from the kit, and
// the modules the four selectors replaced are gone for good. A source-reading test, as the kit
// guard is: it reads the files on disk, not the running app.

const SRC = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

function sources(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const full = path.join(directory, name);
    if (statSync(full).isDirectory()) return sources(full);
    return /\.(ts|tsx)$/.test(name) ? [full] : [];
  });
}

const ALL = sources(SRC);
const relative = (file: string) => path.relative(SRC, file).split(path.sep).join('/');

/** The files this task wrote for the shell; they carry no legacy MUI part the kit wraps. */
const KIT_ONLY = [
  'app/shell/ContextSelectors.tsx',
  'app/shell/ContextHost.tsx',
  'app/shell/TopBar.tsx',
];

const LEGACY_PARTS =
  /import\s*\{[^}]*\b(TextField|Button|IconButton|Menu|MenuItem|Snackbar|Alert|Paper|Select|Autocomplete|Table\w*)\b[^}]*\}\s*from\s*'@mui\/material/;

describe('the shell wears the kit (tech-019)', () => {
  it.each(KIT_ONLY)('%s imports no MUI part the kit wraps', (file) => {
    const text = readFileSync(path.join(SRC, file), 'utf8');
    expect(text).not.toMatch(LEGACY_PARTS);
  });

  it.each(KIT_ONLY)('%s styles no MUI inside', (file) => {
    const text = readFileSync(path.join(SRC, file), 'utf8');
    expect(text).not.toMatch(/\.Mui[A-Z]|Mui-[A-Za-z]/);
  });
});

describe('the retired shell modules are gone (feat-043 step 13)', () => {
  const RETIRED_FILES = [
    'app/shell/ContextSwitcher.tsx',
    'app/shell/placeholderContext.ts',
    'app/navigation/switchTarget.ts',
    'app/navigation/NavigationModelContext.ts',
    'app/navigation/projectViews.test.ts',
  ];

  it.each(RETIRED_FILES)('%s does not exist', (file) => {
    expect(ALL.map(relative)).not.toContain(file);
  });

  it('no source imports a retired module', () => {
    const importing = ALL.filter(
      (file) => relative(file) !== 'app/shell/shellSources.test.ts',
    ).filter((file) =>
      /from\s*'[^']*\/(ContextSwitcher|placeholderContext|switchTarget|NavigationModelContext)'/.test(
        readFileSync(file, 'utf8'),
      ),
    );
    expect(importing.map(relative)).toEqual([]);
  });

  it("keeps no feat-040 team level or mod-020 fallback team in the slice's context", () => {
    expect(
      ALL.map(relative).filter((file) => file.startsWith('features/organizations/context/')),
    ).toEqual([]);
  });
});
