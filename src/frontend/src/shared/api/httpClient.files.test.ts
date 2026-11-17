import { afterEach, describe, expect, it } from 'vitest';
import { HttpResponse, http } from 'msw';
import { server } from '../../test/mocks/server';
import { fakeAccessToken } from '../../test/mocks/handlers';
import { apiFetch, apiFetchBlob } from './httpClient';
import { resetSessionModule } from './session';

const api = 'http://localhost:5000';

function serveToken() {
  server.use(
    http.post(`${api}/api/session/token`, () =>
      HttpResponse.json({
        accessToken: fakeAccessToken(),
        expiresAt: new Date(Date.now() + 900_000).toISOString(),
        expiresIn: 900,
      }),
    ),
  );
}

describe('httpClient files (mod-024)', () => {
  afterEach(() => resetSessionModule());

  // jsdom's FormData is not undici's, so the body is not parsed here: the test pins that the client
  // neither stamps the JSON content type nor stringifies the form (the browser sets the boundary).
  it('sends a FormData body without a JSON content type', async () => {
    serveToken();
    let contentType: string | null = null;
    server.use(
      http.post(`${api}/api/upload`, ({ request }) => {
        contentType = request.headers.get('Content-Type');
        return HttpResponse.json({ ok: true });
      }),
    );
    const form = new FormData();
    form.append('file', new File(['a;b'], 'Реестр.csv'));

    await apiFetch('/api/upload', { method: 'POST', body: form });

    expect(contentType).not.toBe('application/json');
  });

  it('apiFetchBlob returns the bytes and the name from Content-Disposition (UTF-8 form first)', async () => {
    serveToken();
    server.use(
      http.get(
        `${api}/api/report`,
        () =>
          new HttpResponse('xlsx-bytes', {
            headers: {
              'Content-Disposition': `attachment; filename="report.xlsx"; filename*=UTF-8''%D0%9E%D1%82%D1%87%D1%91%D1%82.xlsx`,
            },
          }),
      ),
    );

    const answer = await apiFetchBlob('/api/report');

    expect(await answer.blob.text()).toBe('xlsx-bytes');
    expect(answer.fileName).toBe('Отчёт.xlsx');
  });

  it('apiFetchBlob without a header gives a null name', async () => {
    serveToken();
    server.use(http.get(`${api}/api/report`, () => new HttpResponse('x')));
    expect((await apiFetchBlob('/api/report')).fileName).toBeNull();
  });
});
