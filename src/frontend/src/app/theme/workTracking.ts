import type { SvgIconComponent } from '@mui/icons-material';
import AssignmentIcon from '@mui/icons-material/Assignment';
import BugReportIcon from '@mui/icons-material/BugReport';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import HelpOutlineOutlinedIcon from '@mui/icons-material/HelpOutlineOutlined';
import LandscapeIcon from '@mui/icons-material/Landscape';
import MenuBookIcon from '@mui/icons-material/MenuBook';
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

const TYPE_ICONS: Record<AppearanceToken, SvgIconComponent> = {
  epic: LandscapeIcon,
  feature: EmojiEventsIcon,
  story: MenuBookIcon,
  task: AssignmentIcon,
  bug: BugReportIcon,
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
