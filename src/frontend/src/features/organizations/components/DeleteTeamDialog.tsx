import { ConfirmAction } from './ConfirmAction';

/**
 * «Удаление команды» (US-7; design § 9.7): destructive, with no restore. The
 * text is US-7's, verbatim: the team's areas pass to the first team.
 */
export function DeleteTeamDialog<R>({
  teamName,
  firstTeamName,
  action,
  onSucceeded,
  onClose,
}: {
  teamName: string;
  firstTeamName: string;
  action(): Promise<R>;
  onSucceeded(result: R): void;
  onClose(contextGone: boolean): void;
}) {
  return (
    <ConfirmAction
      title="Удаление команды"
      text={`Удалить команду «${teamName}»? Её области перейдут команде «${firstTeamName}». Восстановить команду будет нельзя.`}
      confirmLabel="Удалить команду"
      destructive
      action={action}
      onSucceeded={onSucceeded}
      onClose={onClose}
    />
  );
}
