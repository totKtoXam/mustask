import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';
import { describe, expect, it } from 'vitest';

/**
 * The colour-literal check of BR-1 (mod-015 design § 2.7, plan § 7):
 * `app/theme` is the only source of appearance, so no file under `src/`
 * outside `app/theme/`, the test files (`*.test.*`) and `src/test/` may hold
 * a string literal that is a hex colour or begins with `rgb(`, `rgba(`,
 * `hsl(` or `hsla(`. Adding one to a slice fails `npm test`.
 *
 * Each file is parsed with the TypeScript compiler, so a comment or JSX text
 * («#12» in copy) is never taken for a literal; only string literals,
 * template literals without substitutions and the head of a template with
 * substitutions are read. `#` in an address (`'#main-content'`) is not a hex
 * colour and does not match. An address anchor made only of hex letters
 * (`'#add'`) would match, as BR-1 words the rule; none exists, and one would
 * be renamed rather than exempted — BR-1 keeps no exception list.
 *
 * Dimensions are not machine-checked: code review holds them to theme tokens
 * and the spacing unit (BR-1).
 */

const HEX_COLOUR = /^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;
const FUNCTIONAL_COLOUR = /^(?:rgb|rgba|hsl|hsla)\(/i;

interface Finding {
  line: number;
  literal: string;
}

function isColourLiteral(text: string): boolean {
  return HEX_COLOUR.test(text) || FUNCTIONAL_COLOUR.test(text);
}

function findColourLiterals(fileName: string, source: string): Finding[] {
  const kind = fileName.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const sourceFile = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true, kind);
  const findings: Finding[] = [];

  const visit = (node: ts.Node): void => {
    let text: string | undefined;
    if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) {
      text = node.text;
    } else if (ts.isTemplateExpression(node)) {
      text = node.head.text;
    }
    if (text !== undefined && isColourLiteral(text)) {
      const { line } = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile));
      findings.push({ line: line + 1, literal: text });
    }
    ts.forEachChild(node, visit);
  };
  visit(sourceFile);
  return findings;
}

const SRC = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const EXCLUDED_DIRECTORIES = [path.join(SRC, 'app', 'theme'), path.join(SRC, 'test')];

function isInScope(file: string): boolean {
  if (!/\.tsx?$/.test(file) || /\.test\./.test(path.basename(file))) {
    return false;
  }
  return !EXCLUDED_DIRECTORIES.some((directory) => file.startsWith(directory + path.sep));
}

function listFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const full = path.join(directory, entry);
    return statSync(full).isDirectory() ? listFiles(full) : [full];
  });
}

describe('US-1 Scenario: No colour literal outside the theme (BR-1)', () => {
  describe('the check itself', () => {
    it.each([
      ["const accent = '#0F6CBD';", '#0F6CBD'],
      ['const white = "#fff";', '#fff'],
      ["const veil = 'rgba(0,0,0,.5)';", 'rgba(0,0,0,.5)'],
      ['const hue = `hsl(${h}, 50%, 50%)`;', 'hsl('],
      ['const box = <Box sx={{ color: "#C42B1C" }} />;', '#C42B1C'],
      ["const label = <Box color='#12345678' />;", '#12345678'],
    ])('catches %s', (source, literal) => {
      expect(findColourLiterals('fixture.tsx', source)).toEqual([{ line: 1, literal }]);
    });

    it.each([
      ["const tooShort = '#12';"],
      ["const bare = '#';"],
      ["const anchor = '#main-content';"],
      ['const copy = <p>Задача #12</p>;'],
      ['const copy = <p>#0F6CBD</p>;'],
      ['// #0F6CBD in a comment\nconst x = 1;'],
      ['/* rgba(0, 0, 0, 0.5) */ const y = 2;'],
      ["const word = 'rgb';"],
    ])('lets %s through', (source) => {
      expect(findColourLiterals('fixture.tsx', source)).toEqual([]);
    });
  });

  it('finds no colour literal under src/ outside app/theme/, the tests and src/test/', () => {
    const files = listFiles(SRC).filter(isInScope);
    // Guard against a vacuous pass: the scan must actually see the slices.
    expect(files.some((file) => file.includes(`${path.sep}features${path.sep}`))).toBe(true);

    const findings = files.flatMap((file) =>
      findColourLiterals(file, readFileSync(file, 'utf8')).map(
        ({ line, literal }) => `${path.relative(SRC, file)}:${line} ${literal}`,
      ),
    );

    expect(findings).toEqual([]);
  });
});
