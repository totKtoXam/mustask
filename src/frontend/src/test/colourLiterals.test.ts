import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';
import { describe, expect, it } from 'vitest';

/**
 * The colour check of BR-1 (mod-015 design § 2.7, plan § 7), strengthened by
 * tech-015 (design § 9.7): `app/theme` is the only source of appearance, so no
 * file under `src/` outside `app/theme/`, the test files (`*.test.*`) and
 * `src/test/` may hold
 *
 * 1. a colour anywhere in a literal — a hex colour (3, 4, 6 or 8 digits) or a
 *    colour function (`rgb`, `rgba`, `hsl`, `hsla`, `hwb`, `lab`, `lch`,
 *    `oklab`, `oklch`, `color-mix`) inside a string, a template without
 *    substitutions, or the head and each span of a template — so
 *    `'1px solid #E0E0E0'`, `` `0 0 0 1px rgba(0,0,0,.1)` `` and
 *    `'linear-gradient(#fff, #000)'` fail; the colour is bounded on the left by
 *    the start, a space, a comma or `(` and on the right by the end, a space, a
 *    comma, `)` or `;`, so an address (`'#main-content'`, `'#12'`) is not one;
 * 2. a named colour or a default-palette path as the value of a colour
 *    property (`color`, `bgcolor`, `background`, `backgroundImage`, `border` and its
 *    sides, physical and logical, each `…Color`, `outline`, `fill`, `stroke`,
 *    `boxShadow`, `textShadow`, `filter`…): `'white'`, `'1px solid white'`,
 *    `'grey.300'`, `'common.black'` — also through a `const` that feeds the
 *    property (`const INK = 'black'; color: INK`); and, anywhere, the property
 *    access `palette.grey` / `palette.common` (`theme.palette.grey[300]`), taken
 *    by destructuring too (`const { grey } = theme.palette`). The theme's own roles
 *    (`'primary.main'`, `'text.secondary'`, `'divider'`, `'shell.rowSelected'`)
 *    pass, and so do `transparent`, `inherit`, `currentColor` and `none`;
 * 3. MUI's default palette by import — `@mui/material/colors` in any form, and
 *    `import { colors } from '@mui/material'` (tech-015 review R1-5).
 *
 * A literal that holds a Cyrillic letter is copy («Задача #123 создана»): only
 * the whole-literal rule of BR-1 — the literal *is* a hex colour or *begins*
 * with `rgb(`, `rgba(`, `hsl(`, `hsla(` — applies to it.
 *
 * Each file is parsed with the TypeScript compiler, so a comment or JSX text
 * («#12» in copy) is never taken for a literal. An address anchor made only of
 * hex letters (`'#add'`) would match, as BR-1 words the rule; none exists, and
 * one would be renamed rather than exempted — BR-1 keeps no exception list. The
 * scan of the tree finds no offender, so the strengthened rule needs no
 * allow-list.
 *
 * Dimensions are not machine-checked: code review holds them to theme tokens
 * and the spacing unit (BR-1).
 */

// The whole-literal rule of BR-1: the literal is a colour, or begins with a function.
const HEX_COLOUR = /^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;
const FUNCTIONAL_COLOUR = /^(?:rgb|rgba|hsl|hsla)\(/i;

// A colour inside a longer literal, with its boundaries (design § 9.7 item 1): `!` ends one
// too, as in `'1px solid #E0E0E0!important'`.
const EMBEDDED_HEX =
  /(?<=^|[\s,(])#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})(?=$|[\s,);!])/;
const EMBEDDED_FUNCTION = /(?<=^|[\s,(])(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color-mix)\(/i;
const CYRILLIC = /[А-Яа-яЁё]/;

/**
 * The properties whose value is a colour (design § 9.7 item 2); a name ending `Color` is one
 * too. The shorthands and the images count: a colour sits in `border`, in a shadow, in a
 * gradient, in a drop-shadow (tech-015 review R1-5).
 */
const COLOUR_PROPERTIES = new Set([
  'color',
  'bgcolor',
  'background',
  'backgroundImage',
  'border',
  'borderTop',
  'borderRight',
  'borderBottom',
  'borderLeft',
  'borderInline',
  'borderInlineStart',
  'borderInlineEnd',
  'borderBlock',
  'borderBlockStart',
  'borderBlockEnd',
  'borderImage',
  'columnRule',
  'outline',
  'fill',
  'stroke',
  'boxShadow',
  'textShadow',
  'textDecoration',
  'filter',
  'backdropFilter',
]);

/** MUI's own default palette by import, which the tokens exist to replace (design § 9.7). */
const DEFAULT_COLOURS_MODULE = /^@mui\/material\/colors(?:\/|$)/;

/**
 * CSS Color Module 4's named colours. `transparent`, `currentColor` and `inherit`
 * are not colours to name, and `none` is no colour at all.
 */
const CSS_COLOUR_KEYWORDS = new Set(
  (
    'aliceblue antiquewhite aqua aquamarine azure beige bisque black blanchedalmond blue ' +
    'blueviolet brown burlywood cadetblue chartreuse chocolate coral cornflowerblue cornsilk ' +
    'crimson cyan darkblue darkcyan darkgoldenrod darkgray darkgreen darkgrey darkkhaki ' +
    'darkmagenta darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen ' +
    'darkslateblue darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue ' +
    'dimgray dimgrey dodgerblue firebrick floralwhite forestgreen fuchsia gainsboro ghostwhite ' +
    'gold goldenrod gray green greenyellow grey honeydew hotpink indianred indigo ivory khaki ' +
    'lavender lavenderblush lawngreen lemonchiffon lightblue lightcoral lightcyan ' +
    'lightgoldenrodyellow lightgray lightgreen lightgrey lightpink lightsalmon lightseagreen ' +
    'lightskyblue lightslategray lightslategrey lightsteelblue lightyellow lime limegreen linen ' +
    'magenta maroon mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen ' +
    'mediumslateblue mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream ' +
    'mistyrose moccasin navajowhite navy oldlace olive olivedrab orange orangered orchid ' +
    'palegoldenrod palegreen paleturquoise palevioletred papayawhip peachpuff peru pink plum ' +
    'powderblue purple rebeccapurple red rosybrown royalblue saddlebrown salmon sandybrown ' +
    'seagreen seashell sienna silver skyblue slateblue slategray slategrey snow springgreen ' +
    'steelblue tan teal thistle tomato turquoise violet wheat white whitesmoke yellow yellowgreen'
  ).split(' '),
);

/** A path into MUI's default palette that bypasses the tokens: `grey.300`, `grey.A100`, `common.black`. */
const DEFAULT_PALETTE_PATH = /^(?:grey\.A?\d+|common\.[A-Za-z]+)$/;

interface Finding {
  line: number;
  literal: string;
}

function isWholeColourLiteral(text: string): boolean {
  return HEX_COLOUR.test(text) || FUNCTIONAL_COLOUR.test(text);
}

function holdsColour(text: string): boolean {
  if (isWholeColourLiteral(text)) return true;
  if (CYRILLIC.test(text)) return false;
  return EMBEDDED_HEX.test(text) || EMBEDDED_FUNCTION.test(text);
}

function namesColour(text: string): boolean {
  return text
    .split(/[\s,()/]+/)
    .some((word) => CSS_COLOUR_KEYWORDS.has(word.toLowerCase()) || DEFAULT_PALETTE_PATH.test(word));
}

/** `border-color` and `borderColor` are one name. */
function camelCase(name: string): string {
  return name.replace(/-([a-z])/g, (_match, letter: string) => letter.toUpperCase());
}

function isColourProperty(name: string): boolean {
  const camel = camelCase(name);
  return COLOUR_PROPERTIES.has(camel) || camel.endsWith('Color');
}

/** The name of an object property or a JSX attribute, when it is written out. */
function writtenName(name: ts.Node): string | undefined {
  if (
    ts.isIdentifier(name) ||
    ts.isStringLiteral(name) ||
    ts.isNoSubstitutionTemplateLiteral(name)
  ) {
    return name.text;
  }
  return undefined;
}

/** Every `const name = initialiser` of the file: a colour kept in a constant is still the value. */
type Constants = ReadonlyMap<string, readonly ts.Expression[]>;

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

/**
 * The literal parts a value can be: the value itself, the parts of a template,
 * the branches of a conditional, `||`, `??`, `&&` and parenthesis, and the
 * initialiser of a `const` the value names (`const INK = 'black'; color: INK`) —
 * never a call's argument or a comparison, so `x === 'white'` is not a colour.
 */
function valueParts(
  expression: ts.Expression | undefined,
  constants: Constants,
  depth = 0,
): ts.Node[] {
  if (!expression || depth > 6) return [];
  if (ts.isStringLiteral(expression) || ts.isNoSubstitutionTemplateLiteral(expression)) {
    return [expression];
  }
  if (ts.isTemplateExpression(expression)) {
    return [expression.head, ...expression.templateSpans.map((span) => span.literal)];
  }
  if (ts.isParenthesizedExpression(expression) || ts.isAsExpression(expression)) {
    return valueParts(expression.expression, constants, depth + 1);
  }
  if (ts.isSatisfiesExpression(expression) || ts.isNonNullExpression(expression)) {
    return valueParts(expression.expression, constants, depth + 1);
  }
  if (ts.isConditionalExpression(expression)) {
    return [
      ...valueParts(expression.whenTrue, constants, depth + 1),
      ...valueParts(expression.whenFalse, constants, depth + 1),
    ];
  }
  if (ts.isBinaryExpression(expression)) {
    const operator = expression.operatorToken.kind;
    if (
      operator === ts.SyntaxKind.BarBarToken ||
      operator === ts.SyntaxKind.QuestionQuestionToken ||
      operator === ts.SyntaxKind.AmpersandAmpersandToken
    ) {
      return [
        ...valueParts(expression.left, constants, depth + 1),
        ...valueParts(expression.right, constants, depth + 1),
      ];
    }
  }
  if (ts.isIdentifier(expression)) {
    return (constants.get(expression.text) ?? []).flatMap((value) =>
      valueParts(value, constants, depth + 1),
    );
  }
  return [];
}

function partText(node: ts.Node): string | undefined {
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
 * `palette.grey` or `palette.common` — MUI's default colours, read off the theme
 * object so they bypass the tokens — as `palette.grey`; `undefined` for any
 * other node.
 */
function defaultPaletteAccess(node: ts.Node): string | undefined {
  const isPalette = (expression: ts.Expression): boolean =>
    (ts.isIdentifier(expression) && expression.text === 'palette') ||
    (ts.isPropertyAccessExpression(expression) && expression.name.text === 'palette');
  let member: string | undefined;
  if (ts.isPropertyAccessExpression(node) && isPalette(node.expression)) {
    member = node.name.text;
  } else if (
    ts.isElementAccessExpression(node) &&
    ts.isStringLiteralLike(node.argumentExpression) &&
    isPalette(node.expression)
  ) {
    member = node.argumentExpression.text;
  }
  return member === 'grey' || member === 'common' ? `palette.${member}` : undefined;
}

/** Whether an expression is the theme's palette: `palette`, `theme.palette`, `useTheme().palette`. */
function isPaletteExpression(expression: ts.Expression): boolean {
  return (
    (ts.isIdentifier(expression) && expression.text === 'palette') ||
    (ts.isPropertyAccessExpression(expression) && expression.name.text === 'palette')
  );
}

/**
 * The default colours read by destructuring: `const { grey } = theme.palette`,
 * `const { palette: { common } } = theme`. Returns the member's name (`grey`, `common`)
 * for each binding element that takes one, as `palette.grey`.
 */
function defaultPaletteBindings(node: ts.Node): { node: ts.Node; literal: string }[] {
  if (!ts.isObjectBindingPattern(node)) return [];
  const fromPalette =
    ts.isVariableDeclaration(node.parent) &&
    node.parent.initializer !== undefined &&
    isPaletteExpression(node.parent.initializer);
  const inPalette =
    ts.isBindingElement(node.parent) &&
    node.parent.propertyName !== undefined &&
    writtenName(node.parent.propertyName) === 'palette';
  if (!fromPalette && !inPalette) return [];
  return node.elements.flatMap((element) => {
    const member = writtenName(element.propertyName ?? element.name);
    return member === 'grey' || member === 'common'
      ? [{ node: element, literal: `palette.${member}` }]
      : [];
  });
}

function findColourLiterals(fileName: string, source: string): Finding[] {
  const kind = fileName.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const sourceFile = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true, kind);
  const constants = collectConstants(sourceFile);
  const found = new Map<number, Finding>();

  const report = (node: ts.Node, literal: string): void => {
    const start = node.getStart(sourceFile);
    if (found.has(start)) return;
    const { line } = sourceFile.getLineAndCharacterOfPosition(start);
    found.set(start, { line: line + 1, literal });
  };

  const checkColourValue = (value: ts.Expression | undefined): void => {
    for (const part of valueParts(value, constants)) {
      const text = partText(part);
      if (text !== undefined && !CYRILLIC.test(text) && namesColour(text)) report(part, text);
    }
  };

  const visit = (node: ts.Node): void => {
    const text = partText(node);
    if (text !== undefined && holdsColour(text)) report(node, text);
    // MUI's default palette by import: `@mui/material/colors`, however it is reached.
    if (text !== undefined && DEFAULT_COLOURS_MODULE.test(text)) report(node, text);
    // `import { colors } from '@mui/material'` is the same palette under another door.
    if (
      ts.isImportSpecifier(node) &&
      (node.propertyName ?? node.name).text === 'colors' &&
      ts.isImportDeclaration(node.parent.parent.parent) &&
      ts.isStringLiteral(node.parent.parent.parent.moduleSpecifier) &&
      /^@mui\/material(?:\/styles)?$/.test(node.parent.parent.parent.moduleSpecifier.text)
    ) {
      report(node, 'colors');
    }
    for (const binding of defaultPaletteBindings(node)) report(binding.node, binding.literal);

    if (ts.isPropertyAssignment(node)) {
      const name = writtenName(node.name);
      if (name !== undefined && isColourProperty(name)) checkColourValue(node.initializer);
    } else if (ts.isJsxAttribute(node)) {
      const name = ts.isIdentifier(node.name) ? node.name.text : undefined;
      if (name !== undefined && isColourProperty(name)) {
        const initializer = node.initializer;
        checkColourValue(
          initializer && ts.isJsxExpression(initializer) ? initializer.expression : initializer,
        );
      }
    } else {
      const palette = defaultPaletteAccess(node);
      if (palette !== undefined) report(node, palette);
    }
    ts.forEachChild(node, visit);
  };
  visit(sourceFile);
  return [...found.values()].sort((a, b) => a.line - b.line);
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
      // A colour inside a shorthand, a template, a gradient.
      ["const sx = { border: '1px solid #E0E0E0' };", '1px solid #E0E0E0'],
      ['const sx = { boxShadow: `0 0 0 1px rgba(0,0,0,.1)` };', '0 0 0 1px rgba(0,0,0,.1)'],
      [
        "const sx = { backgroundImage: 'linear-gradient(#fff, #000)' };",
        'linear-gradient(#fff, #000)',
      ],
      [
        'const sx = { boxShadow: `inset 0 0 0 1px #0078D4 ${shadow}` };',
        'inset 0 0 0 1px #0078D4 ',
      ],
      ['const sx = { boxShadow: `${inset} 0 0 #00000033` };', ' 0 0 #00000033'],
      [
        "const sx = { filter: 'drop-shadow(0 0 1px hsl(0 0% 0%))' };",
        'drop-shadow(0 0 1px hsl(0 0% 0%))',
      ],
      ["const mix = 'color-mix(in srgb, red 50%, blue)';", 'color-mix(in srgb, red 50%, blue)'],
      ["const lab = 'oklch(70% 0.1 200)';", 'oklch(70% 0.1 200)'],
      // A named colour or the default palette as a colour property's value.
      ["const sx = { color: 'white' };", 'white'],
      ["const sx = { borderColor: 'common.black' };", 'common.black'],
      ["const sx = { 'background-color': 'red' };", 'red'],
      ["const sx = { border: '1px solid white' };", '1px solid white'],
      ["const sx = { color: active ? 'black' : 'text.primary' };", 'black'],
      ['const box = <Box bgcolor="grey.300" />;', 'grey.300'],
      ['const box = <Box sx={{ bgcolor: "grey.A100" }} />;', 'grey.A100'],
      ['const icon = <svg fill="black" />;', 'black'],
      ["const icon = <Icon stroke={'navy'} />;", 'navy'],
      ['const sx = { outlineColor: `1px solid ${x}` , caretColor: `gold` };', 'gold'],
      // The default palette read off the theme object, bypassing the tokens.
      ['const c = theme.palette.grey[300];', 'palette.grey'],
      ['const c = theme.palette.common.white;', 'palette.common'],
      ["const c = palette['grey'][500];", 'palette.grey'],
    ])('catches %s', (source, literal) => {
      expect(findColourLiterals('fixture.tsx', source)).toEqual([{ line: 1, literal }]);
    });

    // tech-015 review R1-5: the ways round the above that the guide's «all fail» covers.
    it.each([
      // More properties that hold a colour.
      ["const sx = { textShadow: '0 0 1px black' };", '0 0 1px black'],
      [
        "const sx = { backgroundImage: 'linear-gradient(white, black)' };",
        'linear-gradient(white, black)',
      ],
      [
        "const sx = { 'background-image': 'linear-gradient(to right, red, blue)' };",
        'linear-gradient(to right, red, blue)',
      ],
      ["const sx = { borderInline: '1px solid black' };", '1px solid black'],
      ["const sx = { borderBlockStart: '2px dashed red' };", '2px dashed red'],
      ['const sx = { borderInlineEnd: `1px solid ${w} white` };', ' white'],
      ["const sx = { filter: 'drop-shadow(0 0 2px black)' };", 'drop-shadow(0 0 2px black)'],
      ["const sx = { textDecoration: 'underline red' };", 'underline red'],
      // A colour kept in a constant is still the colour.
      ["const INK = 'black';\nconst sx = { color: INK };", 'black'],
      ["const EDGE = '1px solid white';\nconst sx = { borderTop: EDGE };", '1px solid white'],
      ["const INK = 'grey.500';\nconst box = <Box color={INK} />;", 'grey.500'],
      ["const INK = 'black';\nconst sx = { color: dark ? INK : 'text.primary' };", 'black'],
      // `!important` ends a colour.
      ["const sx = { border: '1px solid #E0E0E0!important' };", '1px solid #E0E0E0!important'],
      ["const sx = { boxShadow: '0 0 0 1px #0078D4!important' };", '0 0 0 1px #0078D4!important'],
      // MUI's default palette by import, however it is reached.
      ["import { red } from '@mui/material/colors';", '@mui/material/colors'],
      ["import red from '@mui/material/colors/red';", '@mui/material/colors/red'],
      ["import * as colours from '@mui/material/colors';", '@mui/material/colors'],
      ["export { grey } from '@mui/material/colors';", '@mui/material/colors'],
      ["const c = require('@mui/material/colors');", '@mui/material/colors'],
      ["const c = await import('@mui/material/colors');", '@mui/material/colors'],
      ["import { colors } from '@mui/material';", 'colors'],
      ["import { colors as palette } from '@mui/material/styles';", 'colors'],
      // The default colours taken off the theme by destructuring.
      ['const { grey } = useTheme().palette;', 'palette.grey'],
      ['const { common } = theme.palette;', 'palette.common'],
      ['const { common: white } = palette;', 'palette.common'],
      ['const { palette: { grey } } = theme;', 'palette.grey'],
      ['const fn = ({ palette: { common } }: Theme) => common.white;', 'palette.common'],
    ])('catches %s', (source, literal) => {
      expect(findColourLiterals('fixture.tsx', source)).toEqual([{ line: 1, literal }]);
    });

    it.each([
      ["const tooShort = '#12';"],
      ["const bare = '#';"],
      ["const anchor = '#main-content';"],
      ["const anchorWithSpace = 'see #main-content';"],
      ["const seven = '#1234567';"],
      ["const fragment = '/items#abcdef';"],
      ['const copy = <p>Задача #12</p>;'],
      ['const copy = <p>#0F6CBD</p>;'],
      ["const copy = 'Задача #123 создана';"],
      ["const copy = 'Цвет #FFF в тексте';"],
      ['// #0F6CBD in a comment\nconst x = 1;'],
      ['/* rgba(0, 0, 0, 0.5) */ const y = 2;'],
      ["const word = 'rgb';"],
      ["const slab = 'slab(1)';"],
      // The theme's own roles, and the words that name no colour.
      ["const sx = { color: 'primary.main' };"],
      ["const sx = { color: 'text.secondary', bgcolor: 'shell.rowSelected' };"],
      ["const sx = { borderColor: 'divider' };"],
      ["const sx = { color: 'inherit' };"],
      ["const sx = { background: 'transparent' };"],
      ["const sx = { fill: 'currentColor' };"],
      ["const sx = { border: '1px solid' };"],
      ["const sx = { boxShadow: 'none' };"],
      ['const box = <Box color="info" />;'],
      // A colour word that is not the value of a colour property.
      ["const option = { label: 'white' };"],
      ["const kind = tone === 'white' ? 1 : 2;"],
      ["const sx = { color: pick('white') };"],
      ["const sx = { content: 'red' };"],
      ['const theme = createTheme({ palette: { grey: custom } });'],
      ['const c = theme.palette.primary.main;'],
      ['const c = theme.palette.text.primary;'],
      // R1-5: the new properties with no colour in them, and a constant that feeds no colour.
      ["const sx = { textShadow: 'none', backgroundImage: 'none', filter: 'blur(2px)' };"],
      ["const sx = { borderInline: '1px solid', borderBlock: 'none' };"],
      ["const sx = { textDecoration: 'underline', backdropFilter: 'blur(4px)' };"],
      ["const sx = { borderInline: '1px solid', borderColor: 'divider' };"],
      ["const INK = 'black';\nconst label = INK;"],
      ['const sx = { color: INK };'],
      ["const INK = 'text.primary';\nconst sx = { color: INK };"],
      ["const INK = 'black';\nconst sx = { content: INK };"],
      // A module that is not MUI's palette, and a destructuring that is not the default colours.
      ["import { alpha } from '@mui/material/styles';"],
      ["import { colors } from './colors';"],
      ["const path = '@mui/material/colors-are-banned';"],
      ['const { primary, text, divider } = theme.palette;'],
      ['const { palette } = theme;'],
      ['const { palette: { primary } } = theme;'],
      ['const { grey } = options;'],
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
