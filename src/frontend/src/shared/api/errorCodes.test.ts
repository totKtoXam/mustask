import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { organizationErrorCodes } from './errorCodes';

/** The repository's error catalog of the organizations module (ADR-0006). */
const ORGANIZATIONS_CATALOG = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../../../../contracts/errors/organizations.json',
);

interface ErrorCatalog {
  module: string;
  codes: Record<string, { statuses: number[] }>;
}

function readCatalog(): ErrorCatalog {
  return JSON.parse(readFileSync(ORGANIZATIONS_CATALOG, 'utf8')) as ErrorCatalog;
}

describe('mod-001 US-2 Scenario: The not-found code is registered with its texts (frontend half)', () => {
  it('holds every organizations constant as a code of contracts/errors/organizations.json', () => {
    const catalog = readCatalog();

    expect(catalog.module).toBe('organizations');
    for (const code of Object.values(organizationErrorCodes)) {
      expect(Object.keys(catalog.codes)).toContain(code);
    }
  });

  it('names the not-found answer exactly as api-contract § 1.3 does, with status 404', () => {
    const catalog = readCatalog();

    expect(organizationErrorCodes.notFound).toBe('organizations.organization.not_found');
    expect(catalog.codes[organizationErrorCodes.notFound]?.statuses).toEqual([404]);
  });
});
