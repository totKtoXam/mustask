import { useState, type ReactNode } from 'react';
import {
  BoxedTabPanel,
  BoxedTabs,
  CardHeaderBar,
  CategoryMarker,
  ColumnLayout,
  IconAction,
  InlineNumberField,
  InlinePickerField,
  InlineSelectField,
  InlineTextArea,
  InlineTextField,
  MoreActionsMenu,
  PersonField,
  PropertyBand,
  Section,
  SplitButton,
  TagList,
  ThinRefreshIcon,
  ThinSaveIcon,
  ThinUndoIcon,
  InlineEmpty,
  type SelectOption,
} from '../../../../shared/ui';
import {
  DEMO_AREAS,
  DEMO_DESCRIPTION,
  DEMO_IDENTIFIER,
  DEMO_PEOPLE,
  DEMO_PRIORITIES,
  DEMO_STATES,
  DEMO_TAGS,
  DEMO_TITLE,
  DEMO_TYPE_LABEL,
} from '../data';
import { noop } from '../noop';

// The showcase's one demo card, `MSK-US-2` (tech-015 design § 10.3, row 14): the card's
// header, its band with the fixed blocks, one tab and the body in columns, built from the
// kit alone. `useDemoCard` returns the three parts, so the page's panel (section 14) and
// the card dialog (section 15) draw the same card.

const STATE_OPTIONS: SelectOption<string>[] = DEMO_STATES.map(({ code, name }) => ({
  value: code,
  label: name,
  startAdornment: <CategoryMarker code={code} size="field" />,
}));

const PRIORITY_OPTIONS: SelectOption<string>[] = DEMO_PRIORITIES.map((value) => ({
  value,
  label: value,
}));

const SAVE_ITEMS = [{ label: 'Сохранить и закрыть' }, { label: 'Сохранить и создать новую' }];

/** Sections that fill the dialog's body, so the header and the band can be seen to stay. */
const FILLER_TITLES = [
  'Обсуждение',
  'История',
  'Вложения',
  'Связи',
  'Тесты',
  'Документы',
  'Сборки',
  'Релизы',
];

const identity = (option: string) => option;

export interface DemoCardOptions {
  /** 2 in the dialog, 3 inside the showcase's section. */
  headingLevel: 2 | 3;
  /** The heading's id: what the dialog's `aria-labelledby` points at. */
  headingId?: string;
  /** Distinguishes the tabs' ids when the card is drawn twice (the page and the dialog). */
  idBase: string;
  /** More sections in the body, so the dialog's panel scrolls. */
  long?: boolean;
}

export interface DemoCard {
  header: ReactNode;
  band: ReactNode;
  body: ReactNode;
  /** Something was edited and not saved. */
  dirty: boolean;
}

export function useDemoCard({
  headingLevel,
  headingId,
  idBase,
  long = false,
}: DemoCardOptions): DemoCard {
  const [saved, setSaved] = useState({ title: DEMO_TITLE, description: DEMO_DESCRIPTION });
  const [title, setTitle] = useState(saved.title);
  const [description, setDescription] = useState(saved.description);
  const [assignee, setAssignee] = useState<string | null>(DEMO_PEOPLE[0]);
  const [status, setStatus] = useState('InProgress');
  const [area, setArea] = useState<string | null>(DEMO_AREAS[0]);
  const [priority, setPriority] = useState('2');
  const [effort, setEffort] = useState('20');

  const dirty = title !== saved.title || description !== saved.description;
  const save = () => setSaved({ title, description });
  const undo = () => {
    setTitle(saved.title);
    setDescription(saved.description);
  };

  const header = (
    <CardHeaderBar
      appearance="story"
      typeLabel={DEMO_TYPE_LABEL}
      identifier={DEMO_IDENTIFIER}
      headingLevel={headingLevel}
      headingId={headingId}
      title={
        <InlineTextField
          label="Заголовок"
          labelPlacement="hidden"
          size="title"
          value={title}
          onChange={setTitle}
        />
      }
      meta={
        <PersonField<string>
          label="Исполнитель"
          labelPlacement="hidden"
          emptyText="Не назначен"
          options={DEMO_PEOPLE}
          value={assignee}
          onChange={setAssignee}
          getOptionLabel={identity}
        />
      }
      actions={
        <>
          <SplitButton
            label="Сохранить"
            icon={<ThinSaveIcon />}
            groupLabel="Сохранение"
            menuLabel="Ещё варианты"
            items={SAVE_ITEMS}
            disabled={!dirty}
            onClick={save}
          />
          <IconAction size="bar" label="Обновить" icon={<ThinRefreshIcon />} onClick={noop} />
          <IconAction
            size="bar"
            label="Отменить изменения"
            icon={<ThinUndoIcon />}
            inactive={!dirty}
            onClick={undo}
          />
          <MoreActionsMenu
            size="bar"
            items={[
              { label: 'Копировать ссылку', onSelect: noop },
              'divider',
              { label: 'Удалить', destructive: true, onSelect: noop },
            ]}
          />
        </>
      }
    />
  );

  const band = (
    <PropertyBand
      blocks={[
        <>
          <InlineSelectField<string>
            label="Статус"
            labelPlacement="start"
            options={STATE_OPTIONS}
            value={status}
            onChange={setStatus}
            startAdornment={<CategoryMarker code={status} size="field" />}
          />
          <InlinePickerField<string>
            label="Область"
            labelPlacement="start"
            options={DEMO_AREAS}
            value={area}
            onChange={setArea}
            getOptionLabel={identity}
          />
        </>,
        <>
          <InlineSelectField<string>
            label="Приоритет"
            labelPlacement="start"
            options={PRIORITY_OPTIONS}
            value={priority}
            onChange={setPriority}
          />
          <InlineNumberField
            label="Трудоёмкость"
            labelPlacement="start"
            inputMode="numeric"
            value={effort}
            onChange={setEffort}
          />
        </>,
      ]}
      meta="Изменено 5 октября"
      tabs={
        <BoxedTabs
          idBase={idBase}
          aria-label="Разделы карточки"
          value="details"
          onChange={noop}
          tabs={[{ value: 'details', label: 'Детали' }]}
        />
      }
    />
  );

  const body = (
    <BoxedTabPanel idBase={idBase} value="details" selected>
      <ColumnLayout
        main={
          <>
            <Section title="Описание" headingLevel={3}>
              <InlineTextArea
                label="Описание"
                labelPlacement="hidden"
                value={description}
                onChange={setDescription}
              />
            </Section>
            {long &&
              FILLER_TITLES.map((fillerTitle) => (
                <Section key={fillerTitle} title={fillerTitle} headingLevel={3}>
                  <InlineEmpty>Пока ничего нет.</InlineEmpty>
                </Section>
              ))}
          </>
        }
        side={
          <Section title="Метки" headingLevel={3}>
            <TagList tags={DEMO_TAGS.slice(0, 1).map((label) => ({ id: label, label }))} />
          </Section>
        }
      />
    </BoxedTabPanel>
  );

  return { header, band, body, dirty };
}
