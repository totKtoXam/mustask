/* eslint-disable react-refresh/only-export-components -- a test-only module
   that re-exports Testing Library; it renders no component of its own. */
/**
 * Testing Library, with every `render` and `renderHook` under the panel’s theme.
 *
 * `vite.config.ts` aliases the bare specifier `@testing-library/react` to this
 * file for the Vitest run only (mod-015 plan-frontend.md § 2.5, D-8). The
 * shared patterns and the restyled screens read the panel's own theme keys
 * (`layout`, `density`, `palette.shell`, `workTracking`), and many component
 * tests call `render` with no `ThemeProvider`: under MUI's default theme
 * those keys do not exist. Wrapping here renders every component test under
 * the theme the app provides, without editing a single test file.
 *
 * It re-exports `@testing-library/react/pure`, which is what the package
 * index amounts to here: the index only adds an automatic `afterEach(cleanup)`
 * and the act-environment hooks when the runner exposes them as globals, and
 * Vitest globals are off — `src/test/setup.ts` cleans up by hand.
 */
import type { ComponentType, ReactNode } from 'react';
import { ThemeProvider } from '@mui/material/styles';
import {
  render as pureRender,
  renderHook as pureRenderHook,
  type RenderHookOptions,
  type RenderHookResult,
  type RenderOptions,
  type RenderResult,
} from '@testing-library/react/pure';
import { theme } from '../app/theme';

export * from '@testing-library/react/pure';

type Wrapper = ComponentType<{ children: ReactNode }>;

/** The caller's wrapper, if any, inside the app's ThemeProvider. */
function themed(CallerWrapper: Wrapper | undefined): Wrapper {
  return function ThemedWrapper({ children }: { children: ReactNode }) {
    return (
      <ThemeProvider theme={theme}>
        {CallerWrapper ? <CallerWrapper>{children}</CallerWrapper> : children}
      </ThemeProvider>
    );
  };
}

export function render(ui: ReactNode, options: RenderOptions = {}): RenderResult {
  const { wrapper, ...rest } = options as RenderOptions & { wrapper?: Wrapper };
  return pureRender(ui, { ...rest, wrapper: themed(wrapper) });
}

/** Hooks see the app's theme too: its breakpoints, `layout` and `density`. */
export function renderHook<Result, Props>(
  callback: (initialProps: Props) => Result,
  options: RenderHookOptions<Props> = {},
): RenderHookResult<Result, Props> {
  const { wrapper, ...rest } = options as RenderHookOptions<Props> & { wrapper?: Wrapper };
  return pureRenderHook(callback, { ...rest, wrapper: themed(wrapper) });
}
