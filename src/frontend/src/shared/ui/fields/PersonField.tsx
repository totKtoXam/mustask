import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';
import { ThinPersonIcon } from '../icons/ThinIcons';
import { UserAvatar } from '../marks/UserAvatar';
import { InlinePickerField, type InlinePickerFieldProps } from './InlinePickerField';

export type PersonFieldProps<O> = Omit<
  InlinePickerFieldProps<O>,
  'startAdornment' | 'adornmentPlacement'
>;

/**
 * A person, chosen from a list (tech-015 design § 5.7): an `InlinePickerField` whose
 * start slot is `layout.mark.avatarInline` wide — a `UserAvatar` with the initials once
 * someone is set, the thin person glyph (`layout.mark.personGlyph`, `text.secondary`)
 * while nobody is. The empty value reads the host's `emptyText` («Не назначен») at rest.
 * `getOptionLabel` gives the person's name, which the avatar takes its initials from.
 */
export function PersonField<O>(props: PersonFieldProps<O>) {
  const theme = useTheme();
  const slot = theme.layout.mark.avatarInline;
  return (
    <InlinePickerField
      {...props}
      adornmentPlacement="value"
      startAdornment={
        props.value === null ? (
          <Box
            component="span"
            aria-hidden="true"
            data-person-empty=""
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: slot,
              height: slot,
              color: 'text.secondary',
            }}
          >
            <ThinPersonIcon sx={{ fontSize: theme.layout.mark.personGlyph }} />
          </Box>
        ) : (
          <UserAvatar name={props.getOptionLabel(props.value)} />
        )
      }
    />
  );
}
