import { useState } from 'react';
import type { Grouping } from '../api/aggregateApi';
import { groupingAnnouncement } from '../lib/grouping';

export interface Announcement {
  text: string;
  serial: number;
}

/**
 * The text of a view's `StatusRegion` and the way to speak into it. A change of the grouping is
 * announced as «Группировка: …» (design § 2); the first frame announces nothing. The change is
 * noticed while rendering, so the region is updated in the same frame the new grouping is drawn.
 */
export function useAnnouncement(grouping: Grouping) {
  const [announcement, setAnnouncement] = useState<Announcement>({ text: '', serial: 0 });
  const [seen, setSeen] = useState(grouping);
  if (seen !== grouping) {
    setSeen(grouping);
    setAnnouncement(({ serial }) => ({ text: groupingAnnouncement(grouping), serial: serial + 1 }));
  }
  const announce = (text: string) =>
    setAnnouncement(({ serial }) => ({ text, serial: serial + 1 }));
  return { announcement, announce };
}
