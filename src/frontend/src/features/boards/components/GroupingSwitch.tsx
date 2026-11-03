import { ActionButton, MenuButton } from '../../../shared/ui';
import type { Grouping } from '../api/aggregateApi';
import { GROUPING_LABELS, GROUPING_SWITCH_LABEL } from '../lib/grouping';

/**
 * «Группировка» (feat-048 BR-AV-1, design § 2): the kit's `MenuButton` as a radio menu behind a
 * text trigger «Группировка: По проекту». Absent when the level offers a single option. It is not
 * a filter and not in the address.
 */
export function GroupingSwitch({
  offered,
  value,
  onChange,
}: {
  offered: readonly Grouping[];
  value: Grouping;
  onChange(next: Grouping): void;
}) {
  if (offered.length < 2) return null;
  return (
    <MenuButton
      label={GROUPING_SWITCH_LABEL}
      selection="radio"
      items={offered.map((grouping) => ({
        label: GROUPING_LABELS[grouping],
        current: grouping === value,
        onSelect: () => onChange(grouping),
      }))}
      trigger={(trigger) => (
        <ActionButton
          label={`${GROUPING_SWITCH_LABEL}: ${GROUPING_LABELS[value]}`}
          menu={{ open: trigger['aria-expanded'] }}
          {...trigger}
        />
      )}
    />
  );
}
