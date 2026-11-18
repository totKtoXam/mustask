import { useSyncExternalStore, type ReactNode } from 'react';
import BrokenImageIcon from '@mui/icons-material/BrokenImage';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import OpenInFullIcon from '@mui/icons-material/OpenInFull';
import Box from '@mui/material/Box';
import ButtonBase from '@mui/material/ButtonBase';
import IconButton from '@mui/material/IconButton';
import LinearProgress from '@mui/material/LinearProgress';
import Skeleton from '@mui/material/Skeleton';
import Typography from '@mui/material/Typography';
import { NodeViewWrapper, useEditorState, type NodeViewProps } from '@tiptap/react';
import type { Theme } from '@mui/material/styles';
import type { AddressState, ImageRuntime } from './runtime';
import { removeImageAt } from './commands';

/** Reads one stored image through the editor's address cache (re-renders as the read ends). */
const NOT_ASKED: AddressState = { status: 'loading' };

export function useStoredImage(runtime: ImageRuntime, reference: string | null): AddressState {
  return useSyncExternalStore(
    (listener) =>
      reference === null ? () => {} : runtime.addresses.subscribe(reference, listener),
    () => (reference === null ? NOT_ASKED : runtime.addresses.snapshot(reference)),
  );
}

/** The image's own look: natural size, never scaled up, an edge on both themes (design § Image block). */
function Picture({ url, alt }: { url: string; alt: string }) {
  return (
    <Box
      component="img"
      src={url}
      alt={alt}
      draggable={false}
      sx={(theme) => ({
        display: 'block',
        maxWidth: '100%',
        height: 'auto',
        border: `1px solid ${theme.palette.divider}`,
        borderRadius: `${theme.shape.borderRadius}px`,
      })}
    />
  );
}

/** «Изображение недоступно»: a removed image or a failed read; no retry button (design § states). */
export function Unavailable({ text }: { text: string }) {
  return (
    <Box
      role="img"
      aria-label={text}
      sx={(theme) => ({
        display: 'flex',
        alignItems: 'center',
        gap: 1,
        minHeight: 96,
        px: 2,
        color: theme.palette.text.secondary,
        border: `1px dashed ${theme.palette.shell.inputBorder}`,
        borderRadius: `${theme.shape.borderRadius}px`,
      })}
    >
      <BrokenImageIcon aria-hidden="true" />
      <Typography variant="body2" component="span">
        {text}
      </Typography>
    </Box>
  );
}

const selectedOutline = (theme: Theme, selected: boolean) =>
  selected
    ? { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: '2px' }
    : { outline: 'none' };

/** The image block (design § Image block): loading, shown, unavailable, selected; a button when read-only. */
export function ImageView({ node, selected, editor, getPos, extension }: NodeViewProps) {
  const runtime = extension.options.runtime as ImageRuntime;
  const copy = runtime.copy;
  const imageId = node.attrs.imageId as string;
  const alt = node.attrs.alt as string;
  const name = alt === '' ? copy.defaultAlt : alt;
  const state = useStoredImage(runtime, imageId);
  const editable = useEditorState({
    editor,
    selector: ({ editor: current }) => current.isEditable,
  });
  const target = () => ({ imageId, alt: name, pos: getPos() ?? 0 });

  const body =
    state.status === 'loading' ? (
      <Skeleton variant="rounded" width={240} height={120} />
    ) : state.status === 'failed' ? (
      <Unavailable text={copy.unavailable} />
    ) : (
      <Picture url={state.url} alt={name} />
    );

  if (!editable) {
    // Read-only: one tab stop per image, opened by a click, Enter or Space (design § Read-only view).
    return (
      <NodeViewWrapper data-rt-image="" contentEditable={false}>
        {state.status === 'ready' ? (
          <ButtonBase
            role="button"
            aria-label={copy.openNamed(name)}
            onClick={() => runtime.ui.openView(target())}
            sx={(theme) => ({
              display: 'block',
              maxWidth: '100%',
              borderRadius: `${theme.shape.borderRadius}px`,
              '&.Mui-focusVisible': selectedOutline(theme, true),
            })}
          >
            {body}
          </ButtonBase>
        ) : (
          body
        )}
      </NodeViewWrapper>
    );
  }

  return (
    <NodeViewWrapper
      data-rt-image=""
      contentEditable={false}
      role="img"
      aria-label={state.status === 'failed' ? copy.unavailable : name}
      onDoubleClick={() => state.status === 'ready' && runtime.ui.openView(target())}
    >
      <Box
        sx={(theme) => ({
          position: 'relative',
          display: 'inline-block',
          maxWidth: '100%',
          borderRadius: `${theme.shape.borderRadius}px`,
          ...selectedOutline(theme, selected),
        })}
      >
        {body}
        {selected && (
          <Box
            sx={{ position: 'absolute', top: 4, right: 4, display: 'flex', gap: 0.5 }}
            onMouseDown={(event) => event.preventDefault()}
          >
            <PointerButton
              label={copy.open}
              icon={<OpenInFullIcon fontSize="small" />}
              onClick={() => runtime.ui.openView(target())}
            />
            <PointerButton
              label={copy.remove}
              icon={<DeleteOutlinedIcon fontSize="small" />}
              onClick={() => removeImageAt(editor, getPos() ?? 0, runtime)}
            />
          </Box>
        )}
      </Box>
    </NodeViewWrapper>
  );
}

/** A pointer button on a selected image: out of the tab order, named (design § Image block). */
function PointerButton({
  label,
  icon,
  onClick,
}: {
  label: string;
  icon: ReactNode;
  onClick: () => void;
}) {
  return (
    <IconButton
      size="small"
      aria-label={label}
      title={label}
      tabIndex={-1}
      onClick={onClick}
      sx={(theme) => ({
        width: 28,
        height: 28,
        backgroundColor: theme.palette.background.paper,
        '&:hover': { backgroundColor: theme.palette.background.paper },
        // A 36 px hit area on a touch screen.
        '&::after': { content: '""', position: 'absolute', inset: -4 },
      })}
    >
      {icon}
    </IconButton>
  );
}

/** The upload placeholder: a block of its own at the insert place (design § Placeholder). */
export function PlaceholderView({ selected, extension }: NodeViewProps) {
  const runtime = extension.options.runtime as ImageRuntime;
  const text = runtime.copy.uploading;
  return (
    <NodeViewWrapper data-rt-image-placeholder="" contentEditable={false}>
      <Box
        role="group"
        aria-busy="true"
        aria-label={text}
        sx={(theme) => ({
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'center',
          gap: 1,
          minHeight: 96,
          px: 2,
          border: `1px dashed ${theme.palette.shell.inputBorder}`,
          borderRadius: `${theme.shape.borderRadius}px`,
          ...selectedOutline(theme, selected),
        })}
      >
        <Typography variant="body2" color="text.secondary">
          {text}
        </Typography>
        <LinearProgress />
      </Box>
    </NodeViewWrapper>
  );
}
