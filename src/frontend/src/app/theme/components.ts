import type { ThemeOptions } from '@mui/material/styles';
import { navigationEntryClass } from './classNames';
import { tooltipEnterDelayMs, typeScale } from './tokens';

/**
 * Component defaults (design § 2.6, plan-frontend.md § 3.2). Every value
 * comes from the theme passed to the callbacks — palette, typography,
 * spacing, `layout`, `density` — so this file holds no colour of its own.
 *
 * The overrides of § 3.2 keep BR-2's closed list of accent uses: MUI tints a
 * selected menu item, list item and tab label with the accent by default, and
 * these put them back on neutral colours. The navigation's own selected
 * marker (`shell.navigationSelected` plus the accent bar) is drawn by the
 * navigation components themselves.
 */
export const components: ThemeOptions['components'] = {
  MuiCssBaseline: {
    styleOverrides: (theme) => ({
      body: { backgroundColor: theme.palette.background.default },
    }),
  },
  MuiButtonBase: {
    // The focus ring is the theme's `focusVisible` (theme.ts), so the ripple
    // is not needed as a focus cue.
    defaultProps: { disableRipple: true },
  },
  MuiButton: {
    defaultProps: { disableElevation: true },
    styleOverrides: {
      root: ({ theme }) => ({
        minHeight: theme.density.controlHeight,
        padding: theme.spacing(0.5, 1.5),
        variants: [
          {
            // Secondary commands keep a neutral label; only the leading icon
            // carries the accent (§ 2.2, BR-2).
            props: { variant: 'text', color: 'primary' },
            style: {
              color: theme.palette.text.primary,
              '--variant-textColor': theme.palette.text.primary,
              '& .MuiButton-startIcon, & .MuiButton-endIcon': { color: theme.palette.primary.main },
              '&:hover': { backgroundColor: theme.palette.action.hover },
            },
          },
          {
            props: { variant: 'outlined', color: 'primary' },
            style: {
              color: theme.palette.text.primary,
              '--variant-outlinedColor': theme.palette.text.primary,
              borderColor: theme.palette.shell.inputBorder,
              '& .MuiButton-startIcon, & .MuiButton-endIcon': { color: theme.palette.primary.main },
              '&:hover': {
                backgroundColor: theme.palette.action.hover,
                borderColor: theme.palette.text.primary,
              },
            },
          },
          {
            props: { size: 'small' },
            style: { minHeight: 0 },
          },
        ],
      }),
    },
  },
  MuiIconButton: {
    styleOverrides: {
      sizeSmall: ({ theme }) => ({
        padding: theme.spacing(0.5),
        '& .MuiSvgIcon-root': { fontSize: theme.typography.pxToRem(20) },
      }),
    },
  },
  MuiTextField: {
    defaultProps: { size: 'small' },
  },
  MuiOutlinedInput: {
    styleOverrides: {
      root: ({ theme }) => ({
        [`&.Mui-focused .MuiOutlinedInput-notchedOutline`]: {
          borderColor: theme.palette.primary.main,
          borderWidth: 2,
        },
        [`&.Mui-error .MuiOutlinedInput-notchedOutline`]: { borderColor: theme.palette.error.main },
        [`&.Mui-disabled .MuiOutlinedInput-notchedOutline`]: {
          borderColor: theme.palette.action.disabled,
        },
      }),
      notchedOutline: ({ theme }) => ({ borderColor: theme.palette.shell.inputBorder }),
    },
  },
  MuiInputLabel: {
    styleOverrides: {
      // A focused field keeps its label neutral; the 2 px accent border is
      // the focus indicator (§ 2.2).
      root: ({ theme }) => ({
        '&.Mui-focused': { color: theme.palette.text.secondary },
        '&.Mui-error': { color: theme.palette.error.main },
      }),
    },
  },
  MuiTable: {
    defaultProps: { size: 'small' },
  },
  MuiTableCell: {
    styleOverrides: {
      root: ({ theme }) => ({ borderBottomColor: theme.palette.divider }),
      sizeSmall: ({ theme }) => ({ padding: theme.spacing(0.5, 1.5) }),
      head: ({ theme }) => ({
        ...theme.typography.subtitle2,
        color: theme.palette.text.secondary,
        backgroundColor: theme.palette.shell.tableHeader,
      }),
    },
  },
  MuiTableRow: {
    styleOverrides: {
      root: ({ theme }) => ({
        height: theme.density.registryRowHeight,
        '&.MuiTableRow-hover:hover': { backgroundColor: theme.palette.action.hover },
      }),
    },
  },
  MuiTabs: {
    styleOverrides: {
      root: ({ theme }) => ({ minHeight: theme.density.tabHeight }),
    },
  },
  MuiTab: {
    styleOverrides: {
      root: ({ theme }) => ({
        minHeight: theme.density.tabHeight,
        color: theme.palette.text.secondary,
        '&.Mui-selected': {
          color: theme.palette.text.primary,
          fontWeight: typeScale.button.weight,
        },
      }),
    },
  },
  MuiListItemButton: {
    styleOverrides: {
      root: ({ theme }) => ({
        '&.Mui-selected, &.Mui-selected:hover': { backgroundColor: theme.palette.action.selected },
        // Navigation entries (design § 2.6): their own hover, and the
        // selected marker — the navigation-selected surface with a 3 px
        // accent bar on the left edge, drawn inside the entry so it adds no
        // width.
        [`&.${navigationEntryClass}`]: {
          '&:hover': { backgroundColor: theme.palette.shell.navigationHover },
          '&.Mui-selected, &.Mui-selected:hover': {
            backgroundColor: theme.palette.shell.navigationSelected,
            boxShadow: `inset ${theme.layout.selectedMarkerWidth}px 0 0 ${theme.palette.primary.main}`,
          },
        },
      }),
    },
  },
  MuiMenuItem: {
    styleOverrides: {
      root: ({ theme }) => ({
        '&.Mui-selected, &.Mui-selected:hover, &.Mui-selected.Mui-focusVisible': {
          backgroundColor: theme.palette.action.selected,
        },
      }),
    },
  },
  MuiTooltip: {
    defaultProps: { enterDelay: tooltipEnterDelayMs },
  },
  MuiChip: {
    defaultProps: { size: 'small' },
  },
  MuiDialogTitle: {
    styleOverrides: {
      // The title is the dialog's h2 (design § 2.6), set at the `h2` scale.
      root: ({ theme }) => ({ ...theme.typography.h2, padding: theme.spacing(2, 3) }),
    },
  },
  MuiDialogActions: {
    styleOverrides: {
      root: ({ theme }) => ({ padding: theme.spacing(2, 3) }),
    },
  },
  MuiAppBar: {
    styleOverrides: {
      // The top bar uses the content surface (BR-2). Its divider is drawn
      // inside the bar, so the bar stays exactly `layout.topBarHeight` high.
      root: ({ theme }) => ({
        backgroundColor: theme.palette.background.paper,
        color: theme.palette.text.primary,
        boxShadow: `inset 0 -${theme.layout.dividerWidth}px 0 ${theme.palette.divider}`,
      }),
    },
  },
  MuiAvatar: {
    styleOverrides: {
      root: ({ theme }) => ({
        width: theme.layout.avatarSize,
        height: theme.layout.avatarSize,
        fontSize: theme.typography.pxToRem(typeScale.avatarInitials.size),
        fontWeight: typeScale.avatarInitials.weight,
      }),
      colorDefault: ({ theme }) => ({
        backgroundColor: theme.palette.text.secondary,
        color: theme.palette.getContrastText(theme.palette.text.secondary),
      }),
    },
  },
  // Selection controls and progress stay off the accent (§ 2.2):
  // `palette.secondary` is the neutral text colour.
  MuiCheckbox: { defaultProps: { color: 'secondary' } },
  MuiRadio: { defaultProps: { color: 'secondary' } },
  MuiSwitch: { defaultProps: { color: 'secondary' } },
  MuiCircularProgress: { defaultProps: { color: 'secondary' } },
};
