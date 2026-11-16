#!/usr/bin/env node
// TEMPORARY (tech-074 investigation, removed before review): sums the self time
// of every .cpuprofile in a folder by function and by package, prints the top.
import fs from 'node:fs';
import path from 'node:path';

const dir = process.argv[2];
const byFn = new Map();
const byPkg = new Map();
let total = 0;
for (const name of fs.readdirSync(dir).filter((n) => n.endsWith('.cpuprofile'))) {
  const prof = JSON.parse(fs.readFileSync(path.join(dir, name), 'utf8'));
  const nodes = new Map(prof.nodes.map((n) => [n.id, n]));
  const self = new Map();
  for (let i = 0; i < prof.samples.length; i++) {
    const dt = prof.timeDeltas[i] ?? 0;
    self.set(prof.samples[i], (self.get(prof.samples[i]) ?? 0) + dt);
  }
  for (const [id, us] of self) {
    const n = nodes.get(id);
    const cf = n.callFrame;
    const url = cf.url || '(native)';
    const m = url.match(/node_modules\/((?:@[^/]+\/)?[^/]+)/);
    const pkg = m ? m[1] : url.includes('/src/') ? 'app:' + url.split('/src/')[1].split('/').slice(0, 2).join('/') : url.slice(-40);
    const key = `${cf.functionName || '(anon)'} ${pkg}:${cf.lineNumber}`;
    byFn.set(key, (byFn.get(key) ?? 0) + us);
    byPkg.set(pkg, (byPkg.get(pkg) ?? 0) + us);
    total += us;
  }
}
const top = (m, k) => [...m].sort((a, b) => b[1] - a[1]).slice(0, k);
console.log(`total sampled: ${(total / 1e6).toFixed(1)} s`);
console.log('--- by package/file ---');
for (const [k, v] of top(byPkg, 30)) console.log(`${(v / 1e6).toFixed(1).padStart(7)} s ${((100 * v) / total).toFixed(1).padStart(5)}%  ${k}`);
console.log('--- by function ---');
for (const [k, v] of top(byFn, 50)) console.log(`${(v / 1e6).toFixed(1).padStart(7)} s ${((100 * v) / total).toFixed(1).padStart(5)}%  ${k}`);
