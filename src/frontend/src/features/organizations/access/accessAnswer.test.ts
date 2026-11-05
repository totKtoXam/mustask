import { describe, expect, it } from 'vitest';
import { gateRow, type AccessAnswer, type GateRow } from './accessAnswer';

const member: AccessAnswer = { kind: 'member', role: 'member', permissions: ['a.read'] };
const notMember: AccessAnswer = { kind: 'not-member' };

// mod-016 design § 2.1: five rows, exactly one of them for every state of the tab.
describe('gateRow — the state order of an organization page', () => {
  it.each<[string, Parameters<typeof gateRow>[0], boolean, GateRow]>([
    [
      'nothing held, a read running (the first read, or «Повторить»)',
      { answer: undefined, fetching: true, failed: false },
      true,
      'loading',
    ],
    [
      'nothing held, a read running after a failed one',
      { answer: undefined, fetching: true, failed: true },
      true,
      'loading',
    ],
    [
      'nothing held, nothing running, nothing tried yet',
      { answer: undefined, fetching: false, failed: false },
      true,
      'loading',
    ],
    [
      'nothing held, nothing running, the last read failed',
      { answer: undefined, fetching: false, failed: true },
      true,
      'failed',
    ],
    ['a 404 held', { answer: notMember, fetching: false, failed: false }, true, 'notFound'],
    [
      'a 404 held, a re-read running',
      { answer: notMember, fetching: true, failed: false },
      true,
      'notFound',
    ],
    [
      'a 404 held, a re-read failed',
      { answer: notMember, fetching: false, failed: true },
      true,
      'notFound',
    ],
    [
      'codes held, the code missing',
      { answer: member, fetching: false, failed: false },
      false,
      'denied',
    ],
    [
      'codes held, the code held',
      { answer: member, fetching: false, failed: false },
      true,
      'content',
    ],
    [
      'codes held, a re-read running, the code held',
      { answer: member, fetching: true, failed: false },
      true,
      'content',
    ],
    [
      'codes held, a re-read failed, the code missing',
      { answer: member, fetching: false, failed: true },
      false,
      'denied',
    ],
  ])('%s is row %#', (_name, input, holds, row) => {
    expect(gateRow(input, holds)).toBe(row);
  });

  it('never reads «denied» without codes: unknown is not denied', () => {
    for (const fetching of [true, false]) {
      for (const failed of [true, false]) {
        for (const holds of [true, false]) {
          expect(gateRow({ answer: undefined, fetching, failed }, holds)).not.toBe('denied');
          expect(gateRow({ answer: undefined, fetching, failed }, holds)).not.toBe('content');
        }
      }
    }
  });

  it('gives the same row to a held 404 whatever runs or fails after it', () => {
    for (const fetching of [true, false]) {
      for (const failed of [true, false]) {
        for (const holds of [true, false]) {
          expect(gateRow({ answer: notMember, fetching, failed }, holds)).toBe('notFound');
        }
      }
    }
  });
});
