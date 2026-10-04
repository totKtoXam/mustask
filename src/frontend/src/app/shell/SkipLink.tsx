import Link from '@mui/material/Link';

/**
 * «Перейти к содержимому» (US-12, design § 12.1): the first focusable element,
 * visually hidden until it has focus. It moves focus into the main region
 * itself rather than following the fragment, so no `#main-content` enters
 * the address (plan § 9, D-16).
 */
export function SkipLink({ onSkip }: { onSkip: () => void }) {
  return (
    <Link
      href="#main-content"
      onClick={(event) => {
        event.preventDefault();
        onSkip();
      }}
      sx={(theme) => ({
        ...theme.mixins.visuallyHidden,
        '&:focus': {
          clip: 'auto',
          width: 'auto',
          height: 'auto',
          margin: 0,
          overflow: 'visible',
          whiteSpace: 'nowrap',
          position: 'fixed',
          top: theme.spacing(1),
          left: theme.spacing(1),
          zIndex: theme.zIndex.tooltip,
          padding: theme.spacing(1, 2),
          backgroundColor: theme.palette.background.paper,
          borderRadius: `${theme.shape.borderRadius}px`,
        },
      })}
    >
      Перейти к содержимому
    </Link>
  );
}
