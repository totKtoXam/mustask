import { useId, useState } from 'react';
import {
  ActionButton,
  ConfirmDialog,
  DialogShell,
  FormDialog,
  InlineTextArea,
  InlineTextField,
} from '../../../../shared/ui';
import { DEMO_IDENTIFIER } from '../data';
import { DemoRow } from '../parts';
import { useDemoCard } from './useDemoCard';

// Section 15, «Окна» (tech-015 design § 10.3, § 10.5): the card's window, the same window
// with the unsaved-changes guard, a busy window, a confirmation and a form window. They
// open for real from the buttons. The card's window draws no dimming; the confirmation over
// it, and the other two sizes, do.

type CardMode = 'card' | 'guard' | 'busy';
type DialogKind = CardMode | 'confirm' | 'form';

const CARD_DIALOG_ID = 'showcase-card-dialog';

function CardDialog({
  open,
  mode,
  onClose,
}: {
  open: boolean;
  mode: CardMode;
  onClose: () => void;
}) {
  const headingId = useId();
  const card = useDemoCard({ headingLevel: 2, headingId, idBase: CARD_DIALOG_ID, long: true });
  // A busy window cannot be closed; the button inside it lets the demo's "save" finish.
  const [busy, setBusy] = useState(mode === 'busy');

  return (
    <DialogShell
      open={open}
      size="card"
      labelledBy={headingId}
      header={card.header}
      band={card.band}
      busy={busy}
      guard={
        mode === 'guard'
          ? {
              dirty: card.dirty,
              title: 'Закрыть без сохранения?',
              text: `Несохранённые изменения рабочего элемента ${DEMO_IDENTIFIER} будут потеряны.`,
              discardLabel: 'Закрыть без сохранения',
            }
          : undefined
      }
      onExpand={() => {
        onClose();
        document.getElementById('card')?.scrollIntoView();
      }}
      onClose={() => onClose()}
    >
      {busy && <ActionButton label="Завершить сохранение" onClick={() => setBusy(false)} />}
      {card.body}
    </DialogShell>
  );
}

export function DialogsDemo() {
  const [open, setOpen] = useState(false);
  const [shown, setShown] = useState<{ kind: DialogKind; cycle: number }>({
    kind: 'card',
    cycle: 0,
  });
  const [team, setTeam] = useState('');
  const [description, setDescription] = useState('');

  function openDialog(kind: DialogKind) {
    // A new `cycle` remounts the card window, so each opening starts from the saved card.
    setShown((current) => ({ kind, cycle: current.cycle + 1 }));
    setOpen(true);
  }
  const close = () => setOpen(false);
  const isCard = shown.kind === 'card' || shown.kind === 'guard' || shown.kind === 'busy';

  return (
    <>
      <DemoRow>
        <ActionButton label="Открыть окно карточки" onClick={() => openDialog('card')} />
        <ActionButton
          label="Открыть окно с защитой от потери изменений"
          onClick={() => openDialog('guard')}
        />
        <ActionButton label="Открыть занятое окно" onClick={() => openDialog('busy')} />
        <ActionButton label="Открыть подтверждение" onClick={() => openDialog('confirm')} />
        <ActionButton label="Открыть окно с формой" onClick={() => openDialog('form')} />
      </DemoRow>
      <CardDialog
        key={shown.cycle}
        open={open && isCard}
        mode={isCard ? (shown.kind as CardMode) : 'card'}
        onClose={close}
      />
      <ConfirmDialog
        open={open && shown.kind === 'confirm'}
        title="Удалить тег?"
        confirmLabel="Удалить"
        destructive
        onConfirm={close}
        onClose={close}
      >
        {`Тег «Discovery» будет удалён из рабочего элемента ${DEMO_IDENTIFIER}.`}
      </ConfirmDialog>
      <FormDialog
        open={open && shown.kind === 'form'}
        title="Новая команда"
        submitLabel="Создать"
        submitDisabled={team.trim() === ''}
        onSubmit={close}
        onClose={close}
      >
        <InlineTextField
          label="Название команды"
          frame="always"
          required
          value={team}
          onChange={setTeam}
        />
        <InlineTextArea
          label="Описание"
          frame="always"
          value={description}
          onChange={setDescription}
        />
      </FormDialog>
    </>
  );
}
