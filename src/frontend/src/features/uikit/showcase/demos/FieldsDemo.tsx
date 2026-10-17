import { useState } from 'react';
import Box from '@mui/material/Box';
import {
  CategoryMarker,
  InlineDateField,
  InlineMultiPickerField,
  InlineMultiSelectField,
  InlineNumberField,
  InlinePickerField,
  InlineSelectField,
  InlineTextArea,
  InlineTextField,
  PersonField,
  type SelectOption,
} from '../../../../shared/ui';
import {
  DEMO_AREAS,
  DEMO_DESCRIPTION,
  DEMO_PEOPLE,
  DEMO_PRIORITIES,
  DEMO_PROJECTS,
  DEMO_STATES,
  DEMO_TITLE,
} from '../data';
import { DemoGrid, DemoGroup } from '../parts';
import { noop } from '../noop';

// Section 12, «Поля ввода» (tech-015 design § 10.3, § 10.4): every kind of field and the
// states of the family. The labels are unique within the section and none is part of
// another, so a test finds a field by its name inside the section's region.

const PRIORITY_OPTIONS: SelectOption<string>[] = DEMO_PRIORITIES.map((value) => ({
  value,
  label: value,
}));

const RISK_OPTIONS: SelectOption<string>[] = ['Низкий', 'Средний', 'Высокий'].map((value) => ({
  value,
  label: value,
}));

const STATE_OPTIONS: SelectOption<string>[] = DEMO_STATES.map(({ code, name }) => ({
  value: code,
  label: name,
  startAdornment: <CategoryMarker code={code} size="field" />,
}));

const TEAM_OPTIONS: SelectOption<string>[] = [
  { value: 'mars', label: 'MARS' },
  { value: 'venus', label: 'VENUS' },
  { value: 'none', label: 'Без команды' },
];

const identity = (option: string) => option;

export function FieldsDemo() {
  const [title, setTitle] = useState<string>(DEMO_TITLE);
  const [topic, setTopic] = useState<string>(DEMO_TITLE);
  const [points, setPoints] = useState('3');
  const [due, setDue] = useState('5 октября');
  const [note, setNote] = useState('Короткая заметка');
  const [comment, setComment] = useState('');
  const [team, setTeam] = useState('');
  const [description, setDescription] = useState<string>(DEMO_DESCRIPTION);
  const [effort, setEffort] = useState('20');
  const [start, setStart] = useState('2026-10-05');
  const [target, setTarget] = useState('');
  const [priority, setPriority] = useState('2');
  const [risk, setRisk] = useState('');
  const [teams, setTeams] = useState<string[]>([]);
  const [projects, setProjects] = useState<string[]>([DEMO_PROJECTS[1]]);
  const [status, setStatus] = useState('Proposed');
  const [area, setArea] = useState<string | null>(DEMO_AREAS[0]);
  const [assignee, setAssignee] = useState<string | null>(DEMO_PEOPLE[0]);
  const [watcher, setWatcher] = useState<string | null>(null);

  return (
    <DemoGrid>
      <DemoGroup caption="В покое (наведите курсор — рамка, нажмите Tab — фокус)">
        <InlineTextField label="Тема" value={topic} onChange={setTopic} />
      </DemoGroup>
      <DemoGroup caption="Подсказка (нажмите Tab)">
        <InlineNumberField
          label="Оценка в пунктах"
          inputMode="decimal"
          hint="От 0 до 9 999"
          value={points}
          onChange={setPoints}
        />
      </DemoGroup>
      <DemoGroup caption="Ошибка">
        <InlineTextField
          label="Срок"
          value={due}
          onChange={setDue}
          error="Введите дату полностью."
        />
      </DemoGroup>
      <DemoGroup caption="Только чтение">
        <InlineTextField label="Автор" value={DEMO_PEOPLE[1]} onChange={noop} readOnly />
      </DemoGroup>
      <DemoGroup caption="Занято">
        <InlineTextField label="Заметка" value={note} onChange={setNote} busy />
      </DemoGroup>
      <DemoGroup caption="Отключено">
        <InlineTextField label="Код" value="MSK-US-2" onChange={noop} disabled />
      </DemoGroup>
      <DemoGroup caption="Пусто (наведите курсор — подсказка)">
        <InlineTextField
          label="Комментарий"
          value={comment}
          onChange={setComment}
          placeholder="Добавьте комментарий"
        />
      </DemoGroup>
      <DemoGroup caption="Рамка всегда: форма, которая начинается пустой">
        <InlineTextField
          label="Название команды"
          frame="always"
          required
          placeholder="Введите название"
          value={team}
          onChange={setTeam}
        />
      </DemoGroup>
      <DemoGroup caption="Заголовок карточки">
        <InlineTextField
          label="Заголовок"
          labelPlacement="hidden"
          size="title"
          value={title}
          onChange={setTitle}
        />
      </DemoGroup>
      <DemoGroup caption="Многострочный текст">
        <InlineTextArea
          label="Описание"
          hint="До 100 000 символов"
          value={description}
          onChange={setDescription}
        />
      </DemoGroup>
      <DemoGroup caption="Число">
        <InlineNumberField
          label="Трудоёмкость"
          inputMode="numeric"
          value={effort}
          onChange={setEffort}
        />
      </DemoGroup>
      <DemoGroup caption="Дата">
        <InlineDateField
          label="Дата начала"
          min="2000-01-01"
          max="2099-12-31"
          value={start}
          onChange={setStart}
        />
      </DemoGroup>
      <DemoGroup caption="Дата, пусто (наведите курсор — маска)">
        <InlineDateField
          label="Целевая дата"
          hint="Не раньше даты начала"
          value={target}
          onChange={setTarget}
        />
      </DemoGroup>
      <DemoGroup caption="Выбор">
        <InlineSelectField<string>
          label="Приоритет"
          hint="1 — самый высокий"
          options={PRIORITY_OPTIONS}
          value={priority}
          onChange={setPriority}
        />
      </DemoGroup>
      <DemoGroup caption="Необязательный выбор">
        <InlineSelectField<string>
          label="Риск"
          emptyLabel="Не указан"
          options={RISK_OPTIONS}
          value={risk}
          onChange={setRisk}
        />
      </DemoGroup>
      <DemoGroup caption="Выбор нескольких: пусто, рамка всегда (фильтр)">
        <InlineMultiSelectField<string>
          label="Команда"
          frame="always"
          emptyText="Все команды"
          options={TEAM_OPTIONS}
          value={teams}
          onChange={setTeams}
        />
      </DemoGroup>
      <DemoGroup caption="Выбор нескольких: список загружается">
        <InlineMultiSelectField<string>
          label="Исполнитель"
          frame="always"
          emptyText="Все исполнители"
          options={[]}
          loading
          value={[]}
          onChange={noop}
        />
      </DemoGroup>
      <DemoGroup caption="Выбор нескольких: только чтение">
        <InlineMultiSelectField<string>
          label="Состояния"
          options={TEAM_OPTIONS}
          value={['mars', 'none']}
          onChange={noop}
          readOnly
        />
      </DemoGroup>
      <DemoGroup caption="Выбор нескольких с поиском: выбранные — теги, список открыт, пока выбираете">
        <InlineMultiPickerField<string>
          label="Проекты"
          frame="always"
          hint="Необязательно"
          value={projects}
          options={DEMO_PROJECTS}
          onChange={setProjects}
          getOptionLabel={identity}
        />
      </DemoGroup>
      <DemoGroup caption="Выбор нескольких с поиском: список загружается">
        <InlineMultiPickerField<string>
          label="Участники"
          frame="always"
          options={[]}
          loading
          value={[]}
          onChange={noop}
          getOptionLabel={identity}
        />
      </DemoGroup>
      <DemoGroup caption="Выбор нескольких с поиском: ошибка">
        <InlineMultiPickerField<string>
          label="Команды"
          frame="always"
          error="Команда не найдена."
          value={[DEMO_PROJECTS[0]]}
          options={DEMO_PROJECTS}
          onChange={noop}
          getOptionLabel={identity}
        />
      </DemoGroup>
      <DemoGroup caption="Выбор нескольких с поиском: только чтение">
        <InlineMultiPickerField<string>
          label="Состав"
          readOnly
          value={[DEMO_PROJECTS[0], DEMO_PROJECTS[1]]}
          options={DEMO_PROJECTS}
          onChange={noop}
          getOptionLabel={identity}
        />
      </DemoGroup>
      <DemoGroup caption="Подпись слева: значок стоит в колонке подписи, текст — на одном уровне">
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
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
        </Box>
      </DemoGroup>
      <DemoGroup caption="Человек">
        <PersonField<string>
          label="Исполнитель"
          options={DEMO_PEOPLE}
          value={assignee}
          onChange={setAssignee}
          getOptionLabel={identity}
        />
      </DemoGroup>
      <DemoGroup caption="Не назначен">
        <PersonField<string>
          label="Наблюдатель"
          emptyText="Не назначен"
          options={DEMO_PEOPLE}
          value={watcher}
          onChange={setWatcher}
          getOptionLabel={identity}
        />
      </DemoGroup>
    </DemoGrid>
  );
}
