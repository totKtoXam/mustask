import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
  type MouseEvent,
} from 'react';
import { useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Tooltip from '@mui/material/Tooltip';
import { alpha } from '@mui/material/styles';
import { InlineNotice, LiveRegion, useNotice } from '../../../shared/ui';
import type { SelectorOffers } from '../api/navigationApi';
import type { AreaNode } from '../api/organizationsApi';
import { useAreaRowLink } from '../hooks/useAreaRowLink';
import { useObjectLink } from '../hooks/useObjectLink';
import type { DiagramViewPatch, DiagramViewState } from '../hooks/useDiagramView';
import type { DisplayMode, LayoutOrientation } from '../hooks/useStructureSettings';
import { structureDiagram as texts } from '../lib/areaTexts';
import {
  layoutDiagram,
  RELATION_LINE_LIMIT,
  VERTICAL_NOTE_LIMIT,
  type CardItem,
  type ChipPlace,
  type DiagramLayout,
} from '../lib/diagramLayout';
import { levelColour, levelIncluded, type LevelMode } from '../lib/diagramLevels';
import {
  ancestorsOf,
  buildModel,
  emphasisOf,
  expandableNodeIds,
  initialDiagramExpansion,
  isChoosable,
  nodeIdsOfBase,
  relatedFilter,
  searchMatches,
  summaryText,
  cardName,
  type DiagramModel,
  type SectionKey,
} from '../lib/diagramModel';
import { clampZoom, scrollAfterZoom, stepZoom, wheelZoom } from '../lib/diagramZoom';
import { DiagramLines } from './DiagramLines';
import { DiagramCard, DiagramSectionHeader, type CardEmphasis } from './DiagramNode';
import { DiagramToolbar } from './DiagramToolbar';

export type OffersStatus = 'ready' | 'loading' | 'failed';

/**
 * «Структура», the diagram view (feat-049 design §§ 3-6): the area tree and the selector offers
 * drawn as SVG lines behind absolutely positioned cards. Read-only (BR-SD-5). The layout is a
 * pure function of the model, the expansion and the options (`layoutDiagram`); choosing a node
 * changes emphasis only, except in «Только связанные», which rebuilds the layout by nature.
 */
export function StructureDiagram({
  organizationId,
  areas,
  offers,
  offersStatus,
  onRetryOffers,
  view,
  patch,
  mode,
  onMode,
  orientation,
  onOrientation,
}: {
  organizationId: string;
  areas: readonly AreaNode[];
  offers: SelectorOffers | null;
  offersStatus: OffersStatus;
  onRetryOffers: () => Promise<unknown>;
  view: DiagramViewState;
  patch: DiagramViewPatch;
  mode: DisplayMode;
  onMode: (mode: DisplayMode) => void;
  orientation: LayoutOrientation;
  onOrientation: (orientation: LayoutOrientation) => void;
}) {
  const navigate = useNavigate();
  const notice = useNotice();
  const rowLink = useAreaRowLink(organizationId);
  const objectLink = useObjectLink(organizationId);
  const model = useMemo(() => buildModel(areas, offers), [areas, offers]);
  const initial = useMemo(() => initialDiagramExpansion(model), [model]);
  const expansion = view.expansion ?? initial;
  const expandable = useMemo(() => expandableNodeIds(model), [model]);

  const [chosenBase, setChosenBase] = useState<string | null>(null);
  const [focusId, setFocusId] = useState<string | null>(null);
  const [matchIndex, setMatchIndex] = useState(-1);
  const pendingFocus = useRef<string | null>(null);
  const [panning, setPanning] = useState(false);
  const [live, setLive] = useState({ text: '', serial: 0 });
  const announce = (text: string) => setLive((current) => ({ text, serial: current.serial + 1 }));

  const frameRef = useRef<HTMLDivElement>(null);
  const items = useRef(new Map<string, HTMLElement>());
  const pendingScroll = useRef<{ left: number; top: number } | null>(null);
  const retryRef = useRef<HTMLButtonElement>(null);
  const register = (id: string, element: HTMLElement | null) => {
    if (element) items.current.set(id, element);
    else items.current.delete(id);
  };

  const chosen = chosenBase && isChoosable(model, chosenBase) ? chosenBase : null;
  const emphasis = useMemo(() => (chosen ? emphasisOf(model, chosen) : null), [model, chosen]);
  const related = useMemo(
    () => (mode === 'only' && emphasis ? relatedFilter(model, emphasis) : null),
    [model, mode, emphasis],
  );
  const layout = useMemo(
    () =>
      layoutDiagram(model, expansion, {
        orientation,
        levels: view.levels,
        sections: view.sections,
        related,
      }),
    [model, expansion, orientation, view.levels, view.sections, related],
  );
  const matches = useMemo(() => searchMatches(model, view.search), [model, view.search]);
  const matchSet = useMemo(() => new Set(matches), [matches]);

  const effectiveFocus =
    focusId && layout.positions.has(focusId) ? focusId : (layout.items[0]?.id ?? null);

  function focusItem(id: string) {
    const element = items.current.get(id);
    if (element) {
      element.focus({ preventScroll: true });
      element.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
    }
  }

  // A jump (search) or a choice waits for the rebuilt layout, then moves focus to the card.
  useEffect(() => {
    const wanted = pendingFocus.current;
    if (wanted === null) return;
    pendingFocus.current = null;
    if (layout.positions.has(wanted)) {
      focusItem(wanted);
    } else {
      notice.show(related ? texts.toasts.hiddenByMode : texts.toasts.hiddenByLevels);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- focus once per pending request
  }, [layout, view.expansion, view.sections]);

  function openNodes(ids: readonly string[]): { expansion: Set<string>; changed: boolean } {
    const next = new Set(expansion);
    let changed = false;
    for (const id of ids) {
      for (const ancestor of ancestorsOf(model, id)) {
        if (!next.has(ancestor)) {
          next.add(ancestor);
          changed = true;
        }
      }
    }
    return { expansion: next, changed };
  }

  function unfoldSections(ids: readonly string[]): Record<SectionKey, boolean> | null {
    const sections = { ...view.sections };
    let changed = false;
    for (const id of ids) {
      const node = model.nodes.get(id);
      if (node && !sections[node.section]) {
        sections[node.section] = true;
        changed = true;
      }
    }
    return changed ? sections : null;
  }

  function choose(base: string | null) {
    const next = base !== null && base === chosen ? null : base;
    setChosenBase(next);
    if (next === null) {
      announce(texts.cleared);
      return;
    }
    const nodeIds = [...emphasisOf(model, next).ids].flatMap((id) => nodeIdsOfBase(model, id));
    const change: Partial<DiagramViewState> = {};
    // Expansion is forced open for the choice only in «Затемнение» and «Подсветка» (BR-SD-11).
    if (mode !== 'only') {
      const opened = openNodes(nodeIds);
      if (opened.changed) change.expansion = opened.expansion;
    }
    const sections = unfoldSections(nodeIds);
    if (sections) change.sections = sections;
    if (Object.keys(change).length > 0) patch(change);
    announce(summaryText(model, next));
  }

  function toggleNode(id: string) {
    const next = new Set(expansion);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    patch({ expansion: next });
  }

  function toggleSection(key: SectionKey, open?: boolean) {
    const value = open ?? !view.sections[key];
    if (value === view.sections[key]) return;
    patch({ sections: { ...view.sections, [key]: value } });
    announce(texts.sectionAnnounced(texts.sections[key], value));
  }

  function jump() {
    if (matches.length === 0) return;
    const index = (matchIndex + 1) % matches.length;
    setMatchIndex(index);
    const base = matches[index]!;
    const id = nodeIdsOfBase(model, base)[0]!;
    const change: Partial<DiagramViewState> = {};
    const opened = openNodes([id]);
    if (opened.changed) change.expansion = opened.expansion;
    const sections = unfoldSections([id]);
    if (sections) change.sections = sections;
    if (Object.keys(change).length > 0) {
      pendingFocus.current = id;
      patch(change);
    } else if (layout.positions.has(id)) {
      focusItem(id);
    } else {
      notice.show(related ? texts.toasts.hiddenByMode : texts.toasts.hiddenByLevels);
    }
  }

  function toggleLevel(level: number) {
    const selected = new Set(view.levels.selected);
    if (selected.has(level)) selected.delete(level);
    else selected.add(level);
    patch({ levels: { ...view.levels, selected } });
    announce(
      selected.size > 0
        ? texts.levels.shown(
            view.levels.mode === 'from'
              ? [Math.min(...selected)]
              : [...selected].sort((a, b) => a - b),
            view.levels.mode === 'from',
          )
        : texts.levels.shownAll,
    );
  }

  function setLevelMode(levelMode: LevelMode) {
    patch({ levels: { ...view.levels, mode: levelMode } });
  }

  function resetLevels() {
    patch({ levels: { ...view.levels, selected: new Set() } });
    announce(texts.levels.shownAll);
    // Focus goes to the first chip, as the button is disabled once nothing is pressed.
    requestAnimationFrame(() => {
      frameRef.current?.querySelector<HTMLElement>('[data-chip="1-0"]')?.focus();
    });
  }

  // Zoom is a transform of the drawn picture: no layout rebuild (BR-SD-20).
  function zoomTo(next: number, pointer?: { x: number; y: number }, say = false) {
    const zoom = clampZoom(next);
    if (zoom === view.zoom) return;
    const frame = frameRef.current;
    if (frame) {
      const rect = frame.getBoundingClientRect();
      const px = pointer ? pointer.x - rect.left : frame.clientWidth / 2;
      const py = pointer ? pointer.y - rect.top : frame.clientHeight / 2;
      pendingScroll.current = {
        left: scrollAfterZoom(frame.scrollLeft, px, view.zoom, zoom),
        top: scrollAfterZoom(frame.scrollTop, py, view.zoom, zoom),
      };
    }
    patch({ zoom });
    if (say) announce(texts.zoom.announced(Math.round(zoom * 100)));
  }

  useLayoutEffect(() => {
    const frame = frameRef.current;
    const target = pendingScroll.current;
    if (frame && target) {
      frame.scrollLeft = target.left;
      frame.scrollTop = target.top;
    }
    pendingScroll.current = null;
  }, [view.zoom]);

  useEffect(() => {
    const frame = frameRef.current;
    if (!frame) return undefined;
    // Non-passive, so Ctrl + wheel zooms the picture and not the browser page.
    const onWheel = (event: WheelEvent) => {
      if (!event.ctrlKey) return;
      event.preventDefault();
      const delta = event.deltaMode === 1 ? event.deltaY * 33 : event.deltaY;
      zoomTo(wheelZoom(view.zoom, delta), { x: event.clientX, y: event.clientY });
    };
    frame.addEventListener('wheel', onWheel, { passive: false });
    return () => frame.removeEventListener('wheel', onWheel);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- re-bound per zoom value
  }, [view.zoom, layout.width]);

  function startPan(event: MouseEvent<HTMLDivElement>) {
    if (event.button !== 1) return;
    const frame = frameRef.current;
    if (!frame) return;
    event.preventDefault();
    const start = {
      x: event.clientX,
      y: event.clientY,
      left: frame.scrollLeft,
      top: frame.scrollTop,
    };
    setPanning(true);
    const move = (moved: globalThis.MouseEvent) => {
      if ((moved.buttons & 4) === 0) {
        end();
        return;
      }
      frame.scrollLeft = start.left - (moved.clientX - start.x);
      frame.scrollTop = start.top - (moved.clientY - start.y);
    };
    const end = () => {
      setPanning(false);
      window.removeEventListener('mousemove', move);
      window.removeEventListener('mouseup', end);
      window.removeEventListener('blur', end);
    };
    window.addEventListener('mousemove', move);
    window.addEventListener('mouseup', end);
    window.addEventListener('blur', end);
  }

  function onTreeKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.ctrlKey || event.metaKey || event.altKey) return;
    const element = (event.target as HTMLElement).closest<HTMLElement>('[data-item]');
    const id = element?.dataset.item;
    if (!id) return;
    const index = layout.items.findIndex((item) => item.id === id);
    const item = layout.items[index];
    if (!item) return;
    const go = (to: number) => {
      const target = layout.items[Math.max(0, Math.min(layout.items.length - 1, to))];
      if (target) focusItem(target.id);
    };
    const handled = () => event.preventDefault();
    switch (event.key) {
      case 'ArrowDown':
        handled();
        go(index + 1);
        return;
      case 'ArrowUp':
        handled();
        go(index - 1);
        return;
      case 'Home':
        handled();
        go(0);
        return;
      case 'End':
        handled();
        go(layout.items.length - 1);
        return;
      case 'Escape':
        if (chosen) {
          handled();
          choose(null);
        }
        return;
      default:
    }
    if (item.type === 'section') {
      if (event.key === 'ArrowRight') {
        handled();
        if (!item.expanded) toggleSection(item.section, true);
        else go(index + 1);
      } else if (event.key === 'ArrowLeft') {
        handled();
        if (item.expanded) toggleSection(item.section, false);
      } else if (event.key === ' ' || event.key === 'Enter') {
        handled();
        toggleSection(item.section);
      }
      return;
    }
    if (event.key === 'ArrowRight') {
      handled();
      if (item.hasChildren && !item.expanded) toggleNode(item.id);
      else if (item.hasChildren) go(index + 1);
    } else if (event.key === 'ArrowLeft') {
      handled();
      if (item.hasChildren && item.expanded) toggleNode(item.id);
      else if (item.parentId && layout.positions.has(item.parentId)) focusItem(item.parentId);
      else focusItem(`section:${item.section}`);
    } else if (event.key === ' ') {
      handled();
      if (isChoosable(model, item.node.baseId)) choose(item.node.baseId);
      else announce(texts.notChoosable);
    } else if (event.key === 'Enter') {
      handled();
      const link = linkOf(item);
      if (link) navigate(link.to, { state: link.state });
      else announce(texts.notOpening);
    }
  }

  function linkOf(item: CardItem) {
    const node = item.node;
    if (node.kind === 'team') return objectLink('team', node.baseId);
    return rowLink({
      id: node.id,
      type: node.kind,
      projectId: node.projectId,
      projectActive: node.active,
    });
  }

  const cards = layout.items.filter((item): item is CardItem => item.type === 'card');
  const allExpanded = [...expandable].every((id) => expansion.has(id));
  const verticalNote =
    orientation === 'v' && layout.visibleCount > VERTICAL_NOTE_LIMIT
      ? texts.notes.vertical(layout.visibleCount)
      : null;
  const drawAll = model.lineTotal <= RELATION_LINE_LIMIT;
  const emptyText =
    cards.length === 0
      ? view.levels.selected.size > 0
        ? texts.levels.none
        : texts.levels.noRelated
      : null;

  async function retryOffers() {
    await onRetryOffers();
    retryRef.current?.focus();
  }

  function emphasisOfCard(card: CardItem): CardEmphasis {
    if (!emphasis || !chosen) return 'none';
    const base = card.node.baseId;
    if (base === chosen) return 'chosen';
    if (emphasis.ids.has(base)) return 'related';
    return mode === 'dim' ? 'dim' : 'none';
  }

  return (
    <Box>
      <DiagramToolbar
        model={model}
        search={view.search}
        onSearch={(text) => {
          patch({ search: text });
          setMatchIndex(-1);
        }}
        onSearchEnter={jump}
        matchCount={matches.length}
        allExpanded={allExpanded}
        canExpand={expandable.size > 0}
        onToggleAll={() => patch({ expansion: allExpanded ? new Set() : new Set(expandable) })}
        mode={mode}
        onMode={onMode}
        layout={orientation}
        onLayout={onOrientation}
        levelMode={view.levels.mode}
        onLevelMode={setLevelMode}
        anyLevel={view.levels.selected.size > 0}
        onAllLevels={resetLevels}
        zoom={view.zoom}
        onZoomStep={(direction) => zoomTo(stepZoom(view.zoom, direction), undefined, true)}
        onZoomReset={() => zoomTo(1, undefined, true)}
      />
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, mb: 1 }}>
        {offersStatus !== 'ready' && (
          <InlineNotice
            severity="warning"
            action={
              offersStatus === 'failed'
                ? { label: texts.notes.retry, onClick: () => void retryOffers(), ref: retryRef }
                : undefined
            }
          >
            {texts.notes.offers}
          </InlineNotice>
        )}
        {verticalNote && <InlineNotice severity="info">{verticalNote}</InlineNotice>}
        {chosen ? (
          <InlineNotice
            severity="info"
            action={{ label: texts.clear, onClick: () => choose(null) }}
          >
            {summaryText(model, chosen)}
            {mode === 'only' && (
              <Box component="span" sx={{ display: 'block' }}>
                {texts.summary.onlyRelated}
              </Box>
            )}
          </InlineNotice>
        ) : (
          !drawAll &&
          model.hasOffers && <InlineNotice severity="info">{texts.notes.lines}</InlineNotice>
        )}
      </Box>
      <Box
        ref={frameRef}
        onMouseDown={startPan}
        onAuxClick={(event) => {
          if (event.button === 1) event.preventDefault();
        }}
        sx={(theme) => ({
          position: 'relative',
          overflow: 'auto',
          maxHeight: 'min(70vh, 560px)',
          backgroundColor: theme.palette.background.paper,
          borderRadius: `${theme.shape.borderRadius}px`,
          border: `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`,
          cursor: panning ? 'grabbing' : undefined,
          userSelect: panning ? 'none' : undefined,
        })}
      >
        <Box
          sx={{
            position: 'relative',
            width: Math.ceil(layout.width * view.zoom),
            height: Math.ceil(layout.height * view.zoom),
          }}
        >
          <Box
            sx={{
              position: 'relative',
              width: layout.width,
              height: layout.height,
              transformOrigin: '0 0',
              transform: `scale(${view.zoom})`,
            }}
          >
            <DiagramLines
              layout={layout}
              maxLevel={model.maxLevel}
              emphasised={emphasis ? emphasis.lines : null}
              mode={mode}
              drawAll={drawAll}
            />
            <LevelChips layout={layout} levels={view.levels} onToggle={toggleLevel} />
            {emptyText && (
              <Box
                sx={(theme) => ({
                  position: 'absolute',
                  left: 16,
                  top: layout.rulerY + 56,
                  color: theme.palette.text.secondary,
                  fontSize: theme.typography.caption.fontSize,
                })}
              >
                {emptyText}
              </Box>
            )}
            <Box
              role="tree"
              aria-label={texts.tree}
              onKeyDown={onTreeKeyDown}
              onFocus={(event) => {
                const id = (event.target as HTMLElement).closest<HTMLElement>('[data-item]')
                  ?.dataset.item;
                if (id) setFocusId(id);
              }}
              sx={{
                position: 'absolute',
                inset: 0,
                pointerEvents: 'none',
                '& > [role="treeitem"]': { pointerEvents: 'auto' },
              }}
            >
              {layout.items.map((item) =>
                item.type === 'section' ? (
                  <DiagramSectionHeader
                    key={item.id}
                    item={item}
                    tabIndex={item.id === effectiveFocus ? 0 : -1}
                    register={register}
                    name={texts.names.section(item.title, item.expanded, item.count)}
                    onToggle={() => toggleSection(item.section)}
                  />
                ) : (
                  <DiagramCard
                    key={item.id}
                    item={item}
                    model={model}
                    vertical={orientation === 'v'}
                    emphasis={emphasisOfCard(item)}
                    mode={mode}
                    hit={matchSet.has(item.node.baseId)}
                    chosen={item.node.baseId === chosen}
                    link={linkOf(item)}
                    tabIndex={item.id === effectiveFocus ? 0 : -1}
                    register={register}
                    name={cardName(model, item.node)}
                    onToggle={() => toggleNode(item.id)}
                    onChoose={() => choose(item.node.baseId)}
                  />
                ),
              )}
            </Box>
          </Box>
        </Box>
      </Box>
      <Box
        sx={(theme) => ({
          mt: 1,
          fontSize: theme.typography.caption.fontSize,
          color: theme.palette.text.secondary,
        })}
      >
        {texts.hint}
      </Box>
      <LiveRegion text={live.text} serial={live.serial} />
    </Box>
  );
}

function LevelChips({
  layout,
  levels,
  onToggle,
}: {
  layout: DiagramLayout;
  levels: DiagramViewState['levels'];
  onToggle: (level: number) => void;
}) {
  const seen = new Map<number, number>();
  return (
    <Box
      role="group"
      aria-label={texts.levels.group}
      sx={{ position: 'absolute', left: 0, top: 0, zIndex: 6 }}
    >
      {layout.chips.map((chip) => {
        const occurrence = seen.get(chip.level) ?? 0;
        seen.set(chip.level, occurrence + 1);
        return (
          <LevelChip
            key={`${chip.level}-${occurrence}`}
            chip={chip}
            id={`${chip.level}-${occurrence}`}
            levels={levels}
            onToggle={onToggle}
          />
        );
      })}
    </Box>
  );
}

function LevelChip({
  chip,
  id,
  levels,
  onToggle,
}: {
  chip: ChipPlace;
  id: string;
  levels: DiagramViewState['levels'];
  onToggle: (level: number) => void;
}) {
  const pressed = levels.selected.has(chip.level);
  const included = levelIncluded(levels, chip.level);
  const action = pressed
    ? texts.levels.clickRemove
    : levels.mode === 'from'
      ? texts.levels.clickFrom
      : texts.levels.clickOnly;
  return (
    <Tooltip title={`${texts.levels.meaning(chip.level)} ${action}`} describeChild>
      <Box
        component="span"
        role="button"
        tabIndex={0}
        aria-pressed={pressed}
        aria-label={texts.levels.chip(chip.level)}
        data-chip={id}
        onClick={() => onToggle(chip.level)}
        onKeyDown={(event: KeyboardEvent<HTMLElement>) => {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            onToggle(chip.level);
          }
        }}
        sx={(theme) => {
          const colour = levelColour(theme, chip.level);
          return {
            position: 'absolute',
            left: chip.x,
            top: chip.y,
            boxSizing: 'border-box',
            width: 22,
            height: 22,
            borderRadius: '50%',
            border: `1.5px solid ${colour}`,
            display: 'grid',
            placeItems: 'center',
            cursor: 'pointer',
            fontSize: 11,
            fontWeight: theme.typography.fontWeightMedium,
            // The included tint is the level colour at 18 %: the level's own colour as text misses
            // 4.5:1 on it for levels 2 and 3, so the digit is plain text there (BR-SD-12).
            color: pressed
              ? theme.palette.primary.contrastText
              : included
                ? theme.palette.text.primary
                : colour,
            backgroundColor: pressed
              ? colour
              : included
                ? alpha(colour, 0.18)
                : theme.palette.background.paper,
            '&:focus-visible': {
              outline: `2px solid ${theme.palette.primary.main}`,
              outlineOffset: 2,
            },
          };
        }}
      >
        {chip.level}
      </Box>
    </Tooltip>
  );
}

export type { DiagramModel };
