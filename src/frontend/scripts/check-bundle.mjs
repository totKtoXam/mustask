// The bundle check of feat-018 (ADR-0039 decision 10, plan-frontend § 10.2).
//
//   npm run build && npm run bundle:check
//
// Reads `dist/bundle-report.json` (written by `vite.config.ts`) against
// `bundle-budget.json` and fails when
//   - an editor-library module is reachable from the entry chunk through static
//     imports (the editor must stay a lazy chunk), or
//   - the entry chunk grew over its ceiling since the recorded baseline, or
//   - the editor chunk or the emoji chunk is over its gzipped ceiling.
//
// The ceilings are the human's to confirm or change (ADR-0039 decision 10);
// this script only holds the build to them.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const budget = JSON.parse(readFileSync(path.join(root, 'bundle-budget.json'), 'utf8'));
const report = JSON.parse(readFileSync(path.join(root, 'dist', 'bundle-report.json'), 'utf8'));

const chunks = new Map(report.chunks.map((chunk) => [chunk.fileName, chunk]));
const entry = report.chunks.find((chunk) => chunk.isEntry);
const editor = report.chunks.find((chunk) =>
    chunk.modules.some((module) =>
        module.id.replace(/\\/g, '/').includes('/RichTextEditor/EditorImpl.'),
    ),
);
const emoji = report.chunks.find((chunk) =>
    chunk.modules.some((module) => module.id.includes('virtual:rich-text-emoji')),
);

const failures = [];
const kb = (bytes) => `${(bytes / 1024).toFixed(1)} KB`;

if (!entry) failures.push('No entry chunk in the report.');
if (!editor)
    failures.push(
        'No editor chunk (RichTextEditor/EditorImpl) in the report: is the editor wired?',
    );

// The entry's static closure: the entry and everything it imports eagerly.
const closure = new Set();
const pending = entry ? [entry.fileName] : [];
while (pending.length > 0) {
    const name = pending.pop();
    if (closure.has(name)) continue;
    closure.add(name);
    for (const next of chunks.get(name)?.imports ?? []) pending.push(next);
}

for (const name of closure) {
    for (const module of chunks.get(name)?.modules ?? []) {
        const id = module.id.replace(/\\/g, '/');
        const hit = budget.forbiddenInEntryClosure.find((pattern) => id.includes(pattern));
        if (hit) failures.push(`${name} (entry closure) holds ${id} — matches "${hit}".`);
    }
}

if (entry) {
    const growth = entry.gzipBytes - budget.baseline.entryGzipBytes;
    console.log(
        `entry   ${entry.fileName}: ${kb(entry.gzipBytes)} gzip, ${growth >= 0 ? '+' : ''}${kb(growth)} over the baseline ` +
            `(ceiling +${kb(budget.ceilings.entryGrowthGzipBytes)})`,
    );
    if (growth > budget.ceilings.entryGrowthGzipBytes) {
        failures.push(
            `The entry chunk grew by ${growth} bytes gzipped; the ceiling is ${budget.ceilings.entryGrowthGzipBytes}.`,
        );
    }
}
if (editor) {
    console.log(
        `editor  ${editor.fileName}: ${kb(editor.gzipBytes)} gzip (ceiling ${kb(budget.ceilings.editorChunkGzipBytes)})`,
    );
    if (editor.gzipBytes > budget.ceilings.editorChunkGzipBytes) {
        failures.push(
            `The editor chunk is ${editor.gzipBytes} bytes gzipped; the ceiling is ${budget.ceilings.editorChunkGzipBytes}.`,
        );
    }
}
if (emoji) {
    console.log(
        `emoji   ${emoji.fileName}: ${kb(emoji.gzipBytes)} gzip (ceiling ${kb(budget.ceilings.emojiChunkGzipBytes)})`,
    );
    if (emoji.gzipBytes > budget.ceilings.emojiChunkGzipBytes) {
        failures.push(
            `The emoji chunk is ${emoji.gzipBytes} bytes gzipped; the ceiling is ${budget.ceilings.emojiChunkGzipBytes}.`,
        );
    }
}

if (failures.length > 0) {
    console.error(`\nbundle:check failed\n- ${failures.join('\n- ')}`);
    process.exit(1);
}
console.log('bundle:check passed');
