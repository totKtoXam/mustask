import { useId, useState } from 'react';
import {
  ActionButton,
  BodyRow,
  Cell,
  ConfirmDialog,
  DialogShell,
  FormDialog,
  HeadCell,
  InlineTextArea,
  InlineTextField,
  TableCard,
  TablePager,
} from '../../../../shared/ui';
import { DEMO_IDENTIFIER } from '../data';
import { DemoRow } from '../parts';
import { useDemoCard } from './useDemoCard';

// Section 15, «Окна» (tech-015 design § 10.3, § 10.5): the card's window, the same window
// with the unsaved-changes guard, a busy window, a confirmation and a form window. They
// open for real from the buttons. The card's window draws no dimming; the confirmation over
// it, and the other two sizes, do.

type CardMode = 'card' | 'guard' | 'busy';
type DialogKind = CardMode | 'confirm' | 'form' | 'wide';

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
  const [expanded, setExpanded] = useState(false);

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
      onExpand={() => setExpanded((current) => !current)}
      expanded={expanded}
      onClose={() => onClose()}
    >
      {busy && <ActionButton label="Завершить сохранение" onClick={() => setBusy(false)} />}
      {card.body}
    </DialogShell>
  );
}

const WIDE_ROWS = [
  {
    id: 'r1',
    who: 'Системный Администратор',
    reason: 'Борис уволился, организация осталась без администратора',
  },
  {
    id: 'r2',
    who: 'Вера Иванова',
    reason: 'PRJ-1001PRJ-1002PRJ-1003PRJ-1004PRJ-1005PRJ-1006PRJ-1007PRJ-1008PRJ-1009',
  },
] as const;

/** The wide window: a read-only table that fits, a cell that wraps, a pager that waits. */
function WideDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [page, setPage] = useState(0);
  const [loading, setLoading] = useState(false);
  return (
    <DialogShell
      open={open}
      size="wide"
      title="Широкое окно: история"
      onClose={onClose}
      footer={<ActionButton label="Закрыть" onClick={onClose} />}
    >
      <TableCard
        aria-label="История"
        columns={[{ width: 200 }, {}]}
        head={['Кто назначил', 'Причина'].map((label) => (
          <HeadCell key={label} label={label} />
        ))}
        foot={
          <TablePager
            page={page}
            rowsPerPage={2}
            count={6}
            disabled={loading}
            onPageChange={(next) => {
              setPage(next);
              setLoading(true);
              setTimeout(() => setLoading(false), 1200);
            }}
          />
        }
      >
        {WIDE_ROWS.map((row) => (
          <BodyRow key={row.id}>
            <Cell>{row.who}</Cell>
            <Cell wrap>{row.reason}</Cell>
          </BodyRow>
        ))}
      </TableCard>
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
        <ActionButton label="Открыть широкое окно" onClick={() => openDialog('wide')} />
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
      <WideDialog open={open && shown.kind === 'wide'} onClose={close} />
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
