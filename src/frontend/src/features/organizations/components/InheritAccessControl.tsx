import { useState } from 'react';
import FormControlLabel from '@mui/material/FormControlLabel';
import Stack from '@mui/material/Stack';
import Switch from '@mui/material/Switch';
import Typography from '@mui/material/Typography';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { InlineNotice } from '../../../shared/ui/feedback/InlineNotice';
import { FieldsSkeleton } from '../../../shared/ui/feedback/FieldsSkeleton';
import { Section } from '../../../shared/ui/layout/Section';
import type { AreaPage } from '../api/organizationsApi';
import { useSetInheritsAccess } from '../hooks/grantMutations';
import { inheritAccess } from '../lib/grantTexts';
import { useNotify } from './notice';
import { SwitchOffDialog } from './SwitchOffDialog';

export interface InheritAccessControlProps {
  organizationId: string;
  /** A project's node or a sub-area; never a department. */
  area: AreaPage;
  /** `area.write` on the node and the parent's code (api-contract § 3), as the panel knows them. */
  canSwitch: boolean;
  /** The reads that decide `canSwitch` have not landed: a skeleton, not the read-only fact that then turns into the switch. */
  deciding?: boolean;
}

/** The sentence under the switch: what the state means here, by the node's kind and place (design § 4). */
function stateText(area: AreaPage, value: boolean): string {
  if (area.type === 'project') {
    if (!area.department) return inheritAccess.stateText.topLevel;
    return value
      ? inheritAccess.stateText.nodeOn(area.department.name, area.name)
      : inheritAccess.stateText.nodeOff(area.department.name, area.name);
  }
  const parent = area.parent?.name ?? '';
  return value
    ? inheritAccess.stateText.subAreaOn(parent)
    : inheritAccess.stateText.subAreaOff(parent);
}

/**
 * «Наследовать доступ» (mod-019 design §§ 4-5, US-2): a switch for the one who may write the node and
 * its parent, a read-only fact («Включено» / «Выключено») for everyone else. Turning it on writes at once;
 * turning it off opens the confirmation with what it would do.
 */
export function InheritAccessControl({
  organizationId,
  area,
  canSwitch,
  deciding = false,
}: InheritAccessControlProps) {
  const value = area.inheritsAccess ?? true;
  const write = useSetInheritsAccess(organizationId, area.id);
  const notify = useNotify();
  const [confirming, setConfirming] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);

  async function apply(next: boolean) {
    setRefusal(null);
    try {
      await write.mutateAsync(next);
      setConfirming(false);
      notify(next ? inheritAccess.turnedOn : inheritAccess.turnedOff);
    } catch (error) {
      setConfirming(false);
      setRefusal(
        error instanceof ApiError && error.status === 403
          ? inheritAccess.forbidden
          : apiErrorText(error),
      );
    }
  }

  return (
    <Section title={inheritAccess.section}>
      <Stack spacing={1}>
        {deciding ? (
          <FieldsSkeleton count={1} />
        ) : canSwitch ? (
          <FormControlLabel
            label={inheritAccess.label}
            control={
              <Switch
                checked={value}
                disabled={write.isPending}
                onChange={(_event, checked) => {
                  if (checked) void apply(true);
                  else setConfirming(true);
                }}
              />
            }
          />
        ) : (
          <Typography variant="body2">
            {`${inheritAccess.label}: ${value ? inheritAccess.on : inheritAccess.off}`}
          </Typography>
        )}
        <Typography variant="body2" color="text.secondary">
          {stateText(area, value)}
        </Typography>
        {refusal && <InlineNotice severity="error">{refusal}</InlineNotice>}
      </Stack>
      {confirming && (
        <SwitchOffDialog
          organizationId={organizationId}
          area={area}
          sourceName={
            area.type === 'project' ? (area.department?.name ?? '') : (area.parent?.name ?? '')
          }
          busy={write.isPending}
          error={null}
          onConfirm={() => void apply(false)}
          onClose={() => setConfirming(false)}
        />
      )}
    </Section>
  );
}
