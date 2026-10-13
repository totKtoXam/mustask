import { DESCRIPTION_MAX_MARKUP, DESCRIPTION_RAW_CEILING } from './limits';
import { readLinkAddress } from './linkAddress';
import {
  CODE_LANGUAGES,
  FONT_SIZES,
  HIGHLIGHTS,
  MARK_ORDER,
  TEXT_COLOURS,
  firstToken,
  type MarkKind,
  type VocabularyToken,
} from './vocabulary';
import {
  cleanText,
  isAllAsciiWhitespace,
  isUnicodeWhiteSpace,
  trimUnicodeWhiteSpace,
} from './whitespace';

/**
 * The client's normaliser (BR-RT-5, plan-frontend § 4.2, F-1): a line-for-line
 * port of the server's reduction — `HtmlRichTextReducer`, `DomConverter`,
 * `TableNormaliser`, `CanonicalWriter`, `LinkAddress` — over a `DOMParser`
 * tree, so that the editor's `getHTML()`, a paste and the legacy conversion
 * all produce the string the server would store, byte for byte. It is total
 * (never throws on a string), never touches the live document, never uses
 * `innerHTML`, and does not recurse on the depth of the DOM.
 *
 * The server's choices the spec is silent on (plan-backend § 6.2) are in the
 * port as the server made them; each is pinned by a shared fixture case.
 */

export interface NormalisedRichText {
  /** The canonical html; `''` when `hasContent` is false (BR-RT-6). */
  readonly html: string;
  /** BR-RT-8: visible text in Unicode code points, counted on the unblanked output. */
  readonly visibleLength: number;
  /** Code points of the unblanked canonical string. */
  readonly markupLength: number;
  /** A non-whitespace code point exists in the visible text. */
  readonly hasContent: boolean;
}

const WRAPPER = '<!DOCTYPE html><html><head></head><body>';
const HTML_NAMESPACE = 'http://www.w3.org/1999/xhtml';
const MAX_LIST_DEPTH = 3;
const MAX_COLUMN_SPAN = 1_000;
/** Each position of a table is at least `<td><p></p></td>`. */
const MIN_CELL_MARKUP = 16;
const REL = 'noopener noreferrer';

const DROPPED = new Set([
  'script',
  'style',
  'iframe',
  'object',
  'embed',
  'template',
  'svg',
  'math',
  'noscript',
  'head',
  'title',
  'link',
  'meta',
  'base',
  'form',
  'input',
  'textarea',
  'select',
  'button',
  'audio',
  'video',
  'canvas',
  'frame',
  'frameset',
  'applet',
  'img',
  'hr',
]);

class BudgetExceeded extends Error {}

// --- model ---------------------------------------------------------------------------------------------------------

interface Marks {
  readonly href: string | null;
  readonly bold: boolean;
  readonly italic: boolean;
  readonly underline: boolean;
  readonly strike: boolean;
  readonly highlight: VocabularyToken | null;
  readonly color: VocabularyToken | null;
  readonly size: VocabularyToken | null;
  readonly code: boolean;
}

const NO_MARKS: Marks = {
  href: null,
  bold: false,
  italic: false,
  underline: false,
  strike: false,
  highlight: null,
  color: null,
  size: null,
  code: false,
};

/** US-7: inline code wins over every mark but a link; a heading drops the size (BR-RT-15). */
function canonicalMarks(marks: Marks, inHeading: boolean): Marks {
  if (marks.code) {
    return { ...NO_MARKS, href: marks.href, code: true };
  }
  return inHeading && marks.size !== null ? { ...marks, size: null } : marks;
}

interface Inline {
  /** `null` is a line break. */
  readonly text: string | null;
  readonly marks: Marks;
}

const BREAK: Inline = { text: null, marks: NO_MARKS };

type LeafKind = 'p' | 'h3' | 'h4';

interface Leaf {
  readonly type: 'leaf';
  readonly kind: LeafKind;
  readonly items: Inline[];
}

interface CodeBlock {
  readonly type: 'code';
  readonly language: VocabularyToken | null;
  readonly text: string;
}

interface ListBlock {
  readonly type: 'list';
  readonly ordered: boolean;
  readonly items: ItemBlock[];
}

interface ItemBlock {
  readonly blocks: Block[];
}

interface CellBlock {
  readonly header: boolean;
  readonly columnSpan: number;
  readonly rowSpan: number;
  readonly paragraphs: Leaf[];
}

interface RowBlock {
  readonly group: number;
  readonly cells: CellBlock[];
}

interface TableBlock {
  readonly type: 'table';
  readonly rows: RowBlock[];
  group: number;
}

interface RectangularTable {
  readonly type: 'rect';
  readonly rows: CellBlock[][];
}

type Block = Leaf | CodeBlock | ListBlock | TableBlock | RectangularTable;

// --- containers ----------------------------------------------------------------------------------------------------

abstract class Container {
  abstract add(block: Block): void;
}

class RootContainer extends Container {
  readonly blocks: Block[] = [];

  add(block: Block): void {
    this.blocks.push(block);
  }
}

class ListContainer extends Container {
  private implicitItem: ItemBlock | null = null;

  readonly list: ListBlock;

  constructor(list: ListBlock) {
    super();
    this.list = list;
  }

  resetImplicit(): void {
    this.implicitItem = null;
  }

  /** Content directly in a list, outside any item, goes into an item of its own. */
  add(block: Block): void {
    if (this.implicitItem === null) {
      this.implicitItem = { blocks: [] };
      this.list.items.push(this.implicitItem);
    }
    this.implicitItem.blocks.push(block);
  }
}

class ItemContainer extends Container {
  private readonly item: ItemBlock;

  constructor(item: ItemBlock) {
    super();
    this.item = item;
  }

  add(block: Block): void {
    this.item.blocks.push(block);
  }
}

class TableContainer extends Container {
  /** Text and blocks outside any cell are put before the table, where a parser foster-parents them. */
  readonly before: Block[] = [];

  readonly table: TableBlock;

  constructor(table: TableBlock) {
    super();
    this.table = table;
  }

  add(block: Block): void {
    this.before.push(block);
  }
}

class RowContainer extends Container {
  private readonly tableContainer: TableContainer;
  readonly row: RowBlock;

  constructor(tableContainer: TableContainer, row: RowBlock) {
    super();
    this.tableContainer = tableContainer;
    this.row = row;
  }

  add(block: Block): void {
    this.tableContainer.add(block);
  }
}

class CellContainer extends Container {
  private readonly cell: CellBlock;

  constructor(cell: CellBlock) {
    super();
    this.cell = cell;
  }

  add(block: Block): void {
    if (block.type === 'leaf') {
      this.cell.paragraphs.push(block);
    }
  }
}

// --- the walk (DomConverter) ---------------------------------------------------------------------------------------

interface Frame {
  readonly element: Element;
  next: number;
  savedMarks: Marks;
  skipped: boolean;
  closeLeaf: boolean;
  pop: boolean;
  endPre: boolean;
}

function newFrame(element: Element): Frame {
  return {
    element,
    next: 0,
    savedMarks: NO_MARKS,
    skipped: false,
    closeLeaf: false,
    pop: false,
    endPre: false,
  };
}

class DomConverter {
  private readonly containers: Container[] = [];
  private currentLeaf: Leaf | null = null;
  private marks: Marks = NO_MARKS;
  private inPre = false;
  private preText = '';
  private preLanguage: VocabularyToken | null = null;
  private listDepth = 0;

  droppedWhitespace = 0;

  private readonly maxPositions: number;
  private readonly droppedLimit: number;

  constructor(maxPositions: number, droppedLimit: number) {
    this.maxPositions = maxPositions;
    this.droppedLimit = droppedLimit;
  }

  convert(body: Element): Block[] {
    const root = new RootContainer();
    this.containers.push(root);

    const stack: Frame[] = [newFrame(body)];

    while (stack.length > 0) {
      const frame = stack[stack.length - 1] as Frame;
      const children = frame.element.childNodes;

      if (frame.next >= children.length) {
        this.exit(frame);
        stack.pop();
        continue;
      }

      const child = children[frame.next++] as ChildNode;

      if (child.nodeType === 3) {
        this.onText((child as Text).data);
      } else if (child.nodeType === 1) {
        const next = newFrame(child as Element);
        if (this.enter(next)) {
          stack.push(next);
        } else {
          this.exit(next);
        }
      }
    }

    return root.blocks;
  }

  // --- elements ---

  /** Applies the element's meaning; `true` when its children are to be walked. */
  private enter(frame: Frame): boolean {
    const element = frame.element;
    const name = element.localName;
    frame.savedMarks = this.marks;

    if (element.namespaceURI !== HTML_NAMESPACE || DROPPED.has(name)) {
      frame.skipped = true;
      return false;
    }

    if (name === 'br') {
      this.onBreak();
      frame.skipped = true;
      return false;
    }

    if (this.inPre) return true;

    switch (name) {
      case 'p':
        this.startLeaf('p');
        frame.closeLeaf = true;
        break;
      case 'h1':
      case 'h2':
      case 'h3':
        this.startLeaf('h3');
        frame.closeLeaf = true;
        break;
      case 'h4':
      case 'h5':
      case 'h6':
        this.startLeaf('h4');
        frame.closeLeaf = true;
        break;
      case 'pre':
        this.closeLeaf();
        this.inPre = true;
        this.preText = '';
        this.preLanguage = this.languageOf(element);
        frame.endPre = true;
        break;
      case 'ul':
      case 'ol':
        this.startList(name === 'ol', frame);
        break;
      case 'li':
        this.startItem(frame);
        break;
      case 'table':
        this.startTable(frame);
        break;
      case 'thead':
      case 'tbody':
      case 'tfoot': {
        this.closeLeaf();
        const current = this.current();
        if (current instanceof TableContainer) current.table.group++;
        break;
      }
      case 'tr': {
        this.closeLeaf();
        const current = this.current();
        if (current instanceof TableContainer) {
          const row: RowBlock = { group: current.table.group, cells: [] };
          current.table.rows.push(row);
          this.containers.push(new RowContainer(current, row));
          frame.pop = true;
        }
        break;
      }
      case 'td':
      case 'th': {
        this.closeLeaf();
        const current = this.current();
        if (current instanceof RowContainer) {
          const cell: CellBlock = {
            header: name === 'th',
            columnSpan: spanOf(element, 'colspan'),
            rowSpan: spanOf(element, 'rowspan'),
            paragraphs: [],
          };
          current.row.cells.push(cell);
          this.containers.push(new CellContainer(cell));
          frame.pop = true;
        }
        break;
      }
      case 'strong':
      case 'b':
        this.marks = { ...this.marks, bold: true };
        break;
      case 'em':
      case 'i':
        this.marks = { ...this.marks, italic: true };
        break;
      case 'u':
        this.marks = { ...this.marks, underline: true };
        break;
      case 's':
      case 'strike':
      case 'del':
        this.marks = { ...this.marks, strike: true };
        break;
      case 'code':
        this.marks = { ...this.marks, code: true };
        break;
      case 'a': {
        const href = readLinkAddress(element.getAttribute('href'));
        if (href !== null) this.marks = { ...this.marks, href };
        break;
      }
      case 'mark': {
        const highlight = firstToken(HIGHLIGHTS, element.getAttribute('class'));
        if (highlight !== null) this.marks = { ...this.marks, highlight };
        break;
      }
      case 'span': {
        const classValue = element.getAttribute('class');
        const color = firstToken(TEXT_COLOURS, classValue);
        const size = firstToken(FONT_SIZES, classValue);
        this.marks = {
          ...this.marks,
          color: color ?? this.marks.color,
          size: size ?? this.marks.size,
        };
        break;
      }
      default:
        // Unknown elements are unwrapped: transparent, their text kept (US-2).
        break;
    }

    return true;
  }

  private exit(frame: Frame): void {
    if (frame.skipped) return;

    if (frame.endPre) this.endPre();

    if (frame.pop) {
      this.closeLeaf();
      this.popContainer();
    } else if (frame.closeLeaf) {
      this.closeLeaf();
    }

    this.marks = frame.savedMarks;
  }

  private languageOf(pre: Element): VocabularyToken | null {
    for (const child of Array.from(pre.children)) {
      if (child.localName === 'code' && child.namespaceURI === HTML_NAMESPACE) {
        return firstToken(CODE_LANGUAGES, child.getAttribute('class'));
      }
    }
    return null;
  }

  // --- text ---

  private onText(data: string): void {
    if (data.length === 0) return;

    if (this.inPre) {
      this.preText += data;
      return;
    }

    if (this.currentLeaf === null) {
      if (isAllAsciiWhitespace(data)) {
        // Whitespace between blocks is dropped, but it was read: it counts against the budget.
        this.droppedWhitespace += data.length;
        if (this.droppedWhitespace > this.droppedLimit) throw new BudgetExceeded();
        return;
      }
      this.startLeaf('p');
    }

    const leaf = this.currentLeaf as Leaf;
    leaf.items.push({ text: data, marks: canonicalMarks(this.marks, leaf.kind !== 'p') });
  }

  private onBreak(): void {
    if (this.inPre) {
      this.preText += '\n';
      return;
    }

    if (this.currentLeaf === null) this.startLeaf('p');
    (this.currentLeaf as Leaf).items.push(BREAK);
  }

  // --- structure ---

  private current(): Container {
    return this.containers[this.containers.length - 1] as Container;
  }

  private popContainer(): void {
    const container = this.containers.pop();

    if (container instanceof ListContainer) {
      this.listDepth--;
    } else if (container instanceof TableContainer) {
      for (const before of container.before) this.current().add(before);

      const rectangular = normaliseTable(container.table, this.maxPositions);
      if (rectangular !== null) this.current().add(rectangular);
    }
  }

  private closeLeaf(): void {
    this.currentLeaf = null;
  }

  private startLeaf(kind: LeafKind): void {
    this.closeLeaf();

    // A heading is a top-level block only; in an item or a cell it is a paragraph (US-7).
    const effective: LeafKind =
      kind !== 'p' && !(this.current() instanceof RootContainer) ? 'p' : kind;
    const leaf: Leaf = { type: 'leaf', kind: effective, items: [] };
    this.current().add(leaf);
    this.currentLeaf = leaf;
  }

  private startList(ordered: boolean, frame: Frame): void {
    this.closeLeaf();
    frame.closeLeaf = true;

    const current = this.current();
    if (
      (current instanceof RootContainer ||
        current instanceof ItemContainer ||
        current instanceof ListContainer) &&
      this.listDepth < MAX_LIST_DEPTH
    ) {
      const list: ListBlock = { type: 'list', ordered, items: [] };
      current.add(list);
      this.containers.push(new ListContainer(list));
      this.listDepth++;
      frame.closeLeaf = false;
      frame.pop = true;
    }
  }

  private startItem(frame: Frame): void {
    this.closeLeaf();
    frame.closeLeaf = true;

    const current = this.current();
    if (current instanceof ListContainer) {
      const item: ItemBlock = { blocks: [] };
      current.list.items.push(item);
      current.resetImplicit();
      this.containers.push(new ItemContainer(item));
      frame.closeLeaf = false;
      frame.pop = true;
    }
  }

  private startTable(frame: Frame): void {
    this.closeLeaf();
    frame.closeLeaf = true;

    if (this.current() instanceof RootContainer) {
      this.containers.push(new TableContainer({ type: 'table', rows: [], group: 0 }));
      frame.closeLeaf = false;
      frame.pop = true;
    }
  }

  private endPre(): void {
    this.inPre = false;
    const text = this.preText;
    this.preText = '';

    if (this.current() instanceof RootContainer) {
      this.current().add({ type: 'code', language: this.preLanguage, text });
      return;
    }

    // In a list item or a table cell a code block is one paragraph per line, empty lines kept (US-7).
    for (const line of text.split('\n')) {
      const leaf: Leaf = { type: 'leaf', kind: 'p', items: [] };
      if (line.length > 0) leaf.items.push({ text: line, marks: NO_MARKS });
      this.current().add(leaf);
    }
  }
}

/** `colspan` / `rowspan`: a trimmed run of 1..9 ASCII digits, else 1. */
function spanOf(element: Element, attribute: string): number {
  const value = element.getAttribute(attribute);
  if (value === null) return 1;
  const trimmed = trimUnicodeWhiteSpace(value);
  return /^[0-9]{1,9}$/.test(trimmed) ? Number.parseInt(trimmed, 10) : 1;
}

// --- rectangular tables (TableNormaliser) --------------------------------------------------------------------------

/**
 * US-7 "Tables are rectangular" (BR-RT-17): a spanning cell keeps its content
 * at its top-left position and every other position it covered becomes an
 * empty cell of the same element; a short row is padded at its end to the
 * widest row with empty cells of the row's last cell's element; a row with no
 * cell and a table with no row are removed.
 */
function normaliseTable(table: TableBlock, maxPositions: number): RectangularTable | null {
  const rows = table.rows.filter((row) => row.cells.length > 0);
  if (rows.length === 0) return null;

  // The last row of each group, so a rowspan never crosses a row group.
  const lastOfGroup = new Map<number, number>();
  rows.forEach((row, index) => lastOfGroup.set(row.group, index));

  const occupied: (Map<number, boolean> | undefined)[] = new Array<
    Map<number, boolean> | undefined
  >(rows.length);
  const placed: { column: number; cell: CellBlock }[][] = [];
  let width = 0;
  let positions = 0;

  for (let r = 0; r < rows.length; r++) {
    const row = rows[r] as RowBlock;
    placed[r] = [];
    let column = 0;

    for (const cell of row.cells) {
      while (occupied[r]?.has(column)) column++;

      const columnSpan = Math.min(Math.max(cell.columnSpan, 1), MAX_COLUMN_SPAN);
      const available = (lastOfGroup.get(row.group) as number) - r + 1;
      const rowSpan =
        cell.rowSpan === 0 ? available : Math.min(Math.max(cell.rowSpan, 1), available);

      placed[r]?.push({ column, cell });
      positions++;

      for (let rr = r; rr < r + rowSpan; rr++) {
        for (let cc = column; cc < column + columnSpan; cc++) {
          if (rr === r && cc === column) continue;

          positions++;
          if (positions > maxPositions) throw new BudgetExceeded();

          const covered = occupied[rr] ?? new Map<number, boolean>();
          occupied[rr] = covered;
          covered.set(cc, cell.header);
        }
      }

      column += columnSpan;
      width = Math.max(width, column);
    }

    const covered = occupied[r];
    if (covered !== undefined && covered.size > 0) {
      let widest = 0;
      for (const key of covered.keys()) widest = Math.max(widest, key);
      width = Math.max(width, widest + 1);
    }

    if (positions > maxPositions) throw new BudgetExceeded();
  }

  if (width * rows.length > maxPositions) throw new BudgetExceeded();

  const result: CellBlock[][] = [];

  for (let r = 0; r < rows.length; r++) {
    const row = rows[r] as RowBlock;
    const line: (CellBlock | undefined)[] = new Array<CellBlock | undefined>(width);
    for (const { column, cell } of placed[r] as { column: number; cell: CellBlock }[])
      line[column] = cell;

    const lastHeader = (row.cells[row.cells.length - 1] as CellBlock).header;
    const cells: CellBlock[] = [];

    for (let c = 0; c < width; c++) {
      const own = line[c];
      const coveredHeader = occupied[r]?.get(c);
      if (own !== undefined) {
        cells.push(own);
      } else if (coveredHeader !== undefined) {
        cells.push({ header: coveredHeader, columnSpan: 1, rowSpan: 1, paragraphs: [] });
      } else {
        cells.push({ header: lastHeader, columnSpan: 1, rowSpan: 1, paragraphs: [] });
      }
    }

    result.push(cells);
  }

  return { type: 'rect', rows: result };
}

// --- the writer (CanonicalWriter) ----------------------------------------------------------------------------------

interface OpenMark {
  readonly kind: MarkKind;
  readonly token: VocabularyToken | null;
  readonly href: string | null;
  readonly isCode: boolean;
}

const CODE_MARK: OpenMark = { kind: 'link', token: null, href: null, isCode: true };

function sameMark(a: OpenMark, b: OpenMark): boolean {
  return a.kind === b.kind && a.token === b.token && a.href === b.href && a.isCode === b.isCode;
}

/**
 * Writes the model in the WHATWG fragment serialisation form (api-contract
 * § 3): attributes `href` then `rel` on `a`, `class` alone elsewhere, double
 * quotes, `&` `<` `>` escaped in text and `&` `"` in attributes, U+00A0 as
 * `&nbsp;`, void `br`. It counts code points as it appends and stops the
 * moment the count passes the bound, and accumulates the visible text length
 * (BR-RT-8) and "has content" (BR-RT-6) in the same pass.
 */
class CanonicalWriter {
  private readonly parts: string[] = [];
  private readonly open: OpenMark[] = [];
  private markup: number;
  private leafSeen = false;

  markupLength = 0;
  visibleLength = 0;
  hasContent = false;

  private readonly limit: number;

  constructor(limit: number, alreadyCounted: number) {
    this.limit = limit;
    this.markup = alreadyCounted;
  }

  html(): string {
    return this.parts.join('');
  }

  write(blocks: readonly Block[]): void {
    for (const block of blocks) this.writeBlock(block);
  }

  private writeBlock(block: Block): void {
    switch (block.type) {
      case 'leaf':
        this.writeLeaf(block);
        break;
      case 'code':
        this.writeCode(block);
        break;
      case 'list':
        this.writeList(block);
        break;
      case 'rect':
        this.writeTable(block);
        break;
      default:
        break;
    }
  }

  private separate(): void {
    if (this.leafSeen) this.visibleLength++;
    this.leafSeen = true;
  }

  private writeLeaf(leaf: Leaf): void {
    this.separate();

    this.markupText('<' + leaf.kind + '>');

    for (const item of leaf.items) {
      if (item.text === null) {
        this.markupText('<br>');
        this.visibleLength++;
      } else {
        this.setMarks(item.marks);
        this.text(item.text);
      }
    }

    this.closeAll();
    this.markupText('</' + leaf.kind + '>');
  }

  private writeCode(code: CodeBlock): void {
    this.separate();

    // <pre><code> has the code element between the pre and its text, so the parser's "drop a line feed right after
    // <pre>" does not apply and the text is written as it is.
    this.markupText(
      code.language === null ? '<pre><code>' : '<pre><code class="' + code.language.class + '">',
    );
    this.text(code.text);
    this.markupText('</code></pre>');
  }

  private writeList(list: ListBlock): void {
    if (list.items.length === 0) return;

    const tag = list.ordered ? 'ol' : 'ul';
    this.markupText('<' + tag + '>');

    for (const item of list.items) {
      this.markupText('<li>');

      // An item holds paragraphs and nested lists; one with nothing to write (a nested list with no item) is an
      // empty paragraph, never an empty li (that would not survive a re-parse unchanged).
      const hasWritable = item.blocks.some(
        (block) => !(block.type === 'list' && block.items.length === 0),
      );
      if (!hasWritable) this.writeLeaf({ type: 'leaf', kind: 'p', items: [] });

      for (const block of item.blocks) this.writeBlock(block);

      this.markupText('</li>');
    }

    this.markupText('</' + tag + '>');
  }

  private writeTable(table: RectangularTable): void {
    this.markupText('<table><tbody>');

    for (const row of table.rows) {
      this.markupText('<tr>');

      for (const cell of row) {
        const tag = cell.header ? 'th' : 'td';
        this.markupText('<' + tag + '>');

        if (cell.paragraphs.length === 0) this.writeLeaf({ type: 'leaf', kind: 'p', items: [] });
        for (const paragraph of cell.paragraphs) this.writeLeaf(paragraph);

        this.markupText('</' + tag + '>');
      }

      this.markupText('</tr>');
    }

    this.markupText('</tbody></table>');
  }

  // --- marks ---

  /**
   * Brings the open elements to `marks`: the common prefix of the canonical order stays open (so adjacent equal
   * marks merge), the rest is closed and the new ones opened outermost first (BR-RT-15).
   */
  private setMarks(marks: Marks): void {
    const wanted: OpenMark[] = [];

    for (const kind of MARK_ORDER) {
      switch (kind) {
        case 'link':
          if (marks.href !== null)
            wanted.push({ kind, token: null, href: marks.href, isCode: false });
          break;
        case 'bold':
          if (marks.bold) wanted.push({ kind, token: null, href: null, isCode: false });
          break;
        case 'italic':
          if (marks.italic) wanted.push({ kind, token: null, href: null, isCode: false });
          break;
        case 'underline':
          if (marks.underline) wanted.push({ kind, token: null, href: null, isCode: false });
          break;
        case 'strike':
          if (marks.strike) wanted.push({ kind, token: null, href: null, isCode: false });
          break;
        case 'highlight':
          if (marks.highlight !== null)
            wanted.push({ kind, token: marks.highlight, href: null, isCode: false });
          break;
        case 'textColor':
          if (marks.color !== null)
            wanted.push({ kind, token: marks.color, href: null, isCode: false });
          break;
        case 'fontSize':
          if (marks.size !== null)
            wanted.push({ kind, token: marks.size, href: null, isCode: false });
          break;
      }
    }

    if (marks.code) wanted.push(CODE_MARK);

    let common = 0;
    while (
      common < this.open.length &&
      common < wanted.length &&
      sameMark(this.open[common] as OpenMark, wanted[common] as OpenMark)
    ) {
      common++;
    }

    for (let index = this.open.length - 1; index >= common; index--) {
      this.markupText(closeTag(this.open[index] as OpenMark));
    }
    this.open.length = common;

    for (let index = common; index < wanted.length; index++) {
      this.openMark(wanted[index] as OpenMark);
      this.open.push(wanted[index] as OpenMark);
    }
  }

  private closeAll(): void {
    for (let index = this.open.length - 1; index >= 0; index--) {
      this.markupText(closeTag(this.open[index] as OpenMark));
    }
    this.open.length = 0;
  }

  private openMark(mark: OpenMark): void {
    if (mark.isCode) {
      this.markupText('<code>');
      return;
    }
    switch (mark.kind) {
      case 'link':
        this.markupText('<a href="');
        this.attribute(mark.href as string);
        this.markupText('" rel="' + REL + '">');
        break;
      case 'bold':
        this.markupText('<strong>');
        break;
      case 'italic':
        this.markupText('<em>');
        break;
      case 'underline':
        this.markupText('<u>');
        break;
      case 'strike':
        this.markupText('<s>');
        break;
      case 'highlight':
        this.markupText('<mark class="' + (mark.token as VocabularyToken).class + '">');
        break;
      default:
        this.markupText('<span class="' + (mark.token as VocabularyToken).class + '">');
        break;
    }
  }

  // --- output ---

  /** Fixed ASCII markup. */
  private markupText(text: string): void {
    this.parts.push(text);
    this.count(text.length);
  }

  private attribute(value: string): void {
    let pending = 0;
    let run = 0;

    for (let index = 0; index < value.length;) {
      const unit = value.charCodeAt(index);
      let width = 1;
      if (unit >= 0xd800 && unit <= 0xdbff && index + 1 < value.length) {
        const next = value.charCodeAt(index + 1);
        if (next >= 0xdc00 && next <= 0xdfff) width = 2;
      }

      let escaped: string | null = null;
      if (unit === 0) escaped = '';
      else if (unit === 0x26) escaped = '&amp;';
      else if (unit === 0x22) escaped = '&quot;';
      else if (unit === 0xa0) escaped = '&nbsp;';

      if (escaped !== null) {
        if (run < index) this.parts.push(value.slice(run, index));
        this.parts.push(escaped);
        run = index + width;
        pending += escaped.length;
      } else {
        pending++;
      }
      index += width;
    }

    if (run < value.length) this.parts.push(value.slice(run));
    this.count(pending);
  }

  /** Text: escaped, counted for the markup, the visible length and "has content". */
  private text(text: string): void {
    let pending = 0;
    let visible = 0;
    let run = 0;

    for (let index = 0; index < text.length;) {
      const unit = text.charCodeAt(index);
      let codePoint = unit;
      let width = 1;
      if (unit >= 0xd800 && unit <= 0xdbff && index + 1 < text.length) {
        const next = text.charCodeAt(index + 1);
        if (next >= 0xdc00 && next <= 0xdfff) {
          codePoint = (unit - 0xd800) * 0x400 + (next - 0xdc00) + 0x10000;
          width = 2;
        }
      }

      let escaped: string | null = null;
      if (codePoint === 0) escaped = '';
      else if (codePoint === 0x26) escaped = '&amp;';
      else if (codePoint === 0x3c) escaped = '&lt;';
      else if (codePoint === 0x3e) escaped = '&gt;';
      else if (codePoint === 0xa0) escaped = '&nbsp;';

      if (codePoint !== 0) {
        visible++;
        if (!this.hasContent && !isUnicodeWhiteSpace(codePoint)) this.hasContent = true;
      }

      if (escaped !== null) {
        if (run < index) this.parts.push(text.slice(run, index));
        if (escaped.length > 0) this.parts.push(escaped);
        run = index + width;
        pending += escaped.length;
      } else {
        pending++;
      }
      index += width;
    }

    if (run < text.length) this.parts.push(text.slice(run));
    this.visibleLength += visible;
    this.count(pending);
  }

  private count(codePoints: number): void {
    this.markup += codePoints;
    this.markupLength += codePoints;
    if (this.markup > this.limit) throw new BudgetExceeded();
  }
}

function closeTag(mark: OpenMark): string {
  if (mark.isCode) return '</code>';
  switch (mark.kind) {
    case 'link':
      return '</a>';
    case 'bold':
      return '</strong>';
    case 'italic':
      return '</em>';
    case 'underline':
      return '</u>';
    case 'strike':
      return '</s>';
    case 'highlight':
      return '</mark>';
    default:
      return '</span>';
  }
}

// --- entry ---------------------------------------------------------------------------------------------------------

const EMPTY: NormalisedRichText = {
  html: '',
  visibleLength: 0,
  markupLength: 0,
  hasContent: false,
};

/** Over the ceiling: reported as one code point past it, so `checkDescription` answers `markup-too-large`. */
const OVER_THE_BOUND: NormalisedRichText = {
  html: '',
  visibleLength: 0,
  markupLength: DESCRIPTION_MAX_MARKUP + 1,
  hasContent: true,
};

/**
 * Reduces `raw` html to the canonical form the server would store (BR-RT-5),
 * with the counts of BR-RT-7 / BR-RT-8. Strings above the server's raw ceiling
 * skip the walk, so a paste cannot freeze the tab; that is a client
 * convenience, never an answer the server relies on.
 */
export function normaliseRichText(raw: string): NormalisedRichText {
  if (raw.length > DESCRIPTION_RAW_CEILING) return OVER_THE_BOUND;

  const text = cleanText(raw);
  const document = new DOMParser().parseFromString(WRAPPER + text, 'text/html');
  const body = document.body as HTMLElement | null;

  // A frameset replaces the body; nothing of it is kept.
  if (body === null || body.localName !== 'body') return EMPTY;

  return normaliseBody(body);
}

/**
 * The reduction of an already parsed `<body>`: what `normaliseRichText` does
 * after the parse. The paste pipeline hands it a document it has adjusted
 * (styles read into marks, code recognised) so the result is never serialised
 * to a string and parsed again.
 */
export function normaliseBody(body: Element): NormalisedRichText {
  try {
    const limit = DESCRIPTION_MAX_MARKUP;
    const converter = new DomConverter(Math.floor(limit / MIN_CELL_MARKUP), limit);
    const blocks = converter.convert(body);

    const writer = new CanonicalWriter(limit, converter.droppedWhitespace);
    writer.write(blocks);

    return {
      html: writer.hasContent ? writer.html() : '',
      visibleLength: writer.visibleLength,
      markupLength: writer.markupLength,
      hasContent: writer.hasContent,
    };
  } catch (error) {
    if (error instanceof BudgetExceeded) return OVER_THE_BOUND;
    throw error;
  }
}
