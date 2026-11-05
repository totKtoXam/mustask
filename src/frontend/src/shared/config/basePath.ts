// The path base the panel is served under (ADR-0037, tech-057). Vite's `base`
// comes from the build-time VITE_BASE_PATH and is exposed as
// `import.meta.env.BASE_URL` ("/" at the root, "/mustask/" under a prefix).
// All base handling lives here; nothing else splits or glues path strings.

/** The base without a trailing slash: "" at the root, "/mustask" under a prefix. Read per call, so tests can stub it. */
export function getBasePath(): string {
  const raw = (import.meta.env.BASE_URL as string | undefined) ?? '/';
  return raw.replace(/\/+$/, '');
}

/** An address inside the panel ("/home?x=1") → the address in the browser ("/mustask/home?x=1"). */
export function withBase(path: string): string {
  return `${getBasePath()}${path}`;
}

/** The browser's pathname → the path inside the panel; a path outside the base is returned as is. */
export function stripBase(pathname: string): string {
  const base = getBasePath();
  if (base === '') return pathname;
  if (pathname === base) return '/';
  return pathname.startsWith(`${base}/`) ? pathname.slice(base.length) : pathname;
}
