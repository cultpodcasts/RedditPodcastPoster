'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const v = require('./pr-gate-verdict.cjs');

const needs = (over = {}) => {
  const base = { detect: 'success', build: 'success', actionlint: 'success', bicep: 'success' };
  return Object.fromEntries(Object.entries({ ...base, ...over }).filter(([, r]) => r !== undefined).map(([k, r]) => [k, { result: r }]));
};
const failed = (n, skipHeavy, detectResult) => v.failedJobs({ needs: n, skipHeavy, detectResult: detectResult ?? n.detect?.result });

test('failedJobs: everything green with a full build passes', () => {
  assert.deepEqual(failed(needs(), 'false'), []);
  assert.deepEqual(failed(needs(), undefined), []);
});

test('failedJobs: detect failed and build ran and failed fails on build', () => {
  assert.deepEqual(failed(needs({ detect: 'failure', build: 'failure' }), ''), ['build']);
});

test('failedJobs: detect failed and build skipped (for example a cancellation) fails, even with a stale skip_heavy=true', () => {
  assert.deepEqual(failed(needs({ detect: 'failure', build: 'skipped' }), ''), ['build']);
  assert.deepEqual(failed(needs({ detect: 'failure', build: 'skipped' }), 'true'), ['build']);
  assert.deepEqual(failed(needs({ detect: 'cancelled', build: 'skipped' }), 'true'), ['build']);
});

test('failedJobs: detect failed but the full build then succeeded passes (detect is not a failure source)', () => {
  assert.deepEqual(failed(needs({ detect: 'failure', build: 'success' }), ''), []);
});

test('failedJobs: skip_heavy=true, detect success, build skipped passes', () => {
  assert.deepEqual(failed(needs({ build: 'skipped' }), 'true'), []);
});

test('failedJobs: empty, unset or false skip_heavy with a skipped build fails', () => {
  for (const s of ['', undefined, 'false', 'TRUE', 'yes']) {
    assert.deepEqual(failed(needs({ build: 'skipped' }), s), ['build'], String(s));
  }
});

test('failedJobs: skip_heavy=true but actionlint or bicep failed, skipped or cancelled fails', () => {
  assert.deepEqual(failed(needs({ build: 'skipped', actionlint: 'failure' }), 'true'), ['actionlint']);
  assert.deepEqual(failed(needs({ build: 'skipped', bicep: 'failure' }), 'true'), ['bicep']);
  assert.deepEqual(failed(needs({ build: 'skipped', actionlint: 'failure', bicep: 'cancelled' }), 'true'), ['actionlint', 'bicep']);
  assert.deepEqual(failed(needs({ build: 'skipped', actionlint: 'skipped' }), 'true'), ['actionlint']);
});

test('failedJobs: only build may be skipped; a skipped actionlint or bicep fails a full build run', () => {
  assert.deepEqual(failed(needs({ bicep: 'skipped' }), 'false'), ['bicep']);
});

test('failedJobs: a failed or cancelled build fails even when skip_heavy=true', () => {
  assert.deepEqual(failed(needs({ build: 'failure' }), 'true'), ['build']);
  assert.deepEqual(failed(needs({ build: 'cancelled' }), 'true'), ['build']);
});

test('failedJobs: a required job missing from needs fails the gate', () => {
  assert.deepEqual(failed(needs({ build: undefined }), 'true'), ['build']);
  assert.deepEqual(failed(needs({ bicep: undefined }), 'false'), ['bicep']);
  assert.deepEqual(v.failedJobs({ needs: {}, skipHeavy: 'true', detectResult: 'success' }), ['detect', 'build', 'actionlint', 'bicep']);
  assert.deepEqual(v.failedJobs({ needs: null, skipHeavy: 'true', detectResult: 'success' }).length, 4);
});

test('parseNeeds: invalid JSON, arrays and scalars are rejected', () => {
  assert.equal(v.parseNeeds('not json'), null);
  assert.equal(v.parseNeeds('[]'), null);
  assert.equal(v.parseNeeds('null'), null);
  assert.equal(v.parseNeeds(undefined), null);
  assert.deepEqual(v.parseNeeds('{"a":{"result":"success"}}'), { a: { result: 'success' } });
});

const run = (n, env = {}) => {
  const lines = [];
  const code = v.main({ RESULTS: typeof n === 'string' ? n : JSON.stringify(n), ...env }, { log: (l) => lines.push(l) });
  return { code, lines };
};

test('main: exit code 0 for a passing skip, 1 with an error annotation naming the failed jobs', () => {
  const ok = run(needs({ build: 'skipped' }), { SKIP_HEAVY: 'true', DETECT_RESULT: 'success', DETECT_REASON: 'why' });
  assert.equal(ok.code, 0);
  assert.ok(ok.lines.includes('detect: skip_heavy=true (why)'));
  const bad = run(needs({ build: 'skipped' }), { SKIP_HEAVY: '', DETECT_RESULT: 'success' });
  assert.equal(bad.code, 1);
  assert.ok(bad.lines.includes('::error::PR gate failed: build'));
  assert.ok(bad.lines.includes('detect: skip_heavy=<unset> (no reason)'));
});

test('main: an unreadable needs context fails the gate', () => {
  const r = run('garbage', { SKIP_HEAVY: 'true', DETECT_RESULT: 'success' });
  assert.equal(r.code, 1);
  assert.match(r.lines[0], /could not read the needs context/);
});
