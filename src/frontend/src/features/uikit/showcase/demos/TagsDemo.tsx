import { useState } from 'react';
import { Tag, TagList, type TagTone } from '../../../../shared/ui';
import { DEMO_TAGS } from '../data';
import { noop } from '../noop';
import { DemoGrid, DemoGroup, DemoRow } from '../parts';

// Section 8, «Теги» (tech-015 design § 10.3): the four tones filled and outlined, a list
// whose tags can be removed and added, and a read-only list.

const TONES: readonly TagTone[] = ['neutral', 'accent', 'success', 'warning'];

export function TagsDemo() {
  const [tags, setTags] = useState<readonly string[]>(DEMO_TAGS.slice(0, 2));
  const addable = DEMO_TAGS.find((tag) => !tags.includes(tag));

  return (
    <DemoGrid>
      <DemoGroup caption="Тона, залитые">
        <DemoRow>
          {TONES.map((tone, index) => (
            <Tag key={tone} tone={tone} label={DEMO_TAGS[index] ?? tone} />
          ))}
        </DemoRow>
      </DemoGroup>
      <DemoGroup caption="Тона, с рамкой">
        <DemoRow>
          {TONES.map((tone, index) => (
            <Tag key={tone} tone={tone} variant="outlined" label={DEMO_TAGS[index] ?? tone} />
          ))}
        </DemoRow>
      </DemoGroup>
      <DemoGroup caption="Список: удалить и добавить («+»)">
        <TagList
          tags={tags.map((label) => ({ id: label, label }))}
          onRemove={(id) => setTags((current) => current.filter((tag) => tag !== id))}
          onAdd={addable ? () => setTags((current) => [...current, addable]) : undefined}
        />
      </DemoGroup>
      <DemoGroup caption="Своё имя кнопки удаления: «Убрать «…»» (поле выбора нескольких)">
        <Tag label="Alpha" removeLabel={(label) => `Убрать «${label}»`} onRemove={noop} />
      </DemoGroup>
      <DemoGroup caption="Список только для чтения">
        <TagList tags={DEMO_TAGS.slice(0, 3).map((label) => ({ id: label, label }))} />
      </DemoGroup>
    </DemoGrid>
  );
}
