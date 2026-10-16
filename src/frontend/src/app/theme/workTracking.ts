import type { SvgIconComponent } from '@mui/icons-material';
import AssignmentTurnedInIcon from '@mui/icons-material/AssignmentTurnedIn';
import BugReportIcon from '@mui/icons-material/BugReport';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import HelpOutlineOutlinedIcon from '@mui/icons-material/HelpOutlineOutlined';
import ScienceIcon from '@mui/icons-material/Science';
import { CrownIcon } from './CrownIcon';
import { StoryIcon } from './StoryIcon';
import {
  APPEARANCE_TOKENS,
  CATEGORY_CODES,
  categoryColours,
  typeColours,
  type AppearanceToken,
  type CategoryCode,
} from './tokens';

/** A work item type's look, resolved from its stored appearance token. */
export interface TypeAppearance {
  icon: SvgIconComponent;
  colour: string;
}

/**
 * The two work-tracking lookups (design § 2.3, BR-4). Slices read them as
 * `useTheme().workTracking`, never by importing `app/`.
 */
export interface WorkTrackingLookups {
  /**
   * Takes the token name a process seed stores for a type — never a type key,
   * a type name or a label. A name outside the set answers the neutral
   * fallback and never throws.
   */
  typeAppearance(token: string): TypeAppearance;
  /**
   * Takes doc-007's state-category code — never a state name or a label. An
   * unknown code answers the secondary text colour.
   */
  categoryColour(code: string): string;
}

// feat-027 design § 2.4: the crown, the two-page book and the clipboard with a
// check are the reference screen's epic, story and task; one lookup, so every
// place that draws a type changes with it.
const TYPE_ICONS: Record<AppearanceToken, SvgIconComponent> = {
  epic: CrownIcon,
  feature: EmojiEventsIcon,
  story: StoryIcon,
  task: AssignmentTurnedInIcon,
  bug: BugReportIcon,
  // feat-032 design § 2: the flask, as the reference's test case.
  testcase: ScienceIcon,
};

function isAppearanceToken(value: string): value is AppearanceToken {
  return (APPEARANCE_TOKENS as readonly string[]).includes(value);
}

function isCategoryCode(value: string): value is CategoryCode {
  return (CATEGORY_CODES as readonly string[]).includes(value);
}

/**
 * Builds the lookups over the theme's own text colours, which supply the
 * fallbacks. Attached to the theme after `createTheme`, so the icon
 * components never pass through MUI's deep merge of the options.
 */
export function createWorkTrackingLookups(text: {
  primary: string;
  secondary: string;
}): WorkTrackingLookups {
  const fallback: TypeAppearance = { icon: HelpOutlineOutlinedIcon, colour: text.primary };
  return {
    typeAppearance: (token) =>
      isAppearanceToken(token) ? { icon: TYPE_ICONS[token], colour: typeColours[token] } : fallback,
    categoryColour: (code) => (isCategoryCode(code) ? categoryColours[code] : text.secondary),
  };
}
