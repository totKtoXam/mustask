import { useId, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';

export interface SectionHeadProps {
  title: string;
  /** The heading's level, 2 or 3: a section inside a page takes the level below the page's. Default 2. */
  headingLevel?: 2 | 3;
  /** «Дочерние элементы (3)»: drawn after the title, inside the heading, so the name carries it. */
  count?: number;
  /** Right of the title: a button, an icon action, a menu. */
  actions?: ReactNode;
  /** The 1 px rule under the head, `shell.sectionRule`. Default true. */
  divider?: boolean;
  /** The heading's id: what a `Section`'s `aria-labelledby` points at. */
  id?: string;
}

/**
 * The head of a titled group (tech-015 design § 5.8): the heading — `typography.h2`
 * (`h3` at level 3) with its count — its actions at the right, and under them
 * `layout.section.headGap` and a rule in `shell.sectionRule`, the reference's, not the
 * `divider`. Alone it is for a heading that is not a region (a board column's head).
 */
export function SectionHead({
  title,
  headingLevel = 2,
  count,
  actions,
  divider = true,
  id,
}: SectionHeadProps) {
  return (
    <Box
      sx={(theme) => ({
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 2,
        paddingBottom: `${theme.layout.section.headGap}px`,
        ...(divider
          ? {
              borderBottom: `${theme.layout.dividerWidth}px solid ${theme.palette.shell.sectionRule}`,
            }
          : {}),
      })}
    >
      <Typography
        id={id}
        variant={headingLevel === 2 ? 'h2' : 'h3'}
        component={`h${headingLevel}`}
        sx={{ minWidth: 0, overflowWrap: 'anywhere' }}
      >
        {count === undefined ? title : `${title} (${count})`}
      </Typography>
      {actions && (
        <Box sx={{ display: 'flex', alignItems: 'center', flexShrink: 0 }}>{actions}</Box>
      )}
    </Box>
  );
}

export interface SectionProps extends Omit<SectionHeadProps, 'id'> {
  children: ReactNode;
}

/**
 * A titled group in a card's body: «Описание», «Планирование», «Связанная работа». A
 * `<section>` — a `region` named by its own heading — with its head and its content
 * inset by `layout.section.inset`, `bodyTop` under the rule.
 *
 * **It has no margin at all.** The 32 px between consecutive sections is the
 * `ColumnLayout` area's (a flex column with `layout.section.gap`), so nothing doubles
 * and no trailing 32 sits on the panel's bottom padding. A section stacked by anything
 * else is not a supported arrangement.
 */
export function Section({ children, ...head }: SectionProps) {
  const headingId = useId();
  return (
    <Box component="section" aria-labelledby={headingId} sx={{ margin: 0 }}>
      <SectionHead id={headingId} {...head} />
      <Box
        sx={(theme) => ({
          paddingTop: `${theme.layout.section.bodyTop}px`,
          paddingLeft: `${theme.layout.section.inset}px`,
          paddingRight: `${theme.layout.section.inset}px`,
        })}
      >
        {children}
      </Box>
    </Box>
  );
}
