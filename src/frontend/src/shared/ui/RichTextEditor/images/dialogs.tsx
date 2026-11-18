import { useId, useSyncExternalStore } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import LinearProgress from '@mui/material/LinearProgress';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Skeleton from '@mui/material/Skeleton';
import Typography from '@mui/material/Typography';
import useMediaQuery from '@mui/material/useMediaQuery';
import { useTheme } from '@mui/material/styles';
import { DialogTitleBar } from '../../DialogTitleBar';
import type { ImageController } from './controller';
import { Unavailable, useStoredImage } from './ImageViews';
import type { AddressState, ImageRuntime, ImageTarget } from './runtime';

/** «7,4 МБ»: megabytes with one decimal and a comma (US-6). */
export function formatSize(bytes: number): string {
  return `${(bytes / 1_048_576).toFixed(1).replace('.', ',')} МБ`;
}

function useNarrow(): boolean {
  const theme = useTheme();
  return useMediaQuery(theme.breakpoints.down('sm'));
}

/** The preview of the reduced image through the host's address rule. */
function Preview({ runtime, state, alt }: PreviewProps) {
  if (state.status === 'loading') {
    return <Skeleton variant="rounded" sx={{ width: '100%', aspectRatio: '4 / 3' }} />;
  }
  if (state.status === 'failed') return <Unavailable text={runtime.copy.unavailable} />;
  return (
    <Box
      component="img"
      src={state.url}
      alt={alt}
      sx={{ display: 'block', maxWidth: '100%', maxHeight: '40vh', objectFit: 'contain' }}
    />
  );
}

interface PreviewProps {
  runtime: ImageRuntime;
  state: AddressState;
  alt: string;
}

/**
 * «Изображение больше 5 МБ» (US-6, design § Dialog): one dialog that changes its body through the
 * steps, so focus and Esc stay in one place. Cancel, Esc and the scrim are the same cancel everywhere.
 */
export function ReductionDialog({
  controller,
  runtime,
}: {
  controller: ImageController;
  runtime: ImageRuntime;
}) {
  const state = useSyncExternalStore(controller.subscribe, () => controller.state);
  const narrow = useNarrow();
  const titleId = useId();
  const linesId = useId();
  const copy = runtime.copy.reduce;
  const preview = useStoredImage(runtime, state?.step === 'result' ? state.result.image.id : null);
  const previewStatus = preview.status;

  if (!state) return null;
  const { offer } = state;

  return (
    <Dialog
      open
      onClose={() => controller.cancel()}
      maxWidth="sm"
      fullWidth
      fullScreen={narrow}
      aria-labelledby={titleId}
      aria-describedby={state.step === 'result' ? linesId : undefined}
    >
      <DialogTitle id={titleId} sx={{ pb: 0.5 }}>
        {copy.title}
      </DialogTitle>
      <DialogContent>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
          {copy.queue(offer.index, offer.total, offer.file.name)}
        </Typography>

        {state.step === 'offer' && (
          <Typography>{copy.offer(formatSize(offer.file.size))}</Typography>
        )}

        {state.step === 'reducing' && (
          <Box role="status">
            <Typography sx={{ mb: 1 }}>{copy.reducing}</Typography>
            <LinearProgress />
          </Box>
        )}

        {state.step === 'result' && (
          <>
            <Typography id={linesId} sx={{ fontWeight: 600, mb: 1.5 }}>
              <span style={{ display: 'block' }}>
                {copy.was(
                  formatSize(state.result.original.byteLength),
                  state.result.original.width,
                  state.result.original.height,
                )}
              </span>
              <span style={{ display: 'block' }}>
                {copy.now(
                  formatSize(state.result.image.byteLength),
                  state.result.image.width,
                  state.result.image.height,
                )}
              </span>
            </Typography>
            <Preview runtime={runtime} state={preview} alt={copy.previewAlt} />
          </>
        )}

        {state.step === 'cannot' && <Alert severity="warning">{copy.cannot}</Alert>}
        {state.step === 'failed' && (
          <Alert severity="error" role="alert">
            {runtime.copy.refusals.reduceFailed}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        {state.step === 'offer' && (
          <>
            <Button onClick={() => controller.cancel()}>{copy.cancel}</Button>
            <Button variant="contained" autoFocus onClick={() => controller.reduce()}>
              {copy.reduce}
            </Button>
          </>
        )}
        {state.step === 'reducing' && (
          <Button autoFocus onClick={() => controller.cancel()}>
            {copy.cancel}
          </Button>
        )}
        {state.step === 'result' && (
          <>
            <Button onClick={() => controller.cancel()}>{copy.cancel}</Button>
            <Button
              variant="contained"
              autoFocus
              // The member cannot judge what is inserted while the preview cannot be read.
              aria-disabled={previewStatus === 'failed' ? true : undefined}
              onClick={() => {
                if (previewStatus !== 'failed') controller.insert();
              }}
            >
              {copy.insert}
            </Button>
          </>
        )}
        {state.step === 'cannot' && (
          <Button variant="contained" autoFocus onClick={() => controller.cancel()}>
            {copy.close}
          </Button>
        )}
        {state.step === 'failed' && (
          <>
            <Button onClick={() => controller.cancel()}>{copy.cancel}</Button>
            <Button variant="contained" autoFocus onClick={() => controller.reduce()}>
              {copy.retry}
            </Button>
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}

/** «Просмотр изображения» (design § Full-size view): natural size, scrolling both ways, focusable. */
export function FullSizeDialog({
  runtime,
  target,
  onClose,
}: {
  runtime: ImageRuntime;
  target: ImageTarget;
  onClose: () => void;
}) {
  const state = useStoredImage(runtime, target.imageId);
  const narrow = useNarrow();
  const { view } = runtime.copy;

  return (
    <Dialog open onClose={onClose} maxWidth="lg" fullWidth fullScreen={narrow}>
      {/* The dialog is named by its title (the kit's title bar), not by the alt caption below. */}
      <DialogTitleBar title={view.title} onClose={onClose} />
      <Typography variant="caption" color="text.secondary" sx={{ px: 3, display: 'block' }}>
        {target.alt}
      </Typography>
      <DialogContent
        tabIndex={0}
        role="region"
        aria-label={view.scrollArea}
        sx={{ overflow: 'auto' }}
      >
        {state.status === 'loading' && <Skeleton variant="rounded" width={320} height={200} />}
        {state.status === 'failed' && <Unavailable text={runtime.copy.unavailable} />}
        {state.status === 'ready' && (
          <Box
            component="img"
            src={state.url}
            alt={target.alt}
            sx={{ display: 'block', maxWidth: 'none' }}
          />
        )}
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onClose}>
          {view.close}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/** «Меню изображения»: the keyboard path to open and remove (Shift+F10, design § Keyboard). */
export function ImageMenu({
  runtime,
  anchor,
  onOpen,
  onRemove,
  onClose,
}: {
  runtime: ImageRuntime;
  anchor: HTMLElement | null;
  onOpen: () => void;
  onRemove: () => void;
  onClose: () => void;
}) {
  const { copy } = runtime;
  return (
    <Menu
      open
      anchorEl={anchor}
      onClose={onClose}
      slotProps={{ list: { 'aria-label': copy.menu } }}
    >
      <MenuItem onClick={onOpen}>{copy.open}</MenuItem>
      <MenuItem onClick={onRemove}>{copy.remove}</MenuItem>
    </Menu>
  );
}
