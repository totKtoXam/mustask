import type { MouseEvent, ReactNode } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import { alpha, type Theme } from '@mui/material/styles';
import { IconAction, Tag, ThinChevronDownIcon, ThinChevronRightIcon } from '../../../shared/ui';
import type { ObjectLink } from '../hooks/useObjectLink';
import type { DisplayMode } from '../hooks/useStructureSettings';
import { areasPage, structureDiagram as texts } from '../lib/areaTexts';
import type { CardItem, SectionItem } from '../lib/diagramLayout';
import { levelColour } from '../lib/diagramLevels';
import { alsoInText, isChoosable, type DiagramModel } from '../lib/diagramModel';

/**
 * What a card or a section header of the diagram draws (feat-049 design §§ 4.4, 4.5, 4.7). The
 * parts are the page's own: a card is a `role="treeitem"` positioned by the layout, named as a
 * whole (`aria-label`), so its text, chevron and link are `tabIndex -1` and hidden from the Tab
 * order — the tree is one Tab stop (BR-SD-10).
 */

export type CardEmphasis = 'none' | 'chosen' | 'related' | 'dim';

interface ItemProps {
  tabIndex: 0 | -1;
  register: (id: string, element: HTMLElement | null) => void;
}

function chevron(open: boolean, onToggle: () => void) {
  return (
    <IconAction
      size="chevron"
      label={open ? texts.toggle.close : texts.toggle.open}
      icon={open ? <ThinChevronDownIcon /> : <ThinChevronRightIcon />}
      tooltip={false}
      tabIndex={-1}
      aria-hidden
      onClick={(event: MouseEvent<HTMLElement>) => {
        event.stopPropagation();
        onToggle();
      }}
    />
  );
}

export function DiagramSectionHeader({
  item,
  tabIndex,
  register,
  name,
  onToggle,
}: ItemProps & { item: SectionItem; name: string; onToggle: () => void }) {
  return (
    <Box
      role="treeitem"
      ref={(element: HTMLElement | null) => register(item.id, element)}
      tabIndex={tabIndex}
      aria-level={item.ariaLevel}
      aria-expanded={item.expanded}
      aria-label={name}
      data-item={item.id}
      onClick={onToggle}
      sx={(theme) => ({
        position: 'absolute',
        left: item.x,
        top: item.y,
        width: item.w > 0 ? item.w : undefined,
        height: item.h,
        boxSizing: 'border-box',
        display: 'flex',
        alignItems: 'center',
        gap: 0.75,
        whiteSpace: 'nowrap',
        cursor: 'pointer',
        color: theme.palette.shell.cardLabel,
        fontWeight: theme.typography.fontWeightMedium,
        borderBottom: `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`,
        '&:focus-visible': {
          outline: `2px solid ${theme.palette.primary.main}`,
          outlineOffset: 2,
        },
      })}
    >
      {chevron(item.expanded, onToggle)}
      <span>{item.title}</span>
      <span>{item.count}</span>
    </Box>
  );
}

function cardSx(
  theme: Theme,
  item: CardItem,
  look: { emphasis: CardEmphasis; mode: DisplayMode; hit: boolean; choosable: boolean },
) {
  const colour = levelColour(theme, item.level);
  const dead = !item.node.active;
  const strong = look.emphasis === 'chosen' || (look.emphasis === 'related' && look.mode === 'hl');
  const tint = dead
    ? theme.palette.shell.buttonNeutral
    : alpha(colour, strong ? 0.18 : look.emphasis === 'related' ? 0.14 : 0.08);
  // Rings are outlines, not shadows (the kit owns shadows): chosen and related ones stand off the
  // card by a paper gap so they read against any tint.
  let ring: string | undefined;
  let ringOffset = 1;
  if (look.emphasis === 'chosen') ring = `3px solid ${theme.palette.primary.main}`;
  else if (look.emphasis === 'related' && look.mode === 'hl') {
    ring = `3px solid ${theme.palette.primary.dark}`;
    ringOffset = 2;
  } else if (look.emphasis === 'related') ring = `2px solid ${theme.palette.primary.main}`;
  else if (look.hit) ring = `2px solid ${theme.palette.warning.main}`;
  return {
    position: 'absolute' as const,
    left: item.x,
    top: item.y,
    width: item.w,
    height: item.h,
    boxSizing: 'border-box' as const,
    display: 'flex',
    overflow: 'hidden',
    whiteSpace: 'nowrap' as const,
    cursor: look.choosable ? 'pointer' : 'default',
    color: theme.palette.text.primary,
    border: `1.5px solid ${colour}`,
    borderLeft: `5px solid ${colour}`,
    borderRadius: `${theme.shape.borderRadius}px`,
    backgroundColor: tint,
    outline: ring,
    outlineOffset: ring ? ringOffset : undefined,
    opacity: look.emphasis === 'dim' ? 0.5 : 1,
    transition: 'opacity 120ms',
    '&:hover':
      look.choosable && !dead && look.emphasis === 'none'
        ? { backgroundColor: alpha(colour, 0.14) }
        : undefined,
    '&:focus-visible': { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: 3 },
  };
}

const quiet = (theme: Theme) => ({
  fontSize: theme.typography.caption.fontSize,
  color: theme.palette.text.secondary,
  flexShrink: 0,
});

export function DiagramCard({
  item,
  model,
  vertical,
  emphasis,
  mode,
  hit,
  chosen,
  link,
  tabIndex,
  register,
  name,
  onToggle,
  onChoose,
}: ItemProps & {
  item: CardItem;
  model: DiagramModel;
  vertical: boolean;
  emphasis: CardEmphasis;
  mode: DisplayMode;
  hit: boolean;
  chosen: boolean;
  link: ObjectLink | null;
  name: string;
  onToggle: () => void;
  onChoose: () => void;
}) {
  const node = item.node;
  const choosable = isChoosable(model, node.baseId);
  const also = alsoInText(model, node);
  const projects = node.kind === 'team' ? (model.projectsOfTeam.get(node.baseId) ?? []).length : 0;
  let count: string | null = null;
  if (node.kind === 'team') count = texts.counts.projects(projects);
  else if (model.hasOffers && node.active && node.kind === 'project') {
    count = texts.counts.teams((model.teamsOfProject.get(node.id) ?? []).length);
  } else if (model.hasOffers && node.kind === 'department') {
    count = texts.counts.teams((model.teamsOfDepartment.get(node.id) ?? []).length);
  }

  const nameNode: ReactNode = link ? (
    <Link
      component={RouterLink}
      to={link.to}
      state={link.state}
      tabIndex={-1}
      title={node.name}
      onClick={(event: MouseEvent<HTMLElement>) => event.stopPropagation()}
      sx={(theme) => ({
        fontWeight: theme.typography.fontWeightMedium,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        minWidth: 0,
      })}
    >
      {node.name}
    </Link>
  ) : (
    <Box
      component="span"
      title={node.name}
      sx={(theme) => ({
        fontWeight: theme.typography.fontWeightMedium,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        minWidth: 0,
      })}
    >
      {node.name}
    </Box>
  );
  const badge = (
    <Box
      component="span"
      aria-hidden="true"
      sx={(theme) => ({
        flexShrink: 0,
        width: 16,
        height: 16,
        borderRadius: '50%',
        textAlign: 'center',
        fontSize: 10,
        lineHeight: '16px',
        backgroundColor: levelColour(theme, item.level),
        color: theme.palette.primary.contrastText,
      })}
    >
      {item.level}
    </Box>
  );
  const type = (
    <Box component="span" sx={quiet}>
      {texts.type[node.kind]}
    </Box>
  );
  const keyNode = node.key ? (
    <Box component="span" sx={quiet}>
      {node.key}
    </Box>
  ) : null;
  const tag = node.active ? null : <Tag label={areasPage.deactivated} />;
  const alsoNode = also ? (
    <Box
      component="span"
      sx={(theme) => ({
        ...quiet(theme),
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        maxWidth: vertical ? '100%' : 200,
      })}
    >
      {also}
    </Box>
  ) : null;
  const countNode = count ? (
    <Box component="span" sx={(theme) => ({ ...quiet(theme), ml: vertical ? 0 : 'auto' })}>
      {count}
    </Box>
  ) : null;
  const toggle = item.hasChildren ? (
    chevron(item.expanded, onToggle)
  ) : vertical ? null : (
    <Box component="span" sx={{ width: 24, flexShrink: 0 }} />
  );

  return (
    <Box
      role="treeitem"
      ref={(element: HTMLElement | null) => register(item.id, element)}
      tabIndex={tabIndex}
      aria-level={item.ariaLevel}
      aria-expanded={item.hasChildren ? item.expanded : undefined}
      aria-selected={choosable ? chosen : undefined}
      aria-label={name}
      data-item={item.id}
      data-level={item.level}
      onClick={choosable ? onChoose : undefined}
      sx={(theme) => ({
        ...cardSx(theme, item, { emphasis, mode, hit, choosable }),
        alignItems: vertical ? 'flex-start' : 'center',
        flexDirection: vertical ? 'column' : 'row',
        justifyContent: vertical ? 'center' : undefined,
        gap: vertical ? 0.125 : 0.75,
        padding: vertical ? theme.spacing(0.25, 1, 0.25, 0.75) : theme.spacing(0, 1, 0, 0.5),
        lineHeight: vertical ? '16px' : undefined,
      })}
    >
      {vertical ? (
        <>
          <Box component="span" sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
            {toggle}
            {badge}
            {type}
          </Box>
          {nameNode}
          <Box
            component="span"
            sx={{ display: 'flex', alignItems: 'center', gap: 0.75, maxWidth: '100%' }}
          >
            {keyNode}
            {tag}
            {alsoNode}
            {countNode}
          </Box>
        </>
      ) : (
        <>
          {toggle}
          {badge}
          {type}
          {nameNode}
          {keyNode}
          {tag}
          {alsoNode}
          {countNode}
        </>
      )}
    </Box>
  );
}
