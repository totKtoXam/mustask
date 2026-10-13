import { readFileSync } from 'node:fs';
// Lists, per slow file, the tests that are >= 5 s and have <= 3 expects: candidates to fold
// or move down (docs/features/tech-029-vitest-full-run-workers/candidates.md). A list only.
//   node scripts/heavy-candidates.mjs <vitest-json-report>
const root = new URL('..', import.meta.url);
const rep = JSON.parse(readFileSync(process.argv[2], 'utf8'));
const slow = JSON.parse(readFileSync(new URL('src/test/slowFiles.json', root), 'utf8')).files;
const out = [];
for (const rel of slow) {
  const file = rep.testResults.find((f) => f.name.split(String.fromCharCode(92)).join('/').endsWith(rel));
  if (!file) continue;
  const src = readFileSync(new URL(rel, root), 'utf8');
  // chunk the source at every `it(` / `it.each(` at the start of a line (after indentation)
  const parts = src.split(/\n(?=\s*it(?:\.each)?\()/);
  const chunks = parts.slice(1).map((p) => {
    const title = /^\s*it(?:\.each\([^)]*\))?\(\s*(['"`])([\s\S]*?)\1/.exec(p);
    return { title: title ? title[2] : '', expects: (p.match(/\bexpect\(/g) || []).length, steps: (p.match(/await user\.|userEvent\./g) || []).length };
  });
  const rows = file.assertionResults.map((a) => {
    const c = chunks.find((x) => x.title && (a.title.startsWith(x.title.slice(0, 25)) || x.title.startsWith(a.title.slice(0, 25))));
    return { s: (a.duration || 0) / 1000, title: a.title, expects: c ? c.expects : null, steps: c ? c.steps : null };
  });
  out.push({ rel, total: rows.reduce((a, b) => a + b.s, 0), n: rows.length, rows });
}
out.sort((a, b) => b.total - a.total);
const lines = [];
for (const f of out) {
  lines.push(`### ${f.rel} — ${f.total.toFixed(0)} s, ${f.n} tests`);
  const cand = f.rows.filter((r) => r.s >= 5 && r.expects !== null && r.expects <= 3).sort((a, b) => b.s - a.s);
  lines.push(cand.length ? 'Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:' : 'No thin-and-heavy tests found.');
  for (const r of cand.slice(0, 8)) lines.push(`- ${r.s.toFixed(1)} s, ${r.expects} expect, ${r.steps} user steps — ${r.title.slice(0, 110)}`);
  lines.push('');
}
console.log(lines.join('\n'));
