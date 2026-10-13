import { theme } from '../app/theme';

// Helpers of the kit's own tests (tech-015). `src/test/` is outside the colour scan,
// so a computed colour can be built here, in the form jsdom reports it: `rgb(r, g, b)`.

/** `#242424` → `rgb(36, 36, 36)`, the form jsdom's computed style reports. */
export function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

/** The panel's theme, for a test that reads a token. */
export { theme };

/** `0.875rem` or `14px` → 14, through the theme's own root font size. */
export function px(value: string): number {
  const match = /^(-?[\d.]+)(rem|px)$/.exec(value.trim());
  if (!match?.[1]) throw new Error(`Not a px or rem size: ${value}`);
  return match[2] === 'rem' ? Number(match[1]) * theme.typography.htmlFontSize : Number(match[1]);
}

/**
 * The size, in px, the field family's stylesheet declares for a value's text. jsdom
 * cascades by the order the rules were inserted, not by specificity, so the input's own
 * computed size is MUI's `inherit`, not the kit's: the declared rule is the answer.
 */
export function valueFontSize(fieldRoot: Element): number {
  const rule = rulesFor(fieldRoot, '.MuiInputBase-input').find(
    (candidate) => candidate.declarations['font-size'],
  );
  return px(rule?.declarations['font-size'] ?? '');
}

/**
 * Whether a rule's selector would reach `target` once the state it names holds. jsdom
 * answers no `:hover`, and the element is not open or focused, so the state — a
 * pseudo-class, or the `data-open` / `data-expanded` the kit sets — is taken out of the
 * selector and the rest is asked of the document. A test that only reads that a rule's
 * selector *contains* `:hover` would pass a rule that can never match — as one did, when a
 * state rule named the root's own class a second time as if it were a descendant.
 */
export function reaches(selector: string, target: Element): boolean {
  const stateless = selector
    .replace(/:(?:hover|focus-within|focus-visible|focus)\b/g, '')
    .replace(/\[data-(?:open|expanded)="true"\]/g, '');
  try {
    return Array.from(document.querySelectorAll(stateless)).includes(target);
  } catch {
    return false;
  }
}

/** One CSS rule the document holds that styles an element. */
export interface FoundRule {
  /** The selector, as the stylesheet wrote it. */
  selector: string;
  /** The enclosing at-rule's condition, e.g. `(hover: none)`; empty at the top level. */
  media: string;
  /** The declarations: property → value. */
  declarations: Record<string, string>;
}

/**
 * The rules, in the document's stylesheets, whose selector names the element's own
 * emotion class and contains `fragment` — what jsdom cannot compute (a pseudo-element's
 * look, a `:hover` or `@media (hover: none)` state). A test asserts the rule is there;
 * the showcase E2E reads the computed value in a browser.
 */
export function rulesFor(element: Element, fragment: string): FoundRule[] {
  const classes = Array.from(element.classList).filter((name) => name.startsWith('css-'));
  const found: FoundRule[] = [];
  const visit = (rules: CSSRuleList, media: string) => {
    for (const rule of Array.from(rules)) {
      if (rule instanceof CSSStyleRule) {
        const selectors = rule.selectorText.split(',').map((part) => part.trim());
        // Every part of a selector list that targets the element is its own entry: a list
        // that names a state before one indicator and not before the other is two rules.
        const hits = selectors.filter(
          (selector) =>
            selector.includes(fragment) && classes.some((name) => selector.includes(`.${name}`)),
        );
        if (hits.length > 0) {
          const declarations: Record<string, string> = {};
          for (let index = 0; index < rule.style.length; index += 1) {
            const property = rule.style[index] as string;
            declarations[property] = rule.style.getPropertyValue(property);
          }
          for (const hit of hits) found.push({ selector: hit, media, declarations });
        }
      } else if (rule instanceof CSSMediaRule) {
        visit(rule.cssRules, rule.conditionText || rule.media.mediaText);
      } else if ('cssRules' in rule) {
        // An at-rule that groups rules and is not a media rule: `@container name (…)`.
        const group = rule as CSSGroupingRule & { conditionText?: string };
        visit(group.cssRules, group.conditionText ?? '');
      }
    }
  };
  for (const sheet of Array.from(document.styleSheets)) visit(sheet.cssRules, '');
  return found;
}
