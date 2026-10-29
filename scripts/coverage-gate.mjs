#!/usr/bin/env node
// Coverage floors (tech-046): compares line coverage with
// scripts/coverage-floors.json, prints a table, exits 1 when a floor is not met.
//
// Usage:
//   node scripts/coverage-gate.mjs backend <dir>
//     <dir> holds one folder per test project (MustaskCoverageDir in
//     Directory.Build.props), each with coverlet's coverage.cobertura.xml.
//   node scripts/coverage-gate.mjs frontend <coverage-summary.json>
//     Vitest's json-summary report of the whole suite.
//
// Backend lines are merged by file and line number across the reports, so a
// line any unit-test project hits counts once. The integration figure comes
// from *.IntegrationTests alone: the unit floors cannot hide behind it.
import fs from 'node:fs';
import path from 'node:path';

const [mode, input] = process.argv.slice(2);
const floors = JSON.parse(
  fs.readFileSync(new URL('./coverage-floors.json', import.meta.url), 'utf8'),
);

const pct = (covered, total) => (total === 0 ? 100 : (100 * covered) / total);
let failed = false;
const row = (name, value, floor) => {
  const ok = value >= floor;
  if (!ok) failed = true;
  console.log(
    `  ${ok ? 'ok  ' : 'FAIL'} ${name.padEnd(26)} ${value.toFixed(1).padStart(5)}%  (floor ${floor}%)`,
  );
};

function findReports(dir) {
  const found = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) found.push(...findReports(full));
    else if (entry.name === 'coverage.cobertura.xml') found.push(full);
  }
  return found;
}

// assembly -> Map("file:line" -> hit)
function mergeLines(reports) {
  const assemblies = new Map();
  for (const report of reports) {
    const xml = fs.readFileSync(report, 'utf8');
    for (const pkg of xml.matchAll(/<package name="([^"]+)"[\s\S]*?<\/package>/g)) {
      const lines = assemblies.get(pkg[1]) ?? new Map();
      assemblies.set(pkg[1], lines);
      for (const cls of pkg[0].matchAll(/<class [^>]*filename="([^"]+)"[\s\S]*?<\/class>/g)) {
        // Class-level <lines> only: the <methods> block repeats the same lines.
        const body = cls[0].slice(cls[0].lastIndexOf('</methods>') + 1);
        for (const line of body.matchAll(/<line number="(\d+)" hits="(\d+)"/g)) {
          const key = `${cls[1]}:${line[1]}`;
          lines.set(key, lines.get(key) === true || Number(line[2]) > 0);
        }
      }
    }
  }
  return assemblies;
}

function count(lines) {
  let covered = 0;
  for (const hit of lines.values()) if (hit) covered++;
  return { covered, total: lines.size };
}

if (mode === 'backend' && input) {
  const projects = fs.readdirSync(input, { withFileTypes: true }).filter((e) => e.isDirectory());
  const unitReports = [];
  const integrationReports = [];
  for (const project of projects) {
    const reports = findReports(path.join(input, project.name));
    (project.name.endsWith('.IntegrationTests') ? integrationReports : unitReports).push(...reports);
  }
  if (unitReports.length === 0) {
    console.error(`coverage-gate: no unit-test coverage reports under ${input}`);
    process.exit(1);
  }

  console.log('Backend unit-test line coverage:');
  const unit = mergeLines(unitReports);
  for (const [assembly, floor] of Object.entries(floors.backend.unit)) {
    const lines = unit.get(assembly);
    if (!lines) {
      console.log(`  FAIL ${assembly.padEnd(26)} no coverage data  (floor ${floor}%)`);
      failed = true;
      continue;
    }
    const { covered, total } = count(lines);
    row(assembly, pct(covered, total), floor);
  }

  if (integrationReports.length === 0) {
    console.error('coverage-gate: no integration-test coverage reports (*.IntegrationTests)');
    process.exit(1);
  }
  console.log('Backend integration-test line coverage:');
  let covered = 0;
  let total = 0;
  for (const lines of mergeLines(integrationReports).values()) {
    const c = count(lines);
    covered += c.covered;
    total += c.total;
  }
  row('all assemblies', pct(covered, total), floors.backend.integration);
} else if (mode === 'frontend' && input) {
  const summary = JSON.parse(fs.readFileSync(input, 'utf8'));
  console.log('Frontend line coverage:');
  row('src', summary.total.lines.pct, floors.frontend);
} else {
  console.error('Usage: coverage-gate.mjs backend <dir> | frontend <coverage-summary.json>');
  process.exit(2);
}

process.exit(failed ? 1 : 0);
