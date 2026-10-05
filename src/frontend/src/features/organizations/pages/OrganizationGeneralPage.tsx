import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import { useWorkContext } from '../../../shared/lib/workContext';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import { ConfirmAction } from '../components/ConfirmAction';
import { FactsPanel } from '../components/FactsPanel';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import { useDeactivateOrganization, useRenameOrganization } from '../hooks/mutations';
import { useOrganization } from '../hooks/queries';
import { notFoundLevel } from '../lib/notFoundLevel';
import { DEACTIVATED_ORGANIZATIONS_ROUTE } from '../paths';

type Dialog = 'rename' | 'deactivate' | null;

/**
 * Organization «Общие» (US-2; design § 6): the organization's facts and its
 * commands, renamed through «Переименование организации» (DC-5) and
 * deactivated after a reversible-style confirmation (DC-3). Its first read
 * validates the address: not found answers «Организация не найдена».
 */
export function OrganizationGeneralPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useOrganization(organizationId);
  const rename = useRenameOrganization(organizationId);
  const deactivate = useDeactivateOrganization(organizationId);
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
  const organization = query.data;

  return (
    <>
      <PageHeader
        title="Общие"
        commands={
          organization && !query.isError
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
        <FormSkeleton fields={1} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <FactsPanel facts={[{ label: 'Название', value: query.data.name }]} />
      )}
      {dialog === 'rename' && organization && (
        <NameDialog
          title="Переименование организации"
          helper="От 1 до 100 символов"
          initialName={organization.name}
          submitLabel="Сохранить"
          submit={(name) => rename.mutateAsync(name)}
          onSucceeded={() => {
            setDialog(null);
            notify('Организация переименована.');
          }}
          onClose={closeDialog}
        />
      )}
      {dialog === 'deactivate' && organization && (
        <ConfirmAction
          title="Деактивировать организацию?"
          text={`Организация «${organization.name}» пропадёт из списков и переключателя, а её страницы и страницы её проектов перестанут открываться. Ничего не будет удалено: организацию можно восстановить в списке деактивированных организаций.`}
          confirmLabel="Деактивировать"
          action={async () => {
            // DC-7, judged before the switcher sees the change: deactivating
            // the switcher's organization while another is active makes the
            // switcher fall back, and its notice is the only one.
            const fallsBack =
              context.selection.organizationId === organization.id &&
              context.organizations.some((entry) => entry.id !== organization.id);
            await deactivate.mutateAsync();
            return fallsBack;
          }}
          onSucceeded={(fallsBack) => {
            setDialog(null);
            navigate(DEACTIVATED_ORGANIZATIONS_ROUTE);
            if (!fallsBack) {
              notify(`Организация «${organization.name}» деактивирована.`);
            }
          }}
          onClose={closeDialog}
        />
      )}
    </>
  );
}
