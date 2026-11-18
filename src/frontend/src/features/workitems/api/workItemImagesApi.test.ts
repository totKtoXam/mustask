import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { env } from '../../../shared/config/env';
import { ImageRefusal } from '../../../shared/ui/RichTextEditor/images/host';
import { server } from '../../../test/mocks/server';
import { itemImagesHost } from './workItemImagesApi';

// feat-078 api-contract § 1-§ 3 through the host group the item's description gets (US-1, US-4).

const base = `${env.apiBaseUrl}/api/organizations/o1/projects/p1/workitems/MSK-1/images`;
const stored = { id: 'a', format: 'png', byteLength: 3, width: 2, height: 1 };

// jsdom's AbortSignal is not the one the fetch under MSW accepts, so these calls pass none.
const NO_SIGNAL = undefined as unknown as AbortSignal;

afterEach(() => vi.unstubAllGlobals());

describe('itemImagesHost', () => {
  it('uploads the bytes as they are, as octet-stream (US-1)', async () => {
    let seen: { type: string | null; size: number } | null = null;
    server.use(
      http.post(base, async ({ request }) => {
        seen = {
          type: request.headers.get('Content-Type'),
          size: (await request.arrayBuffer()).byteLength,
        };
        return HttpResponse.json(stored, { status: 201 });
      }),
    );

    const answer = await itemImagesHost('o1', 'p1', 'MSK-1').upload(
      new File(['abc'], 'a.png', { type: 'image/png' }),
      NO_SIGNAL,
    );

    expect(answer).toEqual(stored);
    // jsdom's File is not undici's Blob, so the byte count of the body is not checked here.
    expect(seen!.type).toBe('application/octet-stream');
  });

  it('turns a 400 of the images routes into a refusal with the catalog text (US-2, US-6)', async () => {
    server.use(
      http.post(`${base}/reductions`, () =>
        HttpResponse.json(
          { errorCode: 'images.reduction.cannot_reduce', detail: 'Не удалось уменьшить.' },
          { status: 400 },
        ),
      ),
    );

    const error = await itemImagesHost('o1', 'p1', 'MSK-1')
      .reduce(new File(['x'], 'a.png'), NO_SIGNAL)
      .catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ImageRefusal);
    expect((error as ImageRefusal).message).toBe('Не удалось уменьшить.');
    expect((error as ImageRefusal).code).toBe('images.reduction.cannot_reduce');
  });

  it('leaves any other failure as it is', async () => {
    server.use(http.post(base, () => HttpResponse.json({}, { status: 500 })));

    const error = await itemImagesHost('o1', 'p1', 'MSK-1')
      .upload(new File(['x'], 'a.png'), NO_SIGNAL)
      .catch((caught: unknown) => caught);

    expect(error).not.toBeInstanceOf(ImageRefusal);
  });

  it('reads the bytes with the token into an object URL that release revokes (US-4)', async () => {
    server.use(
      http.get(`${env.apiBaseUrl}/api/images/a`, () =>
        HttpResponse.arrayBuffer(new Uint8Array([1, 2]).buffer, {
          headers: { 'Content-Type': 'image/png' },
        }),
      ),
    );
    const revoke = vi.fn();
    vi.stubGlobal(
      'URL',
      Object.assign(URL, { createObjectURL: () => 'blob:one', revokeObjectURL: revoke }),
    );

    const host = itemImagesHost('o1', 'p1', 'MSK-1');
    const address = await host.address('a', NO_SIGNAL);
    host.release?.(address);

    expect(address).toBe('blob:one');
    expect(revoke).toHaveBeenCalledWith('blob:one');
  });
});
