import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { env } from 'node:process';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';
import { describe, expect, it } from 'vitest';
import { ALLOWED, UNTIL, type AllowedEntry } from './kitGuard.allow';
import { BASELINE, BASELINE_COMMIT, type BaselineEntry } from './kitGuard.baseline';

/**
 * The UI-kit guard (tech-015 design § 9, `docs/conventions/ui-kit.md` § 9): a
 * screen is built from the kit only, so non-test sources under `src/features/`,
 * `src/app/` (not `app/theme`) and `src/shared/` (not `shared/ui`) may not
 * import a MUI part the kit wraps, draw a heading, input, button or table of
 * their own, style MUI's insides, cast a shadow, read the viewport, or reach
 * round these rules. The sources are parsed with the TypeScript compiler, so a
 * comment or a string of copy is never taken for code. `shared/ui`, the kit itself,
 * wraps MUI freely but is read for KIT-14, KIT-18 and KIT-24: it never hands a MUI part
 * on. A name that is not one literal (`` component={`h${level}`} ``, a conditional,
 * a same-file `const`) is read through its literal parts; one the file cannot tell is
 * left to review. Apart from the rules, no source may hold a relational `:has(`
 * selector (tech-015 review R1-2).
 *
 * A violation is a *site*; the allow-list (`kitGuard.allow.ts`) records an exact
 * count of sites per file and rule — today's violations, cut from the scan of
 * one frozen commit (`kitGuard.baseline.ts`) — and only shrinks: a new site
 * fails, a listed file that gains one fails too, and an entry above what the
 * file now has is stale and fails. See the failure message for the kit part to
 * use instead; if the kit lacks it, add it to the kit first.
 *
 * Cutting or re-cutting the baseline (design § 9.3): extract the commit's
 * `src/frontend/src`, point `KIT_GUARD_ROOT` at it and run
 * `KIT_GUARD_EMIT=baseline npx vitest run src/test/kitGuard.test.ts`; the file
 * is printed between two marker lines instead of asserting. The totals test
 * below is then brought to the new cut, in the same commit.
 *
 * Colour has its own test, `colourLiterals.test.ts`; dimensions are held to the
 * tokens by review.
 */

// ---------------------------------------------------------------------------
// The rules
// ---------------------------------------------------------------------------

interface PartRule {
  id: string;
  /** The MUI parts the rule forbids. */
  names: readonly string[];
  /** What to use instead, for the failure message. */
  use: string;
}

/** KIT-1..13: an import of a MUI part the kit wraps (design § 9.2). */
const PART_RULES: readonly PartRule[] = [
  {
    id: 'KIT-1',
    use: 'the fields (InlineTextField, InlineTextArea, InlineNumberField, InlineDateField, InlineSelectField, InlinePickerField, PersonField)',
    names: [
      'TextField',
      'Autocomplete',
      'Select',
      'Input',
      'InputBase',
      'OutlinedInput',
      'FilledInput',
      'NativeSelect',
      'TextareaAutosize',
      'InputAdornment',
      'InputLabel',
      'FormLabel',
      'FormHelperText',
      'FormControl',
    ],
  },
  {
    id: 'KIT-2',
    use: 'ActionButton, SplitButton or IconAction',
    names: [
      'Button',
      'ButtonGroup',
      'IconButton',
      'ButtonBase',
      'ToggleButton',
      'ToggleButtonGroup',
      'Fab',
    ],
  },
  {
    id: 'KIT-3',
    use: 'MenuButton or MoreActionsMenu',
    names: ['Menu', 'MenuItem', 'MenuList', 'Popover', 'Popper'],
  },
  { id: 'KIT-4', use: 'useNotice', names: ['Snackbar', 'SnackbarContent'] },
  { id: 'KIT-5', use: 'InlineNotice or ErrorBanner', names: ['Alert', 'AlertTitle'] },
  { id: 'KIT-6', use: 'Tag', names: ['Chip'] },
  {
    id: 'KIT-7',
    use: 'Surface, TableCard or FormPanel',
    names: [
      'Paper',
      'Card',
      'CardContent',
      'CardHeader',
      'CardActions',
      'CardActionArea',
      'CardMedia',
    ],
  },
  {
    id: 'KIT-8',
    use: 'the table family (TableCard, HeadCell, BodyRow, Cell, TablePager, DataTable)',
    names: [
      'Table',
      'TableContainer',
      'TableHead',
      'TableBody',
      'TableRow',
      'TableCell',
      'TableFooter',
      'TableSortLabel',
      'TablePagination',
      'Pagination',
      'PaginationItem',
    ],
  },
  {
    id: 'KIT-9',
    use: 'DialogShell, ConfirmDialog or FormDialog',
    names: [
      'Dialog',
      'DialogTitle',
      'DialogContent',
      'DialogContentText',
      'DialogActions',
      'Modal',
    ],
  },
  {
    id: 'KIT-10',
    use: 'the skeletons or ActionButton busy',
    names: ['Skeleton', 'CircularProgress', 'LinearProgress'],
  },
  { id: 'KIT-11', use: 'UserAvatar', names: ['Avatar', 'AvatarGroup'] },
  { id: 'KIT-12', use: 'PageHeader views or BoxedTabs', names: ['Tabs', 'Tab'] },
  { id: 'KIT-13', use: 'PageTrail', names: ['Breadcrumbs'] },
];

const PART_BY_NAME = new Map(
  PART_RULES.flatMap((rule) => rule.names.map((name): [string, PartRule] => [name, rule])),
);

/** What to use instead, by rule — the failure message's last clause. */
const ADVICE: Readonly<Record<string, string>> = {
  ...Object.fromEntries(PART_RULES.map((rule) => [rule.id, rule.use])),
  'KIT-14': 'the thin icons in shared/ui/icons (add the glyph there)',
  'KIT-15': 'the kit’s own sizes and states, not MUI’s inner classes',
  'KIT-16': 'Surface, not a shadow of your own',
  'KIT-17': 'ColumnLayout, not useMediaQuery',
  'KIT-18': 'the layer rule: shared/ does not import features/ or app/ (frontend.md)',
  'KIT-21':
    'PageHeader, Section / SectionHead, StateFrame or CardHeaderBar, not a heading element of your own',
  'KIT-22': 'usePageHead (shared/ui)',
  'KIT-23':
    'the fields, ActionButton / IconAction, the table family or DialogShell, not an intrinsic element',
  'KIT-24':
    'the named kit part; MUI is imported by name from @mui/material or @mui/material/<Name> only in shared/ui',
};

/** Rules in numeric order, so KIT-2 comes before KIT-10. */
function ruleNumber(rule: string): number {
  return Number(rule.slice('KIT-'.length));
}

const HEADING_TAGS = new Set(['h1', 'h2', 'h3', 'h4', 'h5', 'h6']);
/** A `Typography` variant MUI renders as a heading element when it has no `component`. */
const HEADING_VARIANTS = new Set([...HEADING_TAGS, 'subtitle1', 'subtitle2']);
const INTRINSIC_ELEMENTS = new Set(['input', 'textarea', 'select', 'button', 'table', 'dialog']);
/** Calls that make an element from its name: `createElement` and the JSX runtime's `jsx`. */
const ELEMENT_FACTORIES = new Set(['createElement', 'jsx', 'jsxs', 'jsxDEV']);
/**
 * A string that styles MUI's insides (KIT-15): `.MuiButton-root`, `.Mui-focusVisible`, a bare
 * `Mui-disabled`, and an attribute selector on a MUI class (`[class*="MuiOutlinedInput-root"]`).
 */
const MUI_INSIDES = /\.Mui[A-Z]|Mui-[A-Za-z]|\[class[^\]]*Mui[A-Z]/;
/** The shell's seam: a page registers with the shell through `usePageHead` only (KIT-22). */
const PAGE_CHROME_NAMES = new Set(['usePageChrome', 'PageChromeContext', 'useDocumentTitle']);
/** The only `@mui/*` packages the panel depends on (KIT-24). */
const MUI_PACKAGES = new Set(['material', 'icons-material', 'system']);

/**
 * Two named seams, each exempt from KIT-22 only and each with its reason
 * (design § 9.2).
 */
const EXEMPT: readonly { file: string; rules: readonly string[]; reason: string }[] = [
  {
    file: 'app/AppLayout.tsx',
    rules: ['KIT-22'],
    reason: 'the shell provides the page chrome that usePageHead reads',
  },
  {
    file: 'features/uikit/showcase/demos/PageHeadDemo.tsx',
    rules: ['KIT-22'],
    reason: 'the showcase’s inert-chrome demo mounts the chrome to show usePageHead alive',
  },
];

// ---------------------------------------------------------------------------
// Which rules apply to a file
// ---------------------------------------------------------------------------

/**
 * Today's test helpers that are not named `*.test.*`: builders, fixtures and drivers a
 * slice's tests share (tech-015 review R1-4). An explicit list, not a name pattern: a
 * production file called `testCaseSteps.tsx` (feat-031's test-case type) must be read like
 * any other. A new helper is added here, in a diff a reviewer sees.
 */
const TEST_HELPERS: ReadonlySet<string> = new Set([
  'features/boards/testApi.ts',
  'features/boards/testBacklog.ts',
  'features/boards/testBoard.ts',
  'features/boards/testDrag.ts',
  'features/boards/testFixtures.ts',
  'features/boards/testLevels.ts',
  'features/boards/testParentDialog.ts',
  'features/boards/testRender.tsx',
  'features/dictionaries/testFixtures.ts',
  'features/organizations/access/testAccess.tsx',
  'features/organizations/testApi.ts',
  'features/organizations/testFixtures.ts',
  'features/organizations/testRender.tsx',
  'features/process/testFixtures.ts',
  'features/workitems/testApi.ts',
  'features/workitems/testFields.ts',
  'features/workitems/testFixtures.ts',
  'features/workitems/testRender.tsx',
]);

/** `*.test.*`, anything under `src/test/`, the named helpers above, and declaration files. */
function isTestSource(relPath: string): boolean {
  const base = path.posix.basename(relPath);
  return (
    relPath.startsWith('test/') ||
    /\.test\./.test(base) ||
    TEST_HELPERS.has(relPath) ||
    base.endsWith('.d.ts')
  );
}

/** The rules that apply to a file of `src/`, relative with forward slashes; empty for a file out of scope. */
function rulesFor(relPath: string): ReadonlySet<string> {
  const rules = new Set<string>();
  if (isTestSource(relPath)) return rules;

  const inShared = relPath.startsWith('shared/');
  const inKit = relPath.startsWith('shared/ui/');
  const inTheme = relPath.startsWith('app/theme/');
  const guarded =
    relPath.startsWith('features/') ||
    (relPath.startsWith('app/') && !inTheme) ||
    (inShared && !inKit);

  if (guarded) {
    for (let number = 1; number <= 16; number += 1) rules.add(`KIT-${number}`);
    for (const number of [21, 22, 23, 24]) rules.add(`KIT-${number}`);
    if (relPath.startsWith('features/')) rules.add('KIT-17');
  }
  // An icon is defined in `shared/ui/icons` and nowhere else (the row of KIT-14).
  if (inKit && !relPath.startsWith('shared/ui/icons/')) rules.add('KIT-14');
  // The kit wraps a MUI part, it never hands one on: the doors of KIT-24 are shut in it
  // too (it imports by name), and so is a bare re-export of a part (tech-015 review R1-3).
  if (inKit) rules.add('KIT-24');
  if (inShared) rules.add('KIT-18');

  for (const seam of EXEMPT) {
    if (seam.file === relPath) seam.rules.forEach((rule) => rules.delete(rule));
  }
  return rules;
}

// ---------------------------------------------------------------------------
// The scan
// ---------------------------------------------------------------------------

interface Site {
  rule: string;
  line: number;
}

/** `@mui/material` is `{ name: undefined }`, `@mui/material/Button` is `{ name: 'Button' }`. */
function muiModule(specifier: string): { name: string | undefined } | undefined {
  if (specifier === '@mui/material') return { name: undefined };
  const match = /^@mui\/material\/([^/]+)/.exec(specifier);
  return match ? { name: match[1] } : undefined;
}

/** An `@mui/*` package the panel does not depend on: `@mui/lab`, `@mui/x-data-grid`… */
function isForeignMui(specifier: string): boolean {
  const match = /^@mui\/([^/]+)/.exec(specifier);
  return match !== null && !MUI_PACKAGES.has(match[1] ?? '');
}

/** The MUI door of KIT-24: a namespace import, a re-export, `require()` or `import()` of the root or of a part (`styles` excepted). */
function isMuiDoor(specifier: string): boolean {
  const mui = muiModule(specifier);
  return mui !== undefined && mui.name !== 'styles';
}

/** The target of a relative specifier, relative to `src/`, or undefined for a package. */
function resolveRelative(fromFile: string, specifier: string): string | undefined {
  if (!specifier.startsWith('.')) return undefined;
  return path.posix.normalize(path.posix.join(path.posix.dirname(fromFile), specifier));
}

function reachesFeaturesOrApp(target: string): boolean {
  return /^(?:features|app)(?:\/|$)/.test(target);
}

function isStringLike(node: ts.Node): node is ts.StringLiteralLike {
  return ts.isStringLiteralLike(node);
}

/** The text of a literal part: a string, a template without substitutions, or a template's head, middle or tail. */
function literalText(node: ts.Node): string | undefined {
  if (
    ts.isStringLiteral(node) ||
    ts.isNoSubstitutionTemplateLiteral(node) ||
    ts.isTemplateHead(node) ||
    ts.isTemplateMiddle(node) ||
    ts.isTemplateTail(node)
  ) {
    return node.text;
  }
  return undefined;
}

/**
 * What an expression can be, read through its literal parts (tech-015 review R1-3, as
 * `colourLiterals.test.ts` does for colour): `exact` are whole strings it may be, `heads`
 * are the fixed beginnings of a template that goes on with a substitution
 * (`` `h${level}` `` → `h`). Both branches of a condition, either side of `||` / `??`,
 * the right side of `&&`, and the initialisers of a `const` the expression names are read;
 * anything else says nothing.
 */
interface Parts {
  exact: string[];
  heads: string[];
}

const NO_PARTS: Parts = { exact: [], heads: [] };

function joinParts(...all: Parts[]): Parts {
  return { exact: all.flatMap((part) => part.exact), heads: all.flatMap((part) => part.heads) };
}

/** The same expression without the wrappers that change nothing at run time. */
function unwrap(expression: ts.Expression): ts.Expression {
  let current = expression;
  while (
    ts.isParenthesizedExpression(current) ||
    ts.isAsExpression(current) ||
    ts.isNonNullExpression(current) ||
    ts.isSatisfiesExpression(current) ||
    ts.isTypeAssertionExpression(current)
  ) {
    current = current.expression;
  }
  return current;
}

type Constants = ReadonlyMap<string, readonly ts.Expression[]>;

/** Every `const name = initialiser` of the file, by name — a name declared twice keeps both. */
function collectConstants(sourceFile: ts.SourceFile): Constants {
  const found = new Map<string, ts.Expression[]>();
  const walk = (node: ts.Node): void => {
    if (
      ts.isVariableDeclaration(node) &&
      ts.isIdentifier(node.name) &&
      node.initializer &&
      (node.parent.flags & ts.NodeFlags.Const) !== 0
    ) {
      found.set(node.name.text, [...(found.get(node.name.text) ?? []), node.initializer]);
    }
    ts.forEachChild(node, walk);
  };
  walk(sourceFile);
  return found;
}

function partsOf(expression: ts.Expression | undefined, constants: Constants, depth = 0): Parts {
  if (!expression || depth > 6) return NO_PARTS;
  const inner = unwrap(expression);
  if (ts.isStringLiteralLike(inner)) return { exact: [inner.text], heads: [] };
  if (ts.isTemplateExpression(inner)) {
    return { exact: [], heads: inner.head.text === '' ? [] : [inner.head.text] };
  }
  if (ts.isConditionalExpression(inner)) {
    return joinParts(
      partsOf(inner.whenTrue, constants, depth + 1),
      partsOf(inner.whenFalse, constants, depth + 1),
    );
  }
  if (ts.isBinaryExpression(inner)) {
    const operator = inner.operatorToken.kind;
    if (
      operator === ts.SyntaxKind.BarBarToken ||
      operator === ts.SyntaxKind.QuestionQuestionToken
    ) {
      return joinParts(
        partsOf(inner.left, constants, depth + 1),
        partsOf(inner.right, constants, depth + 1),
      );
    }
    if (
      operator === ts.SyntaxKind.AmpersandAmpersandToken ||
      operator === ts.SyntaxKind.CommaToken
    ) {
      return partsOf(inner.right, constants, depth + 1);
    }
    return NO_PARTS;
  }
  if (ts.isIdentifier(inner)) {
    return joinParts(
      ...(constants.get(inner.text) ?? []).map((value) => partsOf(value, constants, depth + 1)),
    );
  }
  return NO_PARTS;
}

/** The rules an element name falls under: a heading (KIT-21) or a form, button, table, dialog (KIT-23). */
function elementRules(parts: Parts): Set<string> {
  const rules = new Set<string>();
  for (const name of parts.exact) {
    if (HEADING_TAGS.has(name)) rules.add('KIT-21');
    else if (INTRINSIC_ELEMENTS.has(name)) rules.add('KIT-23');
  }
  for (const head of parts.heads) {
    if ([...HEADING_TAGS].some((tag) => tag.startsWith(head))) rules.add('KIT-21');
    if ([...INTRINSIC_ELEMENTS].some((tag) => tag.startsWith(head))) rules.add('KIT-23');
  }
  return rules;
}

/** Whether a `Typography` variant would render as a heading element. */
function isHeadingVariant(parts: Parts): boolean {
  return (
    parts.exact.some((name) => HEADING_VARIANTS.has(name)) ||
    parts.heads.some((head) => [...HEADING_VARIANTS].some((variant) => variant.startsWith(head)))
  );
}

/** One attribute of a JSX element, or one property of an object spread into it. */
interface AttributeEntry {
  name: string;
  /** Absent for a bare attribute (`<Box boxShadow />`). */
  value: ts.Expression | undefined;
  node: ts.Node;
  /** Came from `{...obj}`: its property is also visited as an object property. */
  fromSpread: boolean;
}

function attributeEntries(attributes: ts.JsxAttributes, constants: Constants): AttributeEntry[] {
  const entries: AttributeEntry[] = [];
  const spread = (expression: ts.Expression, node: ts.Node, depth: number): void => {
    if (depth > 4) return;
    const target = unwrap(expression);
    const objects: ts.ObjectLiteralExpression[] = [];
    if (ts.isObjectLiteralExpression(target)) objects.push(target);
    else if (ts.isIdentifier(target)) {
      for (const value of constants.get(target.text) ?? []) {
        const inner = unwrap(value);
        if (ts.isObjectLiteralExpression(inner)) objects.push(inner);
      }
    }
    for (const object of objects) {
      for (const property of object.properties) {
        if (ts.isPropertyAssignment(property)) {
          const name = nameOf(property.name);
          if (name !== undefined) {
            entries.push({ name, value: property.initializer, node, fromSpread: true });
          }
        } else if (ts.isShorthandPropertyAssignment(property)) {
          entries.push({ name: property.name.text, value: property.name, node, fromSpread: true });
        } else if (ts.isSpreadAssignment(property)) {
          spread(property.expression, node, depth + 1);
        }
      }
    }
  };
  for (const property of attributes.properties) {
    if (ts.isJsxAttribute(property) && ts.isIdentifier(property.name)) {
      const initializer = property.initializer;
      entries.push({
        name: property.name.text,
        value:
          initializer && ts.isJsxExpression(initializer) ? initializer.expression : initializer,
        node: property,
        fromSpread: false,
      });
    } else if (ts.isJsxSpreadAttribute(property)) {
      spread(property.expression, property, 0);
    }
  }
  return entries;
}

/** A CSS declaration written as text (`` css`box-shadow: …` ``) that casts a shadow. */
function castsShadowInText(text: string): boolean {
  for (const match of text.matchAll(/(?:^|[\s;{])box-shadow\s*:\s*([^;}]*)/gi)) {
    const value = (match[1] ?? '').trim().toLowerCase();
    if (value !== 'none' && value !== '0') return true;
  }
  return false;
}

/** Whether a statement carries the `export` modifier. */
function isExported(node: ts.Node): boolean {
  return (
    ts.canHaveModifiers(node) &&
    (ts.getModifiers(node) ?? []).some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword)
  );
}

/** The written name of an object property or JSX attribute, when it is spelled out. */
function nameOf(name: ts.Node): string | undefined {
  if (ts.isIdentifier(name) || isStringLike(name)) return name.text;
  return undefined;
}

/** A `boxShadow` value that casts nothing: `none` and `0`. */
function isNoShadow(expression: ts.Expression): boolean {
  if (isStringLike(expression)) return ['none', '0'].includes(expression.text.trim());
  if (ts.isNumericLiteral(expression)) return Number(expression.text) === 0;
  if (ts.isParenthesizedExpression(expression)) return isNoShadow(expression.expression);
  return false;
}

/** Whether an identifier is a use of a binding, not a name that happens to be spelled the same. */
function isReference(node: ts.Identifier): boolean {
  const parent = node.parent;
  if (ts.isPropertyAccessExpression(parent) && parent.name === node) return false;
  if (ts.isQualifiedName(parent) && parent.right === node) return false;
  if (ts.isBindingElement(parent) && parent.propertyName === node) return false;
  if (ts.isBindingElement(parent) && parent.name === node) return false;
  if (ts.isExportSpecifier(parent) && parent.propertyName !== undefined && parent.name === node) {
    return false;
  }
  if (
    (ts.isPropertyAssignment(parent) ||
      ts.isPropertyDeclaration(parent) ||
      ts.isPropertySignature(parent) ||
      ts.isMethodDeclaration(parent) ||
      ts.isMethodSignature(parent) ||
      ts.isGetAccessorDeclaration(parent) ||
      ts.isSetAccessorDeclaration(parent) ||
      ts.isEnumMember(parent) ||
      ts.isParameter(parent) ||
      ts.isVariableDeclaration(parent) ||
      ts.isFunctionDeclaration(parent) ||
      ts.isClassDeclaration(parent) ||
      ts.isTypeParameterDeclaration(parent) ||
      ts.isJsxAttribute(parent)) &&
    parent.name === node
  ) {
    return false;
  }
  return true;
}

/** Every site of every rule that applies to the file `relPath` of `src/`. */
function scanSource(relPath: string, source: string): Site[] {
  const enabled = rulesFor(relPath);
  if (enabled.size === 0) return [];

  const kind = relPath.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const sourceFile = ts.createSourceFile(relPath, source, ts.ScriptTarget.Latest, true, kind);
  const sites: Site[] = [];
  const add = (rule: string, node: ts.Node): void => {
    if (!enabled.has(rule)) return;
    const { line } = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile));
    sites.push({ rule, line: line + 1 });
  };

  const constants = collectConstants(sourceFile);

  /** Local names bound to a part of a rule: the import is a site, and so is each use. */
  const tracked = new Map<string, string>();
  /** The local names of MUI parts alone: what a kit file must not hand on (KIT-24). */
  const muiParts = new Set<string>();
  const typographies = new Set<string>();
  // The helpers a name can hide behind: found by what the import brings, not by the spelling
  // of the call (`import { styled as s }`, `createSvgIcon` from `@mui/material/utils`,
  // `useMediaQuery` from `@mui/system`, `createElement` and the JSX runtime).
  const mediaQueries = new Set(['useMediaQuery']);
  const styledNames = new Set(['styled']);
  const svgIconFactories = new Set(['createSvgIcon']);
  const elementFactories = new Set<string>();
  const namespaces = new Set<string>();

  const bindMui = (local: string, part: string, node: ts.Node): void => {
    const rule = PART_BY_NAME.get(part);
    if (rule) {
      tracked.set(local, rule.id);
      muiParts.add(local);
      add(rule.id, node);
    } else if (part === 'Typography') {
      typographies.add(local);
    } else if (part === 'useMediaQuery') {
      mediaQueries.add(local);
    }
  };

  const checkLayer = (specifier: string, node: ts.Node): void => {
    if (!relPath.startsWith('shared/')) return;
    const target = resolveRelative(relPath, specifier);
    if (target !== undefined && reachesFeaturesOrApp(target)) add('KIT-18', node);
  };

  // Pass 1: the imports bind the names the second pass counts.
  for (const statement of sourceFile.statements) {
    if (!ts.isImportDeclaration(statement) || !isStringLike(statement.moduleSpecifier)) continue;
    const specifier = statement.moduleSpecifier.text;
    const mui = muiModule(specifier);
    const clause = statement.importClause;
    let door = isForeignMui(specifier);

    const external = !specifier.startsWith('.');

    if (clause?.name && mui?.name !== undefined) bindMui(clause.name.text, mui.name, clause.name);
    if (clause?.name && external) {
      if (/(?:^|\/)styled$/.test(specifier)) styledNames.add(clause.name.text);
      if (/(?:^|\/)createSvgIcon$/.test(specifier)) svgIconFactories.add(clause.name.text);
      if (/(?:^|\/)useMediaQuery$/.test(specifier)) mediaQueries.add(clause.name.text);
    }
    const bindings = clause?.namedBindings;
    if (bindings && ts.isNamespaceImport(bindings)) {
      namespaces.add(bindings.name.text);
      if (isMuiDoor(specifier)) door = true;
    } else if (bindings) {
      for (const element of bindings.elements) {
        const imported = (element.propertyName ?? element.name).text;
        const local = element.name;
        if (mui && imported.endsWith('Classes')) add('KIT-15', element);
        if (mui && mui.name === undefined) bindMui(local.text, imported, element);
        else if (mui?.name !== undefined && imported === 'default')
          bindMui(local.text, mui.name, element);
        if (PAGE_CHROME_NAMES.has(imported)) {
          tracked.set(local.text, 'KIT-22');
          add('KIT-22', element);
        }
        if (external) {
          if (imported === 'styled') styledNames.add(local.text);
          if (imported === 'createSvgIcon') svgIconFactories.add(local.text);
          if (imported === 'useMediaQuery') mediaQueries.add(local.text);
          if (ELEMENT_FACTORIES.has(imported)) elementFactories.add(local.text);
        }
      }
    }
    if (door) add('KIT-24', statement);
    checkLayer(specifier, statement);
  }

  const callTarget = (node: ts.CallExpression): string | undefined => {
    const [first] = node.arguments;
    return first && isStringLike(first) ? first.text : undefined;
  };

  /**
   * Heading and intrinsic-element checks of what names an element: a `component` or `as`
   * prop, a `styled(…)` argument, the first argument of `createElement`. One site per rule
   * the value can fall under, however many branches it has.
   */
  const checkElement = (value: ts.Expression | undefined, node: ts.Node): void => {
    for (const rule of elementRules(partsOf(value, constants))) add(rule, node);
  };

  const visit = (node: ts.Node): void => {
    // The imports were taken in pass 1; a closing tag is not a second site.
    if (ts.isImportDeclaration(node) || ts.isJsxClosingElement(node)) return;

    if (ts.isIdentifier(node)) {
      const rule = tracked.get(node.text);
      if (rule !== undefined && isReference(node)) add(rule, node);
      return;
    }

    const text = literalText(node);
    if (text !== undefined) {
      if (MUI_INSIDES.test(text)) add('KIT-15', node);
      if (castsShadowInText(text)) add('KIT-16', node);
    }

    // The kit wraps a part and never hands it on: a bare export of an imported part is a
    // door (`export { Chip }`, `export default Chip`, `export const RawChip = Chip`).
    if (relPath.startsWith('shared/ui/')) {
      if (
        ts.isExportDeclaration(node) &&
        !node.moduleSpecifier &&
        node.exportClause &&
        ts.isNamedExports(node.exportClause)
      ) {
        for (const element of node.exportClause.elements) {
          if (muiParts.has((element.propertyName ?? element.name).text)) add('KIT-24', element);
        }
      } else if (ts.isExportAssignment(node)) {
        const exported = unwrap(node.expression);
        if (ts.isIdentifier(exported) && muiParts.has(exported.text)) add('KIT-24', node);
      } else if (ts.isVariableStatement(node) && isExported(node)) {
        for (const declaration of node.declarationList.declarations) {
          const exported = declaration.initializer && unwrap(declaration.initializer);
          if (exported && ts.isIdentifier(exported) && muiParts.has(exported.text)) {
            add('KIT-24', declaration);
          }
        }
      }
    }

    if (
      ts.isExportDeclaration(node) &&
      node.moduleSpecifier &&
      isStringLike(node.moduleSpecifier)
    ) {
      const specifier = node.moduleSpecifier.text;
      if (isMuiDoor(specifier) || isForeignMui(specifier)) add('KIT-24', node);
      checkLayer(specifier, node);
      const clause = node.exportClause;
      if (clause && ts.isNamedExports(clause)) {
        for (const element of clause.elements) {
          if (PAGE_CHROME_NAMES.has((element.propertyName ?? element.name).text))
            add('KIT-22', element);
        }
      }
    } else if (ts.isCallExpression(node)) {
      const callee = node.expression;
      const specifier = callTarget(node);
      if (
        specifier !== undefined &&
        (callee.kind === ts.SyntaxKind.ImportKeyword ||
          (ts.isIdentifier(callee) && callee.text === 'require'))
      ) {
        if (isMuiDoor(specifier) || isForeignMui(specifier)) add('KIT-24', node);
        checkLayer(specifier, node);
      } else if (ts.isIdentifier(callee) && styledNames.has(callee.text)) {
        checkElement(node.arguments[0], node);
      } else if (
        (ts.isIdentifier(callee) && svgIconFactories.has(callee.text)) ||
        (ts.isPropertyAccessExpression(callee) && callee.name.text === 'createSvgIcon')
      ) {
        add('KIT-14', node);
      } else if (ts.isIdentifier(callee) && mediaQueries.has(callee.text)) {
        add('KIT-17', node);
      } else if (
        (ts.isIdentifier(callee) && elementFactories.has(callee.text)) ||
        (ts.isPropertyAccessExpression(callee) && callee.name.text === 'createElement')
      ) {
        checkElement(node.arguments[0], node);
      }
    } else if (ts.isPropertyAccessExpression(node)) {
      const owner = node.expression;
      if (ts.isIdentifier(owner)) {
        // `styled.button`, `styled.h2`: the tag is the property.
        if (styledNames.has(owner.text)) {
          for (const rule of elementRules({ exact: [node.name.text], heads: [] })) add(rule, node);
        }
        // `import * as chrome from '…/pageChrome'; chrome.usePageChrome()`.
        if (namespaces.has(owner.text) && PAGE_CHROME_NAMES.has(node.name.text)) {
          add('KIT-22', node);
        }
      }
    } else if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
      const tag = node.tagName;
      if (ts.isIdentifier(tag) && /^[a-z]/.test(tag.text)) {
        for (const rule of elementRules({ exact: [tag.text], heads: [] })) add(rule, node);
      }
      let variants = NO_PARTS;
      let hasComponent = false;
      for (const entry of attributeEntries(node.attributes, constants)) {
        const spread = entry.fromSpread; // its property is also visited as an object property
        switch (entry.name) {
          case 'component':
            hasComponent = true;
            if (!spread) checkElement(entry.value, entry.node);
            break;
          case 'as':
            checkElement(entry.value, entry.node);
            break;
          case 'variant':
            variants = joinParts(variants, partsOf(entry.value, constants));
            break;
          case 'role':
            if (!spread && partsOf(entry.value, constants).exact.includes('heading')) {
              add('KIT-21', entry.node);
            }
            break;
          case 'boxShadow':
            if (!spread && (!entry.value || !isNoShadow(entry.value))) add('KIT-16', entry.node);
            break;
        }
      }
      // MUI renders `Typography variant="h2"` (and the subtitles) as a heading element.
      if (
        ts.isIdentifier(tag) &&
        typographies.has(tag.text) &&
        isHeadingVariant(variants) &&
        !hasComponent
      ) {
        add('KIT-21', node);
      }
    } else if (ts.isPropertyAssignment(node) || ts.isShorthandPropertyAssignment(node)) {
      const name = nameOf(node.name);
      if (name === 'boxShadow' || name === 'box-shadow') {
        if (ts.isShorthandPropertyAssignment(node) || !isNoShadow(node.initializer)) {
          add('KIT-16', node);
        }
      } else if (name === 'component' && ts.isPropertyAssignment(node)) {
        checkElement(node.initializer, node);
      } else if (name === 'role' && ts.isPropertyAssignment(node)) {
        if (partsOf(node.initializer, constants).exact.includes('heading')) add('KIT-21', node);
      }
    }

    ts.forEachChild(node, visit);
  };
  visit(sourceFile);
  return sites.sort((a, b) => a.line - b.line || ruleNumber(a.rule) - ruleNumber(b.rule));
}

/**
 * The lines of a relational `:has(` selector in a string or a template of a source file —
 * not in a comment, which may say why it is banned. Not a KIT rule and not on the
 * allow-list: the kit has none and may not grow one (tech-015 review R1-2). In jsdom
 * each such rule walks the subtree for every element whose style a query reads, which made
 * every role query on the card 15-20 times slower; the state they read belongs in an
 * attribute the component sets (`FieldFrame`'s `data-open`).
 */
function relationalSelectorLines(relPath: string, source: string): number[] {
  if (isTestSource(relPath)) return [];
  const kind = relPath.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const sourceFile = ts.createSourceFile(relPath, source, ts.ScriptTarget.Latest, true, kind);
  const lines: number[] = [];
  const visit = (node: ts.Node): void => {
    const text = literalText(node);
    if (text !== undefined && /:has\s*\(/i.test(text)) {
      lines.push(sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1);
    }
    ts.forEachChild(node, visit);
  };
  visit(sourceFile);
  return lines;
}

// ---------------------------------------------------------------------------
// The tree
// ---------------------------------------------------------------------------

const SRC = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
/** The tree to scan: this one, or another commit's extracted `src/` (`KIT_GUARD_ROOT`). */
const ROOT = env.KIT_GUARD_ROOT ? path.resolve(env.KIT_GUARD_ROOT) : SRC;
const EMIT = env.KIT_GUARD_EMIT === 'baseline';
const FEATURES_DIRECTORY = path.resolve(SRC, '..', '..', '..', 'docs', 'features');

function listSources(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const full = path.join(directory, entry);
    if (statSync(full).isDirectory()) return listSources(full);
    return /\.tsx?$/.test(entry) ? [full] : [];
  });
}

interface Found {
  file: string;
  rule: string;
  lines: number[];
}

function byFileThenRule(
  a: { file: string; rule: string },
  b: { file: string; rule: string },
): number {
  if (a.file !== b.file) return a.file < b.file ? -1 : 1;
  return ruleNumber(a.rule) - ruleNumber(b.rule);
}

function groupSites(file: string, sites: readonly Site[]): Found[] {
  const lines = new Map<string, number[]>();
  for (const site of sites) lines.set(site.rule, [...(lines.get(site.rule) ?? []), site.line]);
  return [...lines].map(([rule, ruleLines]) => ({ file, rule, lines: ruleLines }));
}

function scanTree(root: string): { found: Found[]; files: string[] } {
  const files = listSources(root).map((full) =>
    path.relative(root, full).split(path.sep).join('/'),
  );
  const found = files.flatMap((file) =>
    groupSites(file, scanSource(file, readFileSync(path.join(root, file), 'utf8'))),
  );
  return { found: found.sort(byFileThenRule), files };
}

// ---------------------------------------------------------------------------
// The ratchet
// ---------------------------------------------------------------------------

interface ListEnvironment {
  fileExists(file: string): boolean;
  /** Whether the task's own `status.md` reads `stage: complete`. */
  isComplete(until: string): boolean;
}

const UNTIL_VALUES: readonly string[] = UNTIL;

/**
 * Compares the scan with the allow-list and the list with the baseline; returns
 * what is wrong, one line each (design § 9.3): the exact match, the bound by the
 * baseline, `until` and `reason`, the order, and that every file exists.
 */
function compareWithList(
  found: readonly Found[],
  allowed: readonly AllowedEntry[],
  baseline: readonly BaselineEntry[],
  environment: ListEnvironment,
): string[] {
  const failures: string[] = [];
  const key = (entry: { file: string; rule: string }): string => `${entry.file} ${entry.rule}`;
  const listed = new Map<string, AllowedEntry>();
  const cut = new Map(baseline.map((entry) => [key(entry), entry]));
  const scanned = new Map(found.map((entry) => [key(entry), entry]));

  allowed.forEach((entry, index) => {
    if (listed.has(key(entry))) failures.push(`${key(entry)}: listed twice`);
    listed.set(key(entry), entry);

    const previous = allowed[index - 1];
    if (previous && byFileThenRule(previous, entry) >= 0 && key(previous) !== key(entry)) {
      failures.push(`${key(entry)}: out of order — sort by file, then rule`);
    }
    if (!Number.isInteger(entry.count) || entry.count < 1) {
      failures.push(`${key(entry)}: count ${entry.count} is not a positive whole number`);
    }
    if (!UNTIL_VALUES.includes(entry.until)) {
      failures.push(
        `${key(entry)}: until '${entry.until}' is not one of ${UNTIL_VALUES.join(', ')}`,
      );
    } else if (environment.isComplete(entry.until)) {
      failures.push(`${key(entry)}: ${entry.until} is complete — its entries must already be gone`);
    }
    if (entry.reason.trim() === '') failures.push(`${key(entry)}: the reason is empty`);

    const base = cut.get(key(entry));
    if (!base) {
      failures.push(`${key(entry)}: not in the baseline — fix the code, not the list`);
    } else if (entry.count > base.count) {
      failures.push(
        `${key(entry)}: lists ${entry.count}, the baseline has ${base.count} — fix the code, not the list`,
      );
    }
    if (!environment.fileExists(entry.file)) {
      failures.push(`${key(entry)}: no such file — remove the entry`);
    }
  });

  for (const site of found) {
    const entry = listed.get(key(site));
    const lines = site.lines.join(', ');
    if (!entry) {
      failures.push(
        `${site.file} ${site.rule}: ${site.lines.length} new site(s) at lines ${lines} — use ${ADVICE[site.rule] ?? 'the kit'}; if the kit lacks it, add it to the kit first (docs/conventions/ui-kit.md § 7)`,
      );
    } else if (entry.count < site.lines.length) {
      failures.push(
        `${site.file} ${site.rule}: ${site.lines.length - entry.count} new site(s) — the list allows ${entry.count}, the file has ${site.lines.length}, at lines ${lines} — use ${ADVICE[site.rule] ?? 'the kit'}`,
      );
    } else if (entry.count > site.lines.length) {
      failures.push(
        `${site.file} ${site.rule}: stale — lists ${entry.count}, the file has ${site.lines.length}; lower it to ${site.lines.length}`,
      );
    }
  }
  for (const entry of allowed) {
    if (!scanned.has(key(entry)) && environment.fileExists(entry.file)) {
      failures.push(`${key(entry)}: stale — the file has none; remove the entry`);
    }
  }
  return failures;
}

/** Whether the task's own `status.md` under `docs/features` reads `stage: complete`; a task with no folder yet is not complete. */
function isTaskComplete(id: string): boolean {
  if (!existsSync(FEATURES_DIRECTORY)) return false;
  return readdirSync(FEATURES_DIRECTORY)
    .filter((folder) => folder.startsWith(`${id}-`))
    .some((folder) => {
      const status = path.join(FEATURES_DIRECTORY, folder, 'status.md');
      return existsSync(status) && /^- stage:\s*complete\s*$/m.test(readFileSync(status, 'utf8'));
    });
}

const LIVE: ListEnvironment = {
  fileExists: (file) => existsSync(path.join(ROOT, file)),
  isComplete: isTaskComplete,
};

/** The text of `kitGuard.baseline.ts` for a scan: what `KIT_GUARD_EMIT=baseline` prints. */
function renderBaseline(found: readonly Found[], commit: string): string {
  const entries = found
    .map(
      (entry) =>
        `  { file: '${entry.file}', rule: '${entry.rule}', count: ${entry.lines.length} },`,
    )
    .join('\n');
  return [
    `// The scan of commit ${commit}, and nothing else (tech-015 design § 9.3): every (file, rule)`,
    '// the kit guard found there, with the number of sites. Frozen — a change other than a',
    '// re-cut from a newer main (logged in the task’s status.md) is a review stop. The list',
    '// that migrations lower is kitGuard.allow.ts; this file never changes with them.',
    `export const BASELINE_COMMIT = '${commit}';`,
    '',
    'export interface BaselineEntry {',
    '  file: string;',
    '  rule: string;',
    '  count: number;',
    '}',
    '',
    'export const BASELINE: readonly BaselineEntry[] = [',
    entries,
    '];',
    '',
  ].join('\n');
}

// ---------------------------------------------------------------------------
// The tests
// ---------------------------------------------------------------------------

const ruleIds = (relPath: string, source: string): string[] =>
  scanSource(relPath, source).map((site) => site.rule);

const FEATURE_FILE = 'features/demo/Demo.tsx';

describe('tech-015 Scenario: the scan catches each way round a rule (design § 9.4)', () => {
  describe('imports of the MUI parts the kit wraps (KIT-1..13)', () => {
    it.each(PART_RULES.flatMap((rule) => rule.names.map((name) => [rule.id, name] as const)))(
      '%s catches a named import of %s from @mui/material',
      (rule, name) => {
        expect(ruleIds(FEATURE_FILE, `import { ${name} } from '@mui/material';`)).toEqual([rule]);
      },
    );

    it.each(PART_RULES.flatMap((rule) => rule.names.map((name) => [rule.id, name] as const)))(
      '%s catches a default import of %s from its own module',
      (rule, name) => {
        expect(ruleIds(FEATURE_FILE, `import ${name} from '@mui/material/${name}';`)).toEqual([
          rule,
        ]);
      },
    );

    it.each([
      ['an alias', `import { Button as B } from '@mui/material';\nconst a = <B />;`, 2],
      [
        '{ default as B }',
        `import { default as B } from '@mui/material/Button';\nconst a = <B />;`,
        2,
      ],
      [
        'a default import under another name',
        `import B from '@mui/material/Button';\nconst a = <B />;`,
        2,
      ],
      [
        'styled(Name)',
        `import Button from '@mui/material/Button';\nconst S = styled(Button)({});`,
        2,
      ],
      [
        'component={Name}',
        `import Menu from '@mui/material/Menu';\nconst a = <Box component={Menu} />;`,
        2,
      ],
      [
        'a type query',
        `import Table from '@mui/material/Table';\ntype P = React.ComponentProps<typeof Table>;`,
        2,
      ],
    ])('counts every use of the local name: %s', (_name, source, sites) => {
      expect(ruleIds(FEATURE_FILE, source)).toHaveLength(sites);
    });

    it('counts one import and two <TextField>s as 3 sites, and a third as the 4th', () => {
      const three = `import TextField from '@mui/material/TextField';\nconst a = <TextField />;\nconst b = <TextField />;`;
      expect(ruleIds(FEATURE_FILE, three)).toEqual(['KIT-1', 'KIT-1', 'KIT-1']);
      expect(ruleIds(FEATURE_FILE, `${three}\nconst c = <TextField />;`)).toHaveLength(4);
    });

    it('counts a closing tag as no site', () => {
      const source = `import TextField from '@mui/material/TextField';\nconst a = <TextField>x</TextField>;`;
      expect(ruleIds(FEATURE_FILE, source)).toEqual(['KIT-1', 'KIT-1']);
    });

    it('reads a file under shared/lib, the widened scope', () => {
      expect(
        ruleIds('shared/lib/wrapper.tsx', `import TextField from '@mui/material/TextField';`),
      ).toEqual(['KIT-1']);
    });

    it('reads app/, outside app/theme', () => {
      expect(ruleIds('app/Frame.tsx', `import { Chip } from '@mui/material';`)).toEqual(['KIT-6']);
    });
  });

  describe('doors round the rules (KIT-24)', () => {
    it.each([
      ["import * as M from '@mui/material';"],
      ["import * as Button from '@mui/material/Button';"],
      ["export { Button } from '@mui/material';"],
      ["export * from '@mui/material/Button';"],
      ["const m = require('@mui/material');"],
      ["const m = import('@mui/material/Button');"],
      ["import { DataGrid } from '@mui/x-data-grid';"],
      ["import Grid from '@mui/lab/Grid';"],
      ["export { DataGrid } from '@mui/x-data-grid';"],
    ])('catches %s', (source) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual(['KIT-24']);
    });

    it.each([
      ["import * as Styles from '@mui/material/styles';"],
      ["import { styled, alpha } from '@mui/material/styles';"],
      ["import { useTheme } from '@mui/system';"],
      ["import * as Icons from '@mui/icons-material';"],
      ["import AddIcon from '@mui/icons-material/Add';"],
      ["const lazyStyles = import('@mui/material/styles');"],
    ])('lets %s through', (source) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual([]);
    });

    // R1-3: the kit is exempt from the import rules, not from this one — "add it to the kit
    // first" must not end in a raw MUI part that every feature can import.
    describe('in the kit: wrap a part, never hand one on (tech-015 review R1-3)', () => {
      const KIT_FILE = 'shared/ui/marks/Raw.tsx';

      it.each([
        ["export { default as RawChip } from '@mui/material/Chip';"],
        ["export { Chip } from '@mui/material';"],
        ["export * from '@mui/material/Chip';"],
        ["import Chip from '@mui/material/Chip';\nexport { Chip };"],
        ["import Chip from '@mui/material/Chip';\nexport { Chip as RawChip };"],
        ["import { Chip } from '@mui/material';\nexport { Chip as default };"],
        ["import Chip from '@mui/material/Chip';\nexport default Chip;"],
        ["import Chip from '@mui/material/Chip';\nexport const RawChip = Chip;"],
        ["import { Chip as C } from '@mui/material';\nexport const Raw = (C);"],
        ["import * as M from '@mui/material';"],
        ["const m = require('@mui/material/Chip');"],
        ["import { DataGrid } from '@mui/x-data-grid';"],
      ])('catches one site in %s', (source) => {
        expect(ruleIds(KIT_FILE, source)).toEqual(['KIT-24']);
      });

      it.each([
        [
          'import Chip from \'@mui/material/Chip\';\nexport function Tag() { return <Chip size="small" />; }',
        ],
        ["import Chip from '@mui/material/Chip';\nexport const Wrapped = styled(Chip)({});"],
        [
          "import Chip from '@mui/material/Chip';\nconst Inner = Chip;\nexport function Tag() { return <Inner />; }",
        ],
        ["export { Tag } from './Tag';\nexport { type TagProps } from './Tag';"],
        ["export { alpha, styled } from '@mui/material/styles';"],
        ["import { Chip } from '../kit';\nexport { Chip };"],
        ["export { default as CloseIcon } from '@mui/icons-material/Close';"],
      ])('lets %s through', (source) => {
        expect(ruleIds(KIT_FILE, source)).toEqual([]);
      });

      it('does not widen the kit’s other freedoms: its MUI parts and elements stay allowed', () => {
        expect(
          ruleIds(KIT_FILE, `import Chip from '@mui/material/Chip';\nconst a = <Chip />;`),
        ).toEqual([]);
      });
    });
  });

  describe("styling MUI's insides (KIT-15)", () => {
    it.each([
      ["const sx = { '& .MuiSvgIcon-root': { fontSize: 16 } };"],
      ["const sx = { '&.Mui-focusVisible': { outline: 'none' } };"],
      ["const sx = { '&.Mui-disabled': {} };"],
      ['const sx = { [`& .MuiButton-startIcon`]: {} };'],
      ['const sx = { [`${base} .MuiInputBase-input`]: {} };'],
      [
        "import { svgIconClasses } from '@mui/material';\nconst sx = { [`& .${svgIconClasses.root}`]: {} };",
      ],
      ["import { outlinedInputClasses } from '@mui/material/OutlinedInput';"],
      ["import { generateUtilityClasses } from '@mui/material';"],
      // R1-4: an attribute selector on a MUI class reaches the same insides.
      ['const sx = { \'& [class*="MuiOutlinedInput-notchedOutline"]\': {} };'],
      ['const sx = { \'& [class^="MuiButton-"]\': {} };'],
      ['const sx = { [`& [class~="MuiChip-label"]`]: {} };'],
    ])('catches one site in %s', (source) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual(['KIT-15']);
    });
  });

  describe('a surface or an icon of your own, the viewport (KIT-14, KIT-16, KIT-17)', () => {
    it.each([
      ["const sx = { boxShadow: '0 1px 2px black' };", 'KIT-16'],
      ['const sx = { boxShadow: 1 };', 'KIT-16'],
      ["const sx = { 'box-shadow': theme.shadows[2] };", 'KIT-16'],
      ['const sx = { boxShadow };', 'KIT-16'],
      ['const a = <Box boxShadow={1} />;', 'KIT-16'],
      ['const a = <Box boxShadow="2px 2px" />;', 'KIT-16'],
      ["const Icon = createSvgIcon(<path d='' />, 'X');", 'KIT-14'],
      ["const Icon = utils.createSvgIcon(<path d='' />, 'X');", 'KIT-14'],
      // R1-4: a shadow written as CSS text, and the factories under another name.
      ['const S = css`box-shadow: 0 1px 2px black;`;', 'KIT-16'],
      ['const S = css`color: red; box-shadow: ${shadow};`;', 'KIT-16'],
      ["const sx = { '--rule': 'a { box-shadow: 1px 1px }' };", 'KIT-16'],
      [
        "import { createSvgIcon as make } from '@mui/material/utils';\nconst Icon = make(<path d='' />, 'X');",
        'KIT-14',
      ],
      [
        "import createSvgIcon from '@mui/material/utils/createSvgIcon';\nconst I = createSvgIcon(<path d='' />, 'X');",
        'KIT-14',
      ],
      [
        "import createIcon from '@mui/material/utils/createSvgIcon';\nconst I = createIcon(<path d='' />, 'X');",
        'KIT-14',
      ],
    ])('catches %s', (source, rule) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual([rule]);
    });

    it.each([
      ['const S = css`box-shadow: none;`;'],
      ['const S = css`box-shadow: 0;`;'],
      ['const S = css`border: 1px solid; margin: 0;`;'],
      ["const copy = 'the box-shadow is not mentioned here';"],
    ])('lets %s through', (source) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual([]);
    });

    it('catches useMediaQuery in a feature, and only in a feature', () => {
      const source = `import useMediaQuery from '@mui/material/useMediaQuery';\nconst wide = useMediaQuery('(min-width: 600px)');`;
      expect(ruleIds(FEATURE_FILE, source)).toEqual(['KIT-17']);
      expect(ruleIds('app/AppLayout.tsx', source)).toEqual([]);
      expect(ruleIds('shared/lib/viewport.ts', source)).toEqual([]);
    });

    it.each([
      [
        `import { useMediaQuery as query } from '@mui/material';\nconst wide = query('(min-width: 600px)');`,
      ],
      // R1-4: the same hook from the package MUI re-exports it from, under another name.
      [
        `import { useMediaQuery as query } from '@mui/system';\nconst wide = query('(min-width: 600px)');`,
      ],
      [`import query from '@mui/system/useMediaQuery';\nconst wide = query('(min-width: 600px)');`],
      [`import q from '@mui/material/useMediaQuery';\nconst wide = q('(min-width: 600px)');`],
    ])('catches useMediaQuery under an alias: %s', (source) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual(['KIT-17']);
    });

    it('catches a createSvgIcon in shared/ui outside shared/ui/icons, and nowhere in the icons or the theme', () => {
      const source = `const Icon = createSvgIcon(<path d='' />, 'X');`;
      expect(ruleIds('shared/ui/marks/Mark.tsx', source)).toEqual(['KIT-14']);
      expect(ruleIds('shared/ui/icons/ThinIcons.tsx', source)).toEqual([]);
      expect(ruleIds('app/theme/CrownIcon.tsx', source)).toEqual([]);
    });
  });

  describe('intrinsic form, button, table and dialog elements (KIT-23)', () => {
    it.each([
      ['const a = <input />;'],
      ['const a = <textarea />;'],
      ['const a = <select />;'],
      ['const a = <button>x</button>;'],
      ['const a = <table />;'],
      ['const a = <dialog />;'],
      ['const a = <Box component="input" />;'],
      ["const a = <Box component={'button'} />;"],
      ['const a = <Typography component="button">x</Typography>;'],
      ["const S = styled('button')({});"],
      ["const o = { slotProps: { root: { component: 'textarea' } } };"],
      // R1-3: a name that is not one literal is read through its literal parts.
      ["const a = <Box component={wide ? 'table' : 'div'} />;"],
      ["const a = <Box component={tag || 'button'} />;"],
      ["const TAG = 'button';\nconst a = <Box component={TAG} />;"],
      ["const TAG = wide ? 'input' : 'textarea';\nconst a = <Box component={TAG} />;"],
      ['const a = <Box component={`b${rest}`} />;'],
      // R1-4: other roads to the same element.
      ['const a = <Box as="button" />;'],
      ["const a = <Box as={ready ? 'input' : 'span'} />;"],
      [
        "import { createElement } from 'react';\nconst a = createElement('input', { type: 'text' });",
      ],
      ["import { createElement as make } from 'react';\nconst a = make('select', null);"],
      ["const a = React.createElement('button', null, 'x');"],
      ["import { jsx } from 'react/jsx-runtime';\nconst a = jsx('textarea', {});"],
      ["import styled from '@emotion/styled';\nconst S = styled.button`color: red;`;"],
      ["import { styled } from '@mui/material/styles';\nconst S = styled.table({});"],
      ["import { styled as s } from '@mui/material/styles';\nconst S = s('button')({});"],
      ["import emotionStyled from '@emotion/styled';\nconst S = emotionStyled('dialog')({});"],
    ])('catches one site in %s, and none for a closing tag', (source) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual(['KIT-23']);
    });

    it('counts one site per rule for a name with several branches, not one per branch', () => {
      expect(
        ruleIds(FEATURE_FILE, "const a = <Box component={x ? 'input' : 'button'} />;"),
      ).toEqual(['KIT-23']);
      expect(ruleIds(FEATURE_FILE, "const a = <Box component={x ? 'h1' : 'h2'} />;")).toEqual([
        'KIT-21',
      ]);
      expect(ruleIds(FEATURE_FILE, "const a = <Box component={x ? 'h1' : 'button'} />;")).toEqual([
        'KIT-21',
        'KIT-23',
      ]);
    });

    it.each([
      ['const a = <Box component={tag} />;'],
      ['const a = <Box component={`${tag}`} />;'],
      ["const a = <Box component={wide ? 'span' : 'div'} />;"],
      ['const a = <Box component={Wrapper} />;'],
      ['const a = <Box as="span" />;'],
      ["import { createElement } from 'react';\nconst a = createElement(Box, null);"],
      ["import { createElement } from 'react';\nconst a = createElement('div', null);"],
      ["import styled from '@emotion/styled';\nconst S = styled.div`color: red;`;"],
      ["const S = styled('div')({});"],
      ["const el = document.createElement('a');"],
      ['const TAG = pick();\nconst a = <Box component={TAG} />;'],
    ])(
      'lets %s through — not an element the guard names, or not knowable from the file',
      (source) => {
        expect(ruleIds(FEATURE_FILE, source)).toEqual([]);
      },
    );

    it('is not KIT-23 for the table cell’s own `th`, which KIT-8 owns', () => {
      expect(
        ruleIds(
          FEATURE_FILE,
          `import TableCell from '@mui/material/TableCell';\nconst a = <TableCell component="th" />;`,
        ),
      ).toEqual(['KIT-8', 'KIT-8']);
    });
  });

  describe('heading elements and the shell’s seam (KIT-21, KIT-22)', () => {
    it.each([
      ['const a = <h1>x</h1>;'],
      ['const a = <h3 />;'],
      ['const a = <h6>x</h6>;'],
      ["const a = <Typography component='h2'>x</Typography>;"],
      ["const a = <Box component={'h4'} />;"],
      [
        'import Typography from \'@mui/material/Typography\';\nconst a = <Typography variant="h2">x</Typography>;',
      ],
      [
        'import { Typography } from \'@mui/material\';\nconst a = <Typography variant="subtitle2">x</Typography>;',
      ],
      ["const S = styled('h2')({});"],
      ["const o = { slotProps: { primary: { component: 'h3' } } };"],
      // R1-3: the kit's own idiom (`component={`h${level}`}`) and the other non-literal roads.
      ['const a = <Typography component={`h${level}`}>x</Typography>;'],
      ['const a = <Box component={`h${level + 1}`} />;'],
      ["const a = <Box component={big ? 'h1' : 'h2'} />;"],
      ["const a = <Box component={heading ?? 'h2'} />;"],
      ["const HEAD = 'h2';\nconst a = <Box component={HEAD} />;"],
      ["const o = { component: big ? 'h1' : 'h2' };"],
      [
        "import Typography from '@mui/material/Typography';\nconst a = <Typography variant={big ? 'h1' : 'h2'}>x</Typography>;",
      ],
      [
        "import Typography from '@mui/material/Typography';\nconst a = <Typography variant={`h${level}`}>x</Typography>;",
      ],
      [
        "import Typography from '@mui/material/Typography';\nconst a = <Typography {...{ variant: 'h2' }}>x</Typography>;",
      ],
      [
        "import Typography from '@mui/material/Typography';\nconst props = { variant: 'h2' };\nconst a = <Typography {...props}>x</Typography>;",
      ],
      [
        "import Typography from '@mui/material/Typography';\nconst VARIANT = 'subtitle1';\nconst a = <Typography variant={VARIANT}>x</Typography>;",
      ],
      // R1-4: a role makes a heading as well as an element does.
      ['const a = <div role="heading" aria-level={2}>x</div>;'],
      ["const a = <Box role={'heading'} />;"],
      ["const o = { role: 'heading' };"],
      ["import { jsx } from 'react/jsx-runtime';\nconst a = jsx('h2', {});"],
      ["import styled from '@emotion/styled';\nconst S = styled.h2`margin: 0;`;"],
      ['const a = <Box as="h3" />;'],
    ])('catches %s', (source) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual(['KIT-21']);
    });

    it.each([
      [
        "import Typography from '@mui/material/Typography';\nconst a = <Typography variant={big ? 'h2' : 'body1'} component=\"p\">x</Typography>;",
      ],
      [
        "import Typography from '@mui/material/Typography';\nconst a = <Typography variant={size}>x</Typography>;",
      ],
      [
        "import Typography from '@mui/material/Typography';\nconst a = <Typography {...{ variant: 'body1' }}>x</Typography>;",
      ],
      [
        "import Typography from '@mui/material/Typography';\nconst a = <Typography {...{ variant: 'h2', component: 'p' }}>x</Typography>;",
      ],
      ['const a = <div role="presentation" />;'],
      ['const a = <Box component={`p${n}`} />;'],
    ])('lets %s through', (source) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual([]);
    });

    it('counts a component prop once, whatever the variant says', () => {
      const source = `import Typography from '@mui/material/Typography';\nconst a = <Typography variant="h1" component="h1">x</Typography>;`;
      expect(ruleIds(FEATURE_FILE, source)).toEqual(['KIT-21']);
    });

    it.each([
      [
        "import { usePageChrome } from '../../shared/ui/pageChrome';\nconst c = usePageChrome();",
        2,
      ],
      [
        "import { useDocumentTitle } from '../../shared/lib/documentTitle';\nuseDocumentTitle('t');",
        2,
      ],
      ["import { PageChromeContext } from '../../shared/ui/pageChrome';", 1],
      ["import { usePageChrome as chrome } from '../../shared/ui/pageChrome';\nchrome();", 2],
      ["export { useDocumentTitle } from '../../shared/lib/documentTitle';", 1],
      // R1-4: read through a namespace import.
      [
        "import * as chrome from '../../shared/ui/pageChrome';\nconst c = chrome.usePageChrome();",
        1,
      ],
      ["import * as title from '../../shared/lib/documentTitle';\ntitle.useDocumentTitle('t');", 1],
      [
        "import * as chrome from '../../shared/ui/pageChrome';\nconst C = chrome.PageChromeContext;",
        1,
      ],
    ])('catches %s as %i site(s)', (source, sites) => {
      expect(ruleIds(FEATURE_FILE, source)).toEqual(Array<string>(sites).fill('KIT-22'));
    });
  });

  describe('the layer rule (KIT-18)', () => {
    it.each([
      ['shared/lib/a.ts', "import { x } from '../../features/boards';"],
      ['shared/lib/a.ts', "import x from '../../app/theme';"],
      ['shared/ui/B.tsx', "import type { WidthClass } from '../../app/theme/tokens';"],
      ['shared/api/c.ts', "const m = import('../../features/x');"],
      ['shared/lib/d.ts', "export { y } from '../../app/y';"],
      ['shared/config/e.ts', "const m = require('../../features/z');"],
    ])('catches %s: %s', (file, source) => {
      expect(ruleIds(file, source)).toEqual(['KIT-18']);
    });

    it.each([
      ['shared/ui/B.tsx', "import y from './sibling';"],
      ['shared/ui/B.tsx', "import z from '../lib/format';"],
      ['shared/lib/a.ts', "import dayjs from 'dayjs';"],
      ['features/a/B.tsx', "import x from '../../app/x';"],
      ['shared/ui/B.test.tsx', "import x from '../../app/theme';"],
    ])('lets %s: %s through', (file, source) => {
      expect(ruleIds(file, source)).toEqual([]);
    });
  });
});

describe('tech-015 Scenario: the scan lets the allowed through (design § 9.4, § 9.5)', () => {
  it.each([
    ["// import TextField from '@mui/material/TextField'\nconst x = 1;"],
    ["/* <button> and <h1> and createSvgIcon and boxShadow: '1px 1px' */ const x = 1;"],
    ["const label = 'TextField, Button, Menu and <h1>';"],
    ['const a = <p>TextField Alert Dialog &lt;button&gt;</p>;'],
    ["const copy = 'Параметры .Mui и Mui-'; const nope = 'Mui'; const dot = '.mui';"],
    [
      "import Box from '@mui/material/Box';\nimport Stack from '@mui/material/Stack';\nimport Typography from '@mui/material/Typography';\nimport Link from '@mui/material/Link';\nimport Tooltip from '@mui/material/Tooltip';\nimport Switch from '@mui/material/Switch';\nconst a = <Box><Stack><Typography variant=\"body1\">x</Typography><Link /><Tooltip><Switch /></Tooltip></Stack></Box>;",
    ],
    [
      "import { alpha, styled, useTheme } from '@mui/material/styles';\nconst S = styled('div')({});",
    ],
    [
      "import AddIcon from '@mui/icons-material/Add';\nimport { Close } from '@mui/icons-material';",
    ],
    ["import AppBar from '@mui/material/AppBar';\nconst a = <AppBar elevation={0} />;"],
    ["import Drawer from '@mui/material/Drawer';\nconst a = <Drawer elevation={16} />;"],
    [
      'import Typography from \'@mui/material/Typography\';\nconst a = <Typography variant="h2" component="p">x</Typography>;',
    ],
    ['const a = <form><a href="#x">x</a><label>y</label><span /></form>;'],
    ["const sx = { boxShadow: 'none' };"],
    ['const sx = { boxShadow: 0, boxShadow2: 1 };'],
    ['const a = <Box boxShadow={0} />;'],
    ["import { Button } from '../kit';\nconst a = <Button />;"],
    ['const o = { component: Icon };'],
    ["const h = { 'aria-level': 2 };"],
  ])('lets %s through', (source) => {
    expect(ruleIds(FEATURE_FILE, source)).toEqual([]);
  });

  it('lets the same MUI imports and elements through under shared/ui, the kit itself', () => {
    const source = `import TextField from '@mui/material/TextField';\nimport Button from '@mui/material/Button';\nconst a = <TextField />;\nconst b = <h1>x</h1>;\nconst c = <input />;\nconst s = { '& .MuiSvgIcon-root': {}, boxShadow: 1 };`;
    expect(ruleIds('shared/ui/Kit.tsx', source)).toEqual([]);
    expect(ruleIds('shared/ui/actions/ActionButton.tsx', source)).toEqual([]);
  });

  it('lets a namespace import of the kit read what is not the page chrome', () => {
    const source = `import * as ui from '../../shared/ui';\nconst a = ui.usePageHead({ title: 't' });`;
    expect(ruleIds(FEATURE_FILE, source)).toEqual([]);
  });

  it('lets the two named seams use the page chrome, and only the page chrome', () => {
    const chrome = `import { PageChromeContext } from '../shared/ui/pageChrome';\nimport { useDocumentTitle } from '../shared/lib/documentTitle';\nconst c = <PageChromeContext.Provider value={x} />;\nuseDocumentTitle('t');`;
    expect(ruleIds('app/AppLayout.tsx', chrome)).toEqual([]);
    expect(ruleIds('features/uikit/showcase/demos/PageHeadDemo.tsx', chrome)).toEqual([]);
    expect(ruleIds('app/Other.tsx', chrome)).toContain('KIT-22');
    // The exemption is for KIT-22 only: a hand-rolled field in the seam still fails.
    expect(
      ruleIds('app/AppLayout.tsx', `import TextField from '@mui/material/TextField';`),
    ).toEqual(['KIT-1']);
  });

  it.each([
    ['features/demo/Demo.test.tsx'],
    ['features/demo/Demo.something.test.ts'],
    ['test/helper.tsx'],
    ['features/boards/testRender.tsx'],
    ['features/boards/testFixtures.ts'],
    ['features/dictionaries/testFixtures.ts'],
    ['features/workitems/testApi.ts'],
    ['shared/ui/lib.d.ts'],
  ])('does not read the test source %s', (file) => {
    expect(
      ruleIds(file, `import TextField from '@mui/material/TextField';\nconst a = <h1 />;`),
    ).toEqual([]);
  });

  // R1-4: the exemption is a list of today's helpers, not a name pattern a production file can match.
  it.each([
    ['features/workitems/testCaseSteps.tsx'],
    ['features/workitems/components/testPlan.tsx'],
    ['features/boards/testNewHelper.ts'],
    ['shared/lib/testing.ts'],
  ])('reads %s: a production file named like a helper is not one', (file) => {
    expect(ruleIds(file, `import TextField from '@mui/material/TextField';`)).toEqual(['KIT-1']);
  });

  it('lists only helpers that exist, each in a folder the guard reads', () => {
    for (const helper of TEST_HELPERS) {
      expect(existsSync(path.join(SRC, helper)), helper).toBe(true);
      expect(path.posix.basename(helper), helper).toMatch(/^test[A-Z]/);
    }
  });

  it('does not read what is out of scope: app/theme and the entry point', () => {
    const source = `import { createSvgIcon } from '@mui/material/SvgIcon';\nconst I = createSvgIcon(<path d='' />, 'X');\nconst s = { boxShadow: 1 };`;
    expect(ruleIds('app/theme/CrownIcon.tsx', source)).toEqual([]);
    expect(ruleIds('main.tsx', source)).toEqual([]);
  });
});

describe('tech-015 Scenario: no relational selector (review R1-2)', () => {
  it.each([
    ['const sx = { \'&:has([aria-expanded="true"])\': { opacity: 1 } };', [1]],
    ['const sx = {\n  [`&:has(${state}) .x`]: {},\n};', [2]],
    ["const sx = { '& :has( > svg)': {} };", [1]],
    ["const a = 'x';\nconst sx = { '&:HAS(.a)': {} };", [2]],
  ])('finds the selector in %s', (source, lines) => {
    expect(relationalSelectorLines('shared/ui/Field.tsx', source)).toEqual(lines);
  });

  it.each([
    ["// the old rule was '&:has([aria-expanded])'\nconst x = 1;"],
    ['/* :has( is slow in jsdom */ const x = 1;'],
    ["const sx = { '&[data-open=\"true\"]': { opacity: 1 }, '&:hover': {} };"],
    ["const copy = 'Это есть хорошо';"],
    ["const has = new Set(); has.add(1); const sx = { '&:hash': {} };"],
  ])('lets %s through', (source) => {
    expect(relationalSelectorLines('shared/ui/Field.tsx', source)).toEqual([]);
  });

  it('does not read a test, which may name the selector it replaced', () => {
    expect(
      relationalSelectorLines('shared/ui/fields/Field.test.tsx', "const s = ':has(a)';"),
    ).toEqual([]);
  });
});

describe('tech-015 Scenario: the allow-list is a ratchet (design § 9.3)', () => {
  const environment: ListEnvironment = { fileExists: () => true, isComplete: () => false };
  const file = 'features/demo/Demo.tsx';
  const baseline: BaselineEntry[] = [{ file, rule: 'KIT-1', count: 3 }];
  const entry = (patch: Partial<AllowedEntry> = {}): AllowedEntry => ({
    file,
    rule: 'KIT-1',
    count: 3,
    until: 'tech-018',
    reason: 'the demo predates the kit',
    ...patch,
  });
  const found = (lines: number[], rule = 'KIT-1'): Found[] => [{ file, rule, lines }];

  it('passes when the list matches the scan exactly', () => {
    expect(compareWithList(found([1, 5, 9]), [entry()], baseline, environment)).toEqual([]);
  });

  it('fails a violation that is not listed, with its lines and the part to use', () => {
    const [message, ...rest] = compareWithList(found([4, 8]), [], baseline, environment);
    expect(rest).toEqual([]);
    expect(message).toContain(`${file} KIT-1: 2 new site(s) at lines 4, 8`);
    expect(message).toContain('InlineTextField');
  });

  it('fails a third site in a file whose entry says 2', () => {
    const failures = compareWithList(
      found([1, 2, 3]),
      [entry({ count: 2 })],
      baseline,
      environment,
    );
    expect(failures).toHaveLength(1);
    expect(failures[0]).toContain('1 new site(s)');
    expect(failures[0]).toContain('lines 1, 2, 3');
  });

  it('fails an entry of 4 for 3 sites as stale, and an entry for none as stale', () => {
    const stale = compareWithList(found([1, 2, 3]), [entry({ count: 4 })], baseline, environment);
    expect(stale.some((message) => message.includes('lower it to 3'))).toBe(true);
    const gone = compareWithList([], [entry()], baseline, environment);
    expect(gone).toEqual([`${file} KIT-1: stale — the file has none; remove the entry`]);
  });

  it('fails an entry for a file or rule the baseline does not know', () => {
    const newcomer = 'features/demo/Newer.tsx';
    const failures = compareWithList(
      [{ file: newcomer, rule: 'KIT-1', lines: [1] }],
      [entry({ file: newcomer, count: 1 })],
      baseline,
      environment,
    );
    expect(failures).toEqual([
      `${newcomer} KIT-1: not in the baseline — fix the code, not the list`,
    ]);
    const otherRule = compareWithList(
      found([1], 'KIT-2'),
      [entry({ rule: 'KIT-2', count: 1 })],
      baseline,
      environment,
    );
    expect(otherRule[0]).toContain('not in the baseline');
  });

  it('fails a count above the baseline’s, even when the file has that many sites', () => {
    const failures = compareWithList(
      found([1, 2, 3, 4]),
      [entry({ count: 4 })],
      baseline,
      environment,
    );
    expect(failures).toEqual([
      `${file} KIT-1: lists 4, the baseline has 3 — fix the code, not the list`,
    ]);
  });

  it('fails an `until` that is not one of the queued tasks, and one whose task is complete', () => {
    const typo = compareWithList(
      found([1, 2, 3]),
      [entry({ until: 'tech-021' as AllowedEntry['until'] })],
      baseline,
      environment,
    );
    expect(typo[0]).toContain(`until 'tech-021' is not one of tech-016, tech-017`);
    const overdue = compareWithList(found([1, 2, 3]), [entry()], baseline, {
      ...environment,
      isComplete: (until) => until === 'tech-018',
    });
    expect(overdue).toEqual([
      `${file} KIT-1: tech-018 is complete — its entries must already be gone`,
    ]);
  });

  it('fails an empty reason, a missing file, a duplicate and a list out of order', () => {
    const base = [...baseline, { file: 'a/First.tsx', rule: 'KIT-1', count: 1 }];
    expect(
      compareWithList(found([1, 2, 3]), [entry({ reason: '  ' })], baseline, environment),
    ).toEqual([`${file} KIT-1: the reason is empty`]);
    expect(
      compareWithList(found([1, 2, 3]), [entry()], baseline, {
        ...environment,
        fileExists: () => false,
      }).some((message) => message.includes('no such file')),
    ).toBe(true);
    expect(
      compareWithList(found([1, 2, 3]), [entry(), entry()], baseline, environment).some((message) =>
        message.includes('listed twice'),
      ),
    ).toBe(true);
    const unsorted = compareWithList(
      [...found([1, 2, 3]), { file: 'a/First.tsx', rule: 'KIT-1', lines: [1] }],
      [entry(), entry({ file: 'a/First.tsx', count: 1 })],
      base,
      environment,
    );
    expect(unsorted.some((message) => message.includes('out of order'))).toBe(true);
  });

  it('orders rules numerically: KIT-2 before KIT-10', () => {
    const two = { file, rule: 'KIT-2', count: 1 };
    const ten = { file, rule: 'KIT-10', count: 1 };
    expect(byFileThenRule(two, ten)).toBeLessThan(0);
    expect(byFileThenRule(ten, two)).toBeGreaterThan(0);
  });

  it('reads a task’s completion from its own status.md, and a task with no folder is not complete', () => {
    // mod-015 is merged for good; no folder starts with the made-up id.
    expect(isTaskComplete('mod-015')).toBe(true);
    expect(isTaskComplete('zzz-000')).toBe(false);
  });

  it('renders the baseline file the emitter prints, one line per (file, rule)', () => {
    const text = renderBaseline(
      [
        { file: 'features/a/B.tsx', rule: 'KIT-2', lines: [1, 2, 3] },
        { file: 'features/a/B.tsx', rule: 'KIT-10', lines: [9] },
      ],
      'abc1234',
    );
    expect(text).toContain("export const BASELINE_COMMIT = 'abc1234';");
    expect(text).toContain("{ file: 'features/a/B.tsx', rule: 'KIT-2', count: 3 },");
    expect(text).toContain("{ file: 'features/a/B.tsx', rule: 'KIT-10', count: 1 },");
  });
});

describe('tech-015 Scenario: the tree holds the rule (design § 9.2, § 9.3)', () => {
  const scan = EMIT || !existsSync(ROOT) ? undefined : scanTree(ROOT);

  it.runIf(EMIT)('prints the baseline instead of asserting (KIT_GUARD_EMIT=baseline)', () => {
    const { found } = scanTree(ROOT);
    console.log(
      `KIT_GUARD_BASELINE_BEGIN\n${renderBaseline(found, BASELINE_COMMIT)}KIT_GUARD_BASELINE_END`,
    );
  });

  it.skipIf(EMIT)('sees the slices the rule is about — the scan is not vacuous', () => {
    const files = (scan as { files: string[] }).files;
    for (const prefix of [
      'features/boards/',
      'features/workitems/',
      'app/',
      'shared/lib/',
      'shared/ui/',
    ]) {
      expect(
        files.some((file) => file.startsWith(prefix) && rulesFor(file).size > 0),
        prefix,
      ).toBe(true);
    }
  });

  it.skipIf(EMIT)(
    'holds no relational :has( selector in any source — the kit, the shell or a slice (R1-2)',
    () => {
      const offenders = (scan as { files: string[] }).files.flatMap((file) =>
        relationalSelectorLines(file, readFileSync(path.join(ROOT, file), 'utf8')).map(
          (line) => `${file}:${line}`,
        ),
      );
      expect(
        offenders,
        'set a data attribute on the component and select on it (FieldFrame data-open)',
      ).toEqual([]);
    },
  );

  it.skipIf(EMIT)('finds no violation beyond the allow-list, and nothing stale in it', () => {
    const failures = compareWithList((scan as { found: Found[] }).found, ALLOWED, BASELINE, LIVE);
    expect(failures).toEqual([]);
  });

  it.skipIf(EMIT)('keeps the baseline sorted, without a duplicate, and cut from one commit', () => {
    expect(BASELINE_COMMIT).toMatch(/^[0-9a-f]{7,40}$/);
    BASELINE.forEach((entry, index) => {
      expect(entry.count, `${entry.file} ${entry.rule}`).toBeGreaterThan(0);
      const previous = BASELINE[index - 1];
      if (previous) {
        expect(
          byFileThenRule(previous, entry),
          `${previous.file} ${previous.rule} → ${entry.file} ${entry.rule}`,
        ).toBeLessThan(0);
      }
    });
  });

  it.skipIf(EMIT)('holds at least one baseline entry of every rule that had an offender', () => {
    const present = new Set(BASELINE.map((entry) => entry.rule));
    // The re-cut at 554bb6e (mod-016): KIT-14 and KIT-17 had their last offenders in the
    // work-item form and the boards' icons, which feat-029 and tech-015 moved into the kit.
    for (const rule of [
      ...Array.from({ length: 11 }, (_unused, index) => `KIT-${index + 1}`),
      'KIT-15',
      'KIT-16',
      'KIT-21',
      'KIT-22',
    ]) {
      expect(present.has(rule), `${rule} has an offender in the baseline`).toBe(true);
    }
  });

  it.skipIf(EMIT)('matches the totals of the cut — files and sites per rule, 125 entries', () => {
    // The totals of the cut at 554bb6e. The table began as the designer’s independent
    // scan of commit bd3bb22 (design § 9.2, last column; the re-cut from 5614f58 changed
    // no entry: bug-004 added no site), a difference being a bug in a rule, explained or
    // fixed before the baseline is committed. A re-cut brings it to the new cut, in the
    // same commit: this one (mod-016, rebased onto tech-015 and feat-029) dropped the
    // work-item form's and the boards' icons’ entries, which the kit and the card
    // replaced (KIT-14, KIT-17 now 0), and added the organization members' screens
    // and the shell's role-change notice.
    const CUT_TOTALS: Readonly<Record<string, readonly [files: number, sites: number]>> = {
      'KIT-1': [12, 46],
      'KIT-2': [19, 52],
      'KIT-3': [14, 54],
      'KIT-4': [5, 10],
      'KIT-5': [10, 24],
      'KIT-6': [7, 14],
      'KIT-7': [14, 32],
      'KIT-8': [16, 201],
      'KIT-9': [1, 3],
      'KIT-10': [10, 23],
      'KIT-11': [2, 4],
      'KIT-12': [0, 0],
      'KIT-13': [0, 0],
      'KIT-14': [0, 0],
      'KIT-15': [8, 11],
      'KIT-16': [2, 5],
      'KIT-17': [0, 0],
      'KIT-18': [0, 0],
      'KIT-21': [4, 4],
      'KIT-22': [1, 2],
      'KIT-23': [0, 0],
      'KIT-24': [0, 0],
    };
    const totals = (rule: string): [number, number] => {
      const entries = BASELINE.filter((entry) => entry.rule === rule);
      return [entries.length, entries.reduce((sum, entry) => sum + entry.count, 0)];
    };
    for (const [rule, expected] of Object.entries(CUT_TOTALS)) {
      expect(totals(rule), rule).toEqual([...expected]);
    }
    expect(BASELINE).toHaveLength(125);
    expect(new Set(BASELINE.map((entry) => entry.rule))).toEqual(
      new Set(
        Object.entries(CUT_TOTALS)
          .filter(([, [files]]) => files > 0)
          .map(([rule]) => rule),
      ),
    );
  });
});
