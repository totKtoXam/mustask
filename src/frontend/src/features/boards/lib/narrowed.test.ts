import { describe, expect, it } from 'vitest';
import { isNarrowed, narrowedTag } from './narrowed';
import { projectScope, teamScope } from './scope';

const TEAMS = [
  { id: 'mars', name: 'Платформа' },
  { id: 'log', name: 'Аналитика' },
];

describe('feat-043 US-4: a narrowed backlog', () => {
  it('is a project’s backlog under a chosen team, and nothing else', () => {
    expect(isNarrowed(projectScope('p'), ['mars'])).toBe(true);
    expect(isNarrowed(projectScope('p'), [])).toBe(false);
    expect(isNarrowed(teamScope('t'), ['p'])).toBe(false);
  });

  it('names the chosen teams, «без команды» for none, and drops an unknown one', () => {
    expect(narrowedTag(['mars'], TEAMS)).toBe('сужено: команда Платформа');
    expect(narrowedTag(['MARS', 'none'], TEAMS)).toBe('сужено: команда Платформа, без команды');
    expect(narrowedTag(['gone'], TEAMS)).toBe('сужено');
    expect(narrowedTag(['none'], TEAMS)).toBe('сужено: без команды');
  });
});
