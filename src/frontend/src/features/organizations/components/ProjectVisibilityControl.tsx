import { useState } from 'react';
import FormControlLabel from '@mui/material/FormControlLabel';
import Stack from '@mui/material/Stack';
import Switch from '@mui/material/Switch';
import Typography from '@mui/material/Typography';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Section } from '../../../shared/ui/layout/Section';
import { useSetProjectVisibility } from '../hooks/mutations';
import { projectTexts } from '../lib/teamTexts';
import { useNotify } from './notice';

const texts = projectTexts.visibility;

export interface ProjectVisibilityControlProps {
  organizationId: string;
  projectId: string;
  projectName: string;
  /** The project's own value, as `GET /projects/{id}` served it. */
  value: boolean;
}

/**
 * «Видимость» of the project's «Параметры» (feat-041; doc-016 design § 6.1): the switch «Видим всей
 * организации». Turning it on asks first; turning it off writes at once. The page mounts it only for
 * a holder of `organizations.visibility.write`, so for anyone else the section is absent.
 */
export function ProjectVisibilityControl({
  organizationId,
  projectId,
  projectName,
  value,
}: ProjectVisibilityControlProps) {
  const write = useSetProjectVisibility(organizationId, projectId);
  const notify = useNotify();
  const [confirming, setConfirming] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);

  async function apply(next: boolean) {
    setRefusal(null);
    try {
      await write.mutateAsync(next);
      setConfirming(false);
      notify(next ? texts.turnedOn(projectName) : texts.turnedOff(projectName));
    } catch (error) {
      const text =
        error instanceof ApiError && error.status === 403
          ? 'Недостаточно прав'
          : apiErrorText(error);
      if (next) {
        setRefusal(text);
      } else {
        notify(text, 'error');
      }
    }
  }

  return (
    <Section title={texts.section}>
      <Stack spacing={1}>
        <FormControlLabel
          label={texts.label}
          control={
            <Switch
              checked={value}
              disabled={write.isPending}
              onChange={(_event, checked) => {
                if (checked) {
                  setRefusal(null);
                  setConfirming(true);
                } else {
                  void apply(false);
                }
              }}
            />
          }
        />
        <Typography variant="body2" color="text.secondary">
          {texts.helper}
        </Typography>
      </Stack>
      <ConfirmDialog
        open={confirming}
        title={texts.confirmTitle(projectName)}
        confirmLabel={texts.confirmLabel}
        busy={write.isPending}
        error={refusal}
        onConfirm={() => void apply(true)}
        onClose={() => setConfirming(false)}
      >
        {texts.confirmText}
      </ConfirmDialog>
    </Section>
  );
}
