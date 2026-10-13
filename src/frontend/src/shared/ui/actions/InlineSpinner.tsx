import CircularProgress from '@mui/material/CircularProgress';
import { useTheme } from '@mui/material/styles';

/**
 * The spinner of a busy button and of a field's busy adornment
 * (`layout.inlineSpinner`): it takes the colour of the text it stands in, so a
 * button's label colour stays the contrast the tone was chosen for. Internal to
 * the kit — a host sets `busy` on the control, never draws a spinner.
 */
export function InlineSpinner() {
  const theme = useTheme();
  return <CircularProgress color="inherit" size={theme.layout.inlineSpinner} />;
}
