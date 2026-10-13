import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

// tech-029. The files of one worker share a module cache (`isolate: false`), and Vitest
// keeps the result of a `vi.mock` factory per worker: it drops it between files only when
// files are isolated. A second file that mocks the same module would silently get the
// first file's stub and never run its own factory. So a module may be mocked by one file.
// If this fails: share the mock through a helper, or `vi.doMock` it inside the test.

const SRC = resolve(dirname(fileURLToPath(import.meta.url)), '..');

function testFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return testFiles(path);
    return /\.test\.tsx?$/.test(entry.name) ? [path] : [];
  });
}

/** `vi.mock('<specifier>'` calls of a file, each as the module it names. */
function mockedModules(file: string): string[] {
  const source = readFileSync(file, 'utf8');
  const found: string[] = [];
  for (const match of source.matchAll(/\bvi\.mock\(\s*(['"`])([^'"`]+)\1/g)) {
    const specifier = match[2] as string;
    // A relative specifier names a file: compare by the path it resolves to.
    found.push(specifier.startsWith('.') ? relative(SRC, resolve(dirname(file), specifier)) : specifier);
  }
  return found;
}

describe('tech-029: a module is mocked by at most one test file', () => {
  it('has no module that two files mock', () => {
    const owners = new Map<string, Set<string>>();
    for (const file of testFiles(SRC)) {
      for (const module of mockedModules(file)) {
        const files = owners.get(module) ?? new Set<string>();
        files.add(relative(SRC, file));
        owners.set(module, files);
      }
    }
    const shared = [...owners].filter(([, files]) => files.size > 1);

    expect(shared.map(([module, files]) => [module, [...files].sort()])).toEqual([]);
  });

  it('finds the mocks that exist today (the scan itself is alive)', () => {
    const mocked = testFiles(SRC).flatMap(mockedModules);

    expect(mocked.length).toBeGreaterThan(0);
  });
});
