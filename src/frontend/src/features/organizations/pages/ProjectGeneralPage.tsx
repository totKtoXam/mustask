import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import Link from '@mui/material/Link';
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import { useWorkContext } from '../../../shared/lib/workContext';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import { processDefinitionPath } from '../../process';
import { ConfirmAction } from '../components/ConfirmAction';
import { FactsPanel } from '../components/FactsPanel';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import { useDeactivateProject, useRenameProject } from '../hooks/mutations';
import { useProject } from '../hooks/queries';
import { notFoundLevel } from '../lib/notFoundLevel';
import { deactivatedProjectsPath } from '../paths';

type Dialog = 'rename' | 'deactivate' | null;

/**
 * Project «Общие» (US-3; design § 8): the project's name, its key — set at
 * creation and never changed, so no field or action edits it (BR-M1-1) —
 * and the read-only «Процесс» link to mod-001's screen (D-c). Renamed
 * through «Переименование проекта», deactivated after a confirmation.
 */
export function ProjectGeneralPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useProject(organizationId, projectId);
  const rename = useRenameProject(organizationId, projectId);
  const deactivate = useDeactivateProject(organizationId, projectId);
  const notify = useNotify();
  const navigate = useNavigate();
  const context = useWorkContext();
  const [dialog, setDialog] = useState<Dialog>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
    }
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const project = query.data;

  return (
    <>
      <PageHeader
        title="Общие"
        commands={
          project && !query.isError
            ? [
                {
                  label: 'Переименовать',
                  icon: <EditOutlinedIcon />,
                  onClick: () => setDialog('rename'),
                },
                {
                  label: 'Деактивировать',
                  icon: <ArchiveOutlinedIcon />,
                  onClick: () => setDialog('deactivate'),
                },
              ]
            : undefined
        }
      />
      {loading ? (
        <FormSkeleton fields={3} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <FactsPanel
          facts={[
            { label: 'Название', value: query.data.name },
            { label: 'Ключ', value: query.data.key, note: 'Задан при создании и не меняется' },
            {
              label: 'Типы рабочих элементов',
              value: (
                <>
                  {'Задаются процессом организации: '}
                  <Link component={RouterLink} to={processDefinitionPath(organizationId)}>
                    Процесс
                  </Link>
                </>
              ),
            },
          ]}
        />
      )}
      {dialog === 'rename' && project && (
        <NameDialog
          title="Переименование проекта"
          text="Корневая область проекта получит то же название. Ключ и названия команд не изменятся."
          helper="От 1 до 100 символов"
          initialName={project.name}
          submitLabel="Сохранить"
          submit={(name) => rename.mutateAsync(name)}
          onSucceeded={() => {
            setDialog(null);
            notify('Проект переименован.');
          }}
          onClose={closeDialog}
        />
      )}
      {dialog === 'deactivate' && project && (
        <ConfirmAction
          title="Деактивировать проект?"
          text={`Проект «${project.name}» (${project.key}) пропадёт из списков и переключателя, а его страницы, команды и области перестанут открываться. Ничего не будет удалено: проект можно восстановить в списке деактивированных проектов организации.`}
          confirmLabel="Деактивировать"
          action={async () => {
            // DC-7, judged before the switcher sees the change: deactivating
            // the switcher's project while another is active makes the
            // switcher fall back, and its notice is the only one.
            const organization = context.organizations.find(
              (entry) => entry.id === context.selection.organizationId,
            );
            const fallsBack =
              context.selection.projectId === project.id &&
              (organization?.projects.some((entry) => entry.id !== project.id) ?? false);
            await deactivate.mutateAsync();
            return fallsBack;
          }}
          onSucceeded={(fallsBack) => {
            setDialog(null);
            navigate(deactivatedProjectsPath(organizationId));
            if (!fallsBack) {
              notify(`Проект «${project.name}» деактивирован.`);
            }
          }}
          onClose={closeDialog}
        />
      )}
    </>
  );
}
