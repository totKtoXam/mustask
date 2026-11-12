import { describe, expect, it } from 'vitest';
import { objectAccessCheck } from './objectAccess';

const organization = (granted: string[]) => (code: string) => granted.includes(code);

describe('objectAccessCheck (BR-SA-18)', () => {
  it('asks an organization-scope code of the organization answer', () => {
    const check = objectAccessCheck(organization(['organizations.area.create']), []);
    expect(check('organizations.area.create', 'organization')).toBe(true);
    expect(check('organizations.area.read', 'organization')).toBe(false);
  });

  it('asks a narrower code of the object, never of the organization answer', () => {
    const check = objectAccessCheck(organization(['organizations.project.write']), [
      'organizations.project.grant',
    ]);
    expect(check('organizations.project.write', 'organization')).toBe(false);
    expect(check('organizations.project.grant', 'organization')).toBe(true);
  });

  it('fails closed while the object is unread', () => {
    const check = objectAccessCheck(organization(['workitems.workitem.write']), undefined);
    expect(check('workitems.workitem.write', 'organization')).toBe(false);
  });

  it('leaves an instance code to the token', () => {
    const check = objectAccessCheck((code) => code === 'dictionaries.languages.read', null);
    expect(check('dictionaries.languages.read', 'instance')).toBe(true);
  });
});
