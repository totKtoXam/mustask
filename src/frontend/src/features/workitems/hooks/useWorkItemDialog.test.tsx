import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { MemoryRouter, useLocation, useNavigate, type To } from 'react-router-dom';
import { useWorkItemDialog } from './useWorkItemDialog';

const BACKLOG = '/organizations/o/teams/t/backlog';

interface Entry {
  pathname: string;
  search?: string;
  hash?: string;
  state?: unknown;
}

/** The hook inside a router whose history starts as `entries`, standing on the last. */
function setup(entries: (string | Entry)[], index = entries.length - 1) {
  const view = renderHook(
    () => ({
      dialog: useWorkItemDialog(),
      location: useLocation(),
      navigate: useNavigate(),
    }),
    {
      wrapper: ({ children }: { children: ReactNode }) => (
        <MemoryRouter initialEntries={entries} initialIndex={index}>
          {children}
        </MemoryRouter>
      ),
    },
  );
  const current = () => view.result.current;
  const go = (to: To | number) =>
    act(() => {
      if (typeof to === 'number') current().navigate(to);
      else current().navigate(to);
    });
  const address = () => `${current().location.pathname}${current().location.search}`;
  return { view, current, go, address };
}

describe('feat-029 US-1: the dialog lives in the address (BR-C-2)', () => {
  it('is closed on a page without the parameter, and closing it does nothing', () => {
    const t = setup([BACKLOG]);

    expect(t.current().dialog.identifier).toBeNull();
    expect(t.current().dialog.depth).toBe(0);
    act(() => t.current().dialog.close());
    expect(t.address()).toBe(BACKLOG);
  });

  it('reads the identifier as received, and takes an empty value for no dialog', () => {
    expect(setup([`${BACKLOG}?workitem=MSK-US-999`]).current().dialog.identifier).toBe(
      'MSK-US-999',
    );
    expect(setup([`${BACKLOG}?workitem=msk-bug-4`]).current().dialog.identifier).toBe('msk-bug-4');
    expect(setup([`${BACKLOG}?workitem=`]).current().dialog.identifier).toBeNull();
  });

  it('opens over the page with one history entry, keeping the rest of the address', () => {
    const t = setup([{ pathname: BACKLOG, search: '?level=story', hash: '#row' }]);

    act(() => t.current().dialog.open('MSK-US-2'));

    expect(t.current().dialog.identifier).toBe('MSK-US-2');
    expect(t.current().dialog.depth).toBe(1);
    expect(t.current().location.pathname).toBe(BACKLOG);
    expect(new URLSearchParams(t.current().location.search).get('level')).toBe('story');
    expect(new URLSearchParams(t.current().location.search).get('workitem')).toBe('MSK-US-2');
    expect(t.current().location.hash).toBe('#row');
    expect(t.current().location.state).toEqual({ cardDepth: 1 });
  });

  it('Back closes it', () => {
    const t = setup([BACKLOG]);
    act(() => t.current().dialog.open('MSK-US-2'));

    t.go(-1);

    expect(t.current().dialog.identifier).toBeNull();
    expect(t.address()).toBe(BACKLOG);
  });

  it('closes with one close path, going back as many entries as the card pushed', () => {
    const t = setup([BACKLOG]);
    act(() => t.current().dialog.open('MSK-US-2'));
    act(() => t.current().dialog.switchTo('MSK-FEAT-6'));
    expect(t.current().dialog.identifier).toBe('MSK-FEAT-6');
    expect(t.current().dialog.depth).toBe(2);

    // Back shows the item the dialog switched from.
    t.go(-1);
    expect(t.current().dialog.identifier).toBe('MSK-US-2');
    expect(t.current().dialog.depth).toBe(1);
    t.go(1);

    act(() => t.current().dialog.close());

    expect(t.current().dialog.identifier).toBeNull();
    expect(t.address()).toBe(BACKLOG);
    // The entry before the page is still before it: nothing was pushed or lost on the way.
    t.go(1);
    expect(t.current().dialog.identifier).toBe('MSK-US-2');
  });

  it('closes a dialog the address opened by replacing the address, so Back still leaves the page', () => {
    const t = setup(['/home', `${BACKLOG}?level=story&workitem=MSK-US-2`]);
    expect(t.current().dialog.identifier).toBe('MSK-US-2');
    expect(t.current().dialog.depth).toBe(0);

    act(() => t.current().dialog.close());

    expect(t.current().dialog.identifier).toBeNull();
    expect(t.address()).toBe(`${BACKLOG}?level=story`);
    t.go(-1);
    expect(t.address()).toBe('/home');
  });

  describe('a dialog the address opened that switched to a related item (R1-1)', () => {
    const SHARED = `${BACKLOG}?level=story&workitem=MSK-US-2`;

    it('marks the entries it pushes as a run that started on the address', () => {
      const t = setup(['/home', SHARED]);

      act(() => t.current().dialog.switchTo('MSK-FEAT-6'));
      expect(t.current().location.state).toEqual({ cardDepth: 1, cardBase: 'address' });

      act(() => t.current().dialog.switchTo('MSK-BUG-3'));
      expect(t.current().dialog.depth).toBe(2);
      expect(t.current().location.state).toEqual({ cardDepth: 2, cardBase: 'address' });
    });

    it('closes at once: the address loses the parameter, and Back still leaves the page', async () => {
      const t = setup(['/home', SHARED]);
      act(() => t.current().dialog.switchTo('MSK-FEAT-6'));

      act(() => t.current().dialog.close());

      await waitFor(() => expect(t.address()).toBe(`${BACKLOG}?level=story`));
      expect(t.current().dialog.identifier).toBeNull();
      expect(t.current().location.state).toBeNull();
      t.go(-1);
      expect(t.address()).toBe('/home');
    });

    it('goes back over every item it switched through, then strips the first', async () => {
      const t = setup(['/home', SHARED]);
      act(() => t.current().dialog.switchTo('MSK-FEAT-6'));
      act(() => t.current().dialog.switchTo('MSK-BUG-3'));

      act(() => t.current().dialog.close());

      await waitFor(() => expect(t.address()).toBe(`${BACKLOG}?level=story`));
      expect(t.current().dialog.identifier).toBeNull();
      t.go(-1);
      expect(t.address()).toBe('/home');
    });

    it('never shows the first item again on the way out', async () => {
      const seen: (string | null)[] = [];
      const view = renderHook(
        () => {
          const dialog = useWorkItemDialog();
          seen.push(dialog.identifier);
          return { dialog, location: useLocation() };
        },
        {
          wrapper: ({ children }: { children: ReactNode }) => (
            <MemoryRouter initialEntries={['/home', SHARED]} initialIndex={1}>
              {children}
            </MemoryRouter>
          ),
        },
      );
      act(() => view.result.current.dialog.switchTo('MSK-FEAT-6'));
      seen.length = 0;

      act(() => view.result.current.dialog.close());

      await waitFor(() => expect(view.result.current.location.search).toBe('?level=story'));
      expect(seen).not.toContain('MSK-US-2');
    });

    it('does not strip a dialog that is opened later', async () => {
      const t = setup(['/home', SHARED]);
      act(() => t.current().dialog.switchTo('MSK-FEAT-6'));
      act(() => t.current().dialog.close());
      await waitFor(() => expect(t.address()).toBe(`${BACKLOG}?level=story`));

      act(() => t.current().dialog.open('MSK-US-9'));

      expect(t.current().dialog.identifier).toBe('MSK-US-9');
      expect(t.current().location.state).toEqual({ cardDepth: 1 });
      act(() => t.current().dialog.close());
      await waitFor(() => expect(t.current().dialog.identifier).toBeNull());
      expect(t.address()).toBe(`${BACKLOG}?level=story`);
    });

    it('leaves a dialog opened over a page as it was: one step back is the whole close', () => {
      const t = setup(['/home', `${BACKLOG}?level=story`]);
      act(() => t.current().dialog.open('MSK-US-2'));
      act(() => t.current().dialog.switchTo('MSK-FEAT-6'));
      expect(t.current().location.state).toEqual({ cardDepth: 2 });

      act(() => t.current().dialog.close());

      expect(t.address()).toBe(`${BACKLOG}?level=story`);
      t.go(-1);
      expect(t.address()).toBe('/home');
    });
  });

  it('is dropped by leaving the page’s address, since the new address has no parameter', () => {
    const t = setup([BACKLOG]);
    act(() => t.current().dialog.open('MSK-US-2'));

    t.go('/organizations/o/teams/other/backlog');

    expect(t.current().dialog.identifier).toBeNull();
  });
});
