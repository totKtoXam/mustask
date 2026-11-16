// The sample file of an editing session outlives the editor's remount: a save refreshes the template
// (a new version) and a create moves to the saved template's address, and both rebuild the editor.
// Held by template id (`new` until the first save), in memory only: a reload loses it, as the design says.
import type { SampleFile } from '../components/SourceTab';

const NEW = 'new';
const held = new Map<string, SampleFile>();

export const heldSample = {
  keyOf: (templateId: string | undefined) => templateId ?? NEW,
  get: (key: string): SampleFile | null => {
    return held.get(key) ?? null;
  },
  clear: () => held.clear(),
  set: (key: string, sample: SampleFile | null) => {
    if (sample) held.set(key, sample);
    else held.delete(key);
  },
  /** The first save of a new template: its sample now belongs to the saved id. */
  promote: (templateId: string) => {
    const sample = held.get(NEW);
    if (sample) held.set(templateId, sample);
    held.delete(NEW);
  },
};
