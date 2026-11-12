import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';
import { describe, expect, it } from 'vitest';

/**
 * Stored html is never assigned to the page (spec NFR Safety, BR-RT-12,
 * ADR-0039 "mXSS"): no `dangerouslySetInnerHTML` attribute, no write of
 * `innerHTML`, `outerHTML` or `insertAdjacentHTML`, and no `document.write`
 * anywhere under `src/` outside the test files. `react/no-danger` is not in the
 * stack (plan-frontend F-4); ESLint's `no-restricted-syntax` and
 * `no-restricted-properties` say the same at edit time, this scan is the
 * `npm test` side, in the style of `colourLiterals.test.ts`.
 *
 * A read of `innerHTML` is not told apart from a write by this scan, and none
 * is allowed in the product code either: html comes out of the editor through
 * its schema, never through the DOM's string setters.
 */

const FORBIDDEN_PROPERTIES = new Set(['innerHTML', 'outerHTML', 'insertAdjacentHTML']);

interface Finding {
  line: number;
  what: string;
}

function findRawHtml(fileName: string, source: string): Finding[] {
  const kind = fileName.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const sourceFile = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true, kind);
  const findings: Finding[] = [];

  const report = (node: ts.Node, what: string): void => {
    const { line } = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile));
    findings.push({ line: line + 1, what });
  };

  const visit = (node: ts.Node): void => {
    if (ts.isJsxAttribute(node) && node.name.getText(sourceFile) === 'dangerouslySetInnerHTML') {
      report(node, 'dangerouslySetInnerHTML');
    } else if (ts.isPropertyAccessExpression(node) && FORBIDDEN_PROPERTIES.has(node.name.text)) {
      report(node, node.name.text);
    } else if (
      ts.isElementAccessExpression(node) &&
      ts.isStringLiteralLike(node.argumentExpression) &&
      FORBIDDEN_PROPERTIES.has(node.argumentExpression.text)
    ) {
      report(node, node.argumentExpression.text);
    } else if (
      ts.isPropertyAccessExpression(node) &&
      node.name.text === 'write' &&
      ts.isIdentifier(node.expression) &&
      node.expression.text === 'document'
    ) {
      report(node, 'document.write');
    }
    ts.forEachChild(node, visit);
  };
  visit(sourceFile);
  return findings;
}

const SRC = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function productSources(directory: string): string[] {
  const result: string[] = [];
  for (const entry of readdirSync(directory)) {
    const full = path.join(directory, entry);
    if (statSync(full).isDirectory()) {
      if (entry === 'test') continue;
      result.push(...productSources(full));
    } else if (
      /\.(ts|tsx)$/.test(entry) &&
      !/\.test\.(ts|tsx)$/.test(entry) &&
      !entry.endsWith('.d.ts')
    ) {
      result.push(full);
    }
  }
  return result;
}

describe('no raw html reaches the page (BR-RT-12)', () => {
  it('finds each forbidden form in a sample', () => {
    const sample = [
      'const a = <div dangerouslySetInnerHTML={{ __html: x }} />;',
      'el.innerHTML = x;',
      "el['outerHTML'] = x;",
      'el.insertAdjacentHTML("beforeend", x);',
      'document.write(x);',
    ].join('\n');

    expect(findRawHtml('sample.tsx', sample).map((finding) => finding.what)).toEqual([
      'dangerouslySetInnerHTML',
      'innerHTML',
      'outerHTML',
      'insertAdjacentHTML',
      'document.write',
    ]);
  });

  it('does not take a comment, a string or a similar name for one', () => {
    const sample = [
      '// el.innerHTML = x',
      "const s = 'innerHTML';",
      'const t = { innerHtmlLength: 1 };',
      'const u = obj.write(x);',
    ].join('\n');

    expect(findRawHtml('sample.ts', sample)).toEqual([]);
  });

  it('is clean across the product sources', () => {
    const findings = productSources(SRC).flatMap((file) =>
      findRawHtml(file, readFileSync(file, 'utf8')).map(
        (finding) =>
          `${path.relative(SRC, file).replaceAll('\\', '/')}:${finding.line} ${finding.what}`,
      ),
    );

    expect(findings).toEqual([]);
  });
});
