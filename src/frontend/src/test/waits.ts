/**
 * An explicit findBy/waitFor budget, scaled like the default one (tech-073): the
 * CI runner runs the heavy page tests about ten times slower than a developer
 * box, so a budget tuned here is tripled there, as `asyncUtilTimeout` is in
 * src/test/setup.ts. Use it for a wait that needs more than the default;
 * a wait that only needs the default passes no timeout at all.
 */
export function waitBudget(ms: number): number {
  return process.env.CI ? ms * 3 : ms;
}
