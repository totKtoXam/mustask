/** Zoom of the diagram's picture (BR-SD-20): pure helpers, no DOM. */
export const ZOOM_MIN = 0.5;
export const ZOOM_MAX = 2;
/** One wheel notch or button press multiplies or divides by this. */
export const ZOOM_STEP = 1.1;

export function clampZoom(zoom: number): number {
  return Math.round(Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, zoom)) * 100) / 100;
}

export function stepZoom(zoom: number, direction: 1 | -1): number {
  return clampZoom(direction === 1 ? zoom * ZOOM_STEP : zoom / ZOOM_STEP);
}

/** The zoom a wheel movement of `deltaY` pixels gives (smooth, one notch ≈ ×1.1). */
export function wheelZoom(zoom: number, deltaY: number): number {
  return clampZoom(zoom * Math.exp(-deltaY * 0.001));
}

/** The same, unrounded and clamped: the accumulator for small deltas (BR-SD-20). */
export function wheelZoomRaw(zoom: number, deltaY: number): number {
  return Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, zoom * Math.exp(-deltaY * 0.001)));
}

/**
 * The scroll offset that keeps the picture point under `pointer` (frame coordinates) fixed when
 * the zoom changes from `from` to `to`.
 */
export function scrollAfterZoom(scroll: number, pointer: number, from: number, to: number): number {
  return ((scroll + pointer) / from) * to - pointer;
}
