import SvgIcon, { type SvgIconProps } from '@mui/material/SvgIcon';

/**
 * The toolbar toggles' glyphs, drawn by hand (feat-049 design § 4.9, the prototype's `ICONS`):
 * display mode — three nodes and one link (the rest dimmed / the unrelated one struck out / the
 * related one filled); layout — a tree left-to-right and top-down; level mode — three level bars
 * (one filled / it and the lower ones filled). They stroke and fill with `currentColor`.
 */

const STROKE = { stroke: 'currentColor', strokeWidth: 1.8, fill: 'none' } as const;

export function DisplayDimIcon(props: SvgIconProps) {
  return (
    <SvgIcon {...props}>
      <g {...STROKE}>
        <path d="M8.6 10.6l6.8-3.4" />
        <circle cx="6" cy="12" r="3" fill="currentColor" />
        <circle cx="18" cy="6" r="3" />
        <circle cx="18" cy="18" r="3" opacity=".3" />
      </g>
    </SvgIcon>
  );
}

export function DisplayOnlyIcon(props: SvgIconProps) {
  return (
    <SvgIcon {...props}>
      <g {...STROKE}>
        <path d="M8.6 10.6l6.8-3.4" />
        <circle cx="6" cy="12" r="3" fill="currentColor" />
        <circle cx="18" cy="6" r="3" />
        <circle cx="18" cy="18" r="3" strokeDasharray="2 2.4" opacity=".55" />
        <path d="M15.5 20.5l5-5" opacity=".55" />
      </g>
    </SvgIcon>
  );
}

export function DisplayHighlightIcon(props: SvgIconProps) {
  return (
    <SvgIcon {...props}>
      <g {...STROKE}>
        <path d="M8.6 10.6l6.8-3.4" strokeWidth="2.6" />
        <circle cx="6" cy="12" r="3" fill="currentColor" />
        <circle cx="18" cy="6" r="3" fill="currentColor" />
        <circle cx="18" cy="18" r="3" />
      </g>
    </SvgIcon>
  );
}

export function LayoutHorizontalIcon(props: SvgIconProps) {
  return (
    <SvgIcon {...props}>
      <g {...STROKE}>
        <rect x="2.5" y="9.5" width="6" height="5" />
        <rect x="15.5" y="3.5" width="6" height="5" />
        <rect x="15.5" y="15.5" width="6" height="5" />
        <path d="M8.5 12H12V6h3.5M12 12v6h3.5" />
      </g>
    </SvgIcon>
  );
}

export function LayoutVerticalIcon(props: SvgIconProps) {
  return (
    <SvgIcon {...props}>
      <g {...STROKE}>
        <rect x="9.5" y="2.5" width="5" height="6" />
        <rect x="3.5" y="15.5" width="5" height="6" />
        <rect x="15.5" y="15.5" width="5" height="6" />
        <path d="M12 8.5V12H6V15.5M12 12h6v3.5" />
      </g>
    </SvgIcon>
  );
}

export function LevelsOnlyIcon(props: SvgIconProps) {
  return (
    <SvgIcon {...props}>
      <g {...STROKE}>
        <rect x="4" y="3.5" width="16" height="3.6" rx="1" />
        <rect x="4" y="10.2" width="16" height="3.6" rx="1" fill="currentColor" />
        <rect x="4" y="16.9" width="16" height="3.6" rx="1" />
      </g>
    </SvgIcon>
  );
}

export function LevelsFromIcon(props: SvgIconProps) {
  return (
    <SvgIcon {...props}>
      <g {...STROKE}>
        <rect x="4" y="3.5" width="16" height="3.6" rx="1" />
        <rect x="4" y="10.2" width="16" height="3.6" rx="1" fill="currentColor" />
        <rect x="4" y="16.9" width="16" height="3.6" rx="1" fill="currentColor" />
      </g>
    </SvgIcon>
  );
}
