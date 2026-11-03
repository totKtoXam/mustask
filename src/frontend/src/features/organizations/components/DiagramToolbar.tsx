import type { KeyboardEvent } from 'react';
import Box from '@mui/material/Box';
import Tooltip from '@mui/material/Tooltip';
import UnfoldLessIcon from '@mui/icons-material/UnfoldLess';
import UnfoldMoreIcon from '@mui/icons-material/UnfoldMore';
import {
  ActionButton,
  IconAction,
  IconToggleGroup,
  InlineTextField,
  ThinAddIcon,
  ThinRemoveIcon,
} from '../../../shared/ui';
import type { DisplayMode, LayoutOrientation } from '../hooks/useStructureSettings';
import { areasPage, structureDiagram as texts } from '../lib/areaTexts';
import type { LevelMode } from '../lib/diagramLevels';
import type { DiagramModel } from '../lib/diagramModel';
import { ZOOM_MAX, ZOOM_MIN } from '../lib/diagramZoom';
import {
  DisplayDimIcon,
  DisplayHighlightIcon,
  DisplayOnlyIcon,
  LayoutHorizontalIcon,
  LayoutVerticalIcon,
  LevelsFromIcon,
  LevelsOnlyIcon,
} from './DiagramIcons';

/** The diagram's toolbar (feat-049 design §§ 3, 4.9): search, expand all, toggles, zoom, counters. */

const tip = texts.tooltip;

export function DiagramToolbar({
  model,
  search,
  onSearch,
  onSearchEnter,
  matchCount,
  allExpanded,
  canExpand,
  onToggleAll,
  mode,
  onMode,
  layout,
  onLayout,
  levelMode,
  onLevelMode,
  anyLevel,
  onAllLevels,
  zoom,
  onZoomStep,
  onZoomReset,
}: {
  model: DiagramModel;
  search: string;
  onSearch: (text: string) => void;
  onSearchEnter: () => void;
  matchCount: number;
  allExpanded: boolean;
  canExpand: boolean;
  onToggleAll: () => void;
  mode: DisplayMode;
  onMode: (mode: DisplayMode) => void;
  layout: LayoutOrientation;
  onLayout: (layout: LayoutOrientation) => void;
  levelMode: LevelMode;
  onLevelMode: (mode: LevelMode) => void;
  anyLevel: boolean;
  onAllLevels: () => void;
  zoom: number;
  onZoomStep: (direction: 1 | -1) => void;
  onZoomReset: () => void;
}) {
  const percent = Math.round(zoom * 100);
  const { counts } = model;
  const searching = search.trim() !== '';
  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1.5, mb: 1 }}>
      <Box sx={{ width: 220 }}>
        <InlineTextField
          label={texts.search}
          labelPlacement="hidden"
          placeholder={texts.search}
          frame="always"
          value={search}
          onChange={onSearch}
          inputProps={{
            onKeyDown: (event: KeyboardEvent<HTMLInputElement>) => {
              if (event.key === 'Enter') {
                event.preventDefault();
                onSearchEnter();
              }
            },
          }}
        />
      </Box>
      <Box
        component="span"
        role="status"
        sx={(theme) => ({
          fontSize: theme.typography.caption.fontSize,
          color: theme.palette.text.secondary,
        })}
      >
        {searching ? (matchCount > 0 ? texts.found(matchCount) : texts.notFound) : ''}
      </Box>
      <ActionButton
        tone="subtle"
        label={allExpanded ? areasPage.collapseAll : areasPage.expandAll}
        icon={allExpanded ? <UnfoldLessIcon /> : <UnfoldMoreIcon />}
        disabled={!canExpand}
        onClick={onToggleAll}
      />
      <IconToggleGroup
        label={texts.mode.label}
        value={mode}
        onChange={(next) => onMode(next as DisplayMode)}
        items={[
          {
            value: 'dim',
            label: texts.mode.dim.label,
            icon: <DisplayDimIcon />,
            tooltip: tip(texts.mode.label, texts.mode.dim.label, texts.mode.dim.hint),
          },
          {
            value: 'only',
            label: texts.mode.only.label,
            icon: <DisplayOnlyIcon />,
            tooltip: tip(texts.mode.label, texts.mode.only.label, texts.mode.only.hint),
          },
          {
            value: 'hl',
            label: texts.mode.hl.label,
            icon: <DisplayHighlightIcon />,
            tooltip: tip(texts.mode.label, texts.mode.hl.label, texts.mode.hl.hint),
          },
        ]}
      />
      <IconToggleGroup
        label={texts.layout.label}
        value={layout}
        onChange={(next) => onLayout(next as LayoutOrientation)}
        items={[
          {
            value: 'h',
            label: texts.layout.h.label,
            icon: <LayoutHorizontalIcon />,
            tooltip: tip(texts.layout.label, texts.layout.h.label, texts.layout.h.hint),
          },
          {
            value: 'v',
            label: texts.layout.v.label,
            icon: <LayoutVerticalIcon />,
            tooltip: tip(texts.layout.label, texts.layout.v.label, texts.layout.v.hint),
          },
        ]}
      />
      <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
        <IconToggleGroup
          label={texts.levelMode.label}
          value={levelMode}
          onChange={(next) => onLevelMode(next as LevelMode)}
          items={[
            {
              value: 'only',
              label: texts.levelMode.only.label,
              icon: <LevelsOnlyIcon />,
              tooltip: tip(
                texts.levelMode.label,
                texts.levelMode.only.label,
                texts.levelMode.only.hint,
              ),
            },
            {
              value: 'from',
              label: texts.levelMode.from.label,
              icon: <LevelsFromIcon />,
              tooltip: tip(
                texts.levelMode.label,
                texts.levelMode.from.label,
                texts.levelMode.from.hint,
              ),
            },
          ]}
        />
        <Tooltip title={texts.levels.allHint}>
          <Box component="span" sx={{ display: 'inline-flex' }}>
            <ActionButton
              tone="subtle"
              label={texts.levels.all}
              disabled={!anyLevel}
              onClick={onAllLevels}
            />
          </Box>
        </Tooltip>
      </Box>
      <Box
        role="group"
        aria-label={texts.zoom.group}
        sx={{ display: 'inline-flex', alignItems: 'center' }}
      >
        <IconAction
          label={texts.zoom.out}
          icon={<ThinRemoveIcon />}
          inactive={zoom <= ZOOM_MIN}
          onClick={() => onZoomStep(-1)}
        />
        <Tooltip title={texts.zoom.valueTip(percent)}>
          <Box component="span" sx={{ display: 'inline-flex' }}>
            <ActionButton
              tone="subtle"
              label={`${percent}%`}
              aria-label={texts.zoom.value(percent)}
              disabled={percent === 100}
              onClick={onZoomReset}
            />
          </Box>
        </Tooltip>
        <IconAction
          label={texts.zoom.in}
          icon={<ThinAddIcon />}
          inactive={zoom >= ZOOM_MAX}
          onClick={() => onZoomStep(1)}
        />
      </Box>
      <Box
        component="span"
        sx={(theme) => ({
          ml: 'auto',
          fontSize: theme.typography.caption.fontSize,
          color: theme.palette.text.secondary,
        })}
      >
        {texts.counters(counts.departments, counts.projects, counts.areas, counts.teams)}
      </Box>
    </Box>
  );
}
