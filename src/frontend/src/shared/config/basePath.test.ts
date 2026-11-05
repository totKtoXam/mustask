import { afterEach, describe, expect, it, vi } from 'vitest';
import { getBasePath, stripBase, withBase } from './basePath';

afterEach(() => vi.unstubAllEnvs());

describe('basePath at the root', () => {
  it('adds and removes nothing', () => {
    vi.stubEnv('BASE_URL', '/');
    expect(getBasePath()).toBe('');
    expect(withBase('/home?a=1')).toBe('/home?a=1');
    expect(stripBase('/signed-out')).toBe('/signed-out');
  });
});

describe('basePath under /mustask', () => {
  it('drops the trailing slash of BASE_URL', () => {
    vi.stubEnv('BASE_URL', '/mustask/');
    expect(getBasePath()).toBe('/mustask');
  });

  it('withBase prefixes a panel path', () => {
    vi.stubEnv('BASE_URL', '/mustask/');
    expect(withBase('/organizations/1')).toBe('/mustask/organizations/1');
  });

  it('stripBase removes the prefix, the bare base becomes "/"', () => {
    vi.stubEnv('BASE_URL', '/mustask/');
    expect(stripBase('/mustask/signed-out')).toBe('/signed-out');
    expect(stripBase('/mustask')).toBe('/');
    expect(stripBase('/mustask/')).toBe('/');
  });

  it('leaves a path outside the base and a lookalike alone', () => {
    vi.stubEnv('BASE_URL', '/mustask/');
    expect(stripBase('/other/x')).toBe('/other/x');
    expect(stripBase('/mustaskx/y')).toBe('/mustaskx/y');
  });
});
