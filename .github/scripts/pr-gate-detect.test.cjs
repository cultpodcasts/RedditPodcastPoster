'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const d = require('./pr-gate-detect.cjs');

const HEAD = 'a'.repeat(40);
const PRIOR = 'b'.repeat(40);

const checkRun = (over = {}) => ({ name: 'gate', status: 'completed', conclusion: 'success', app: { slug: 'github-actions' }, ...over });
const githubWith = (runsBySha, fail) => ({
  rest: {
    checks: {
      listForRef: async ({ ref, check_name }) => {
        if (fail) throw new Error('checks 500');
        assert.equal(check_name, 'gate');
        return { data: { check_runs: runsBySha[ref] || [] } };
      },
    },
  },
});

test('hasGreenGate: a completed successful gate check-run from GitHub Actions is green', async () => {
  const github = githubWith({ [PRIOR]: [checkRun()] });
  assert.equal(await d.hasGreenGate({ github, owner: 'o', repo: 'r', sha: PRIOR }), true);
});

test('hasGreenGate: failed, cancelled, skipped, running, foreign-app or differently named runs are not green', async () => {
  for (const over of [
    { conclusion: 'failure' }, { conclusion: 'cancelled' }, { conclusion: 'skipped' },
    { conclusion: null, status: 'in_progress' }, { app: { slug: 'some-other-app' } }, { app: null }, { name: 'build' },
  ]) {
    const github = githubWith({ [PRIOR]: [checkRun(over)] });
    assert.equal(await d.hasGreenGate({ github, owner: 'o', repo: 'r', sha: PRIOR }), false, JSON.stringify(over));
  }
  assert.equal(await d.hasGreenGate({ github: githubWith({}), owner: 'o', repo: 'r', sha: PRIOR }), false);
});

const base = { owner: 'o', repo: 'r', baseRef: 'main', headSha: HEAD };

test('decide: merge-only tip above a green gate skips the heavy build', async () => {
  let classify;
  const walk = async (a) => { classify = a.classify; return { ok: true, anchor: PRIOR, hops: [{}, {}] }; };
  const github = githubWith({ [PRIOR]: [checkRun()] });
  const r = await d.decide({ ...base, github, walk });
  assert.equal(r.skip, true);
  assert.match(r.reason, /merge-from-base only \(2 commits\) above bbbbbbb with a green gate/);
  assert.equal(await classify(PRIOR), 'stop');
  assert.equal(await classify(HEAD), null);
});

test('decide: a merge-only tip whose chain never reaches a green gate runs the full build', async () => {
  const walk = async () => ({ ok: false, reason: 'no anchor within 10 merge-from-base commits', hops: [] });
  const r = await d.decide({ ...base, github: githubWith({}), walk });
  assert.equal(r.skip, false);
  assert.match(r.reason, /no anchor/);
});

test('decide: a feature commit, rebase, evil merge or overlap runs the full build', async () => {
  for (const reason of ['not a two-parent merge commit', 'merge changes differ from the base side', 'PR and base changed the same files']) {
    const walk = async () => ({ ok: false, reason, hops: [] });
    const r = await d.decide({ ...base, github: githubWith({}), walk });
    assert.equal(r.skip, false);
    assert.equal(r.reason, reason);
  }
});

test('decide: a failing, cancelled or missing prior gate (real classify) runs the full build', async () => {
  // Walk that asks classify about the prior commit, as the real walker does after one hop.
  const walk = async (a) => ((await a.classify(PRIOR)) === 'stop'
    ? { ok: true, anchor: PRIOR, hops: [{}] }
    : { ok: false, reason: 'no anchor within 10 merge-from-base commits', hops: [] });
  for (const runs of [[], [checkRun({ conclusion: 'failure' })], [checkRun({ conclusion: 'cancelled' })]]) {
    const r = await d.decide({ ...base, github: githubWith({ [PRIOR]: runs }), walk });
    assert.equal(r.skip, false);
  }
  const green = await d.decide({ ...base, github: githubWith({ [PRIOR]: [checkRun()] }), walk });
  assert.equal(green.skip, true);
});

test('decide: an API error from the checks lookup fails closed (full build)', async () => {
  const walk = async (a) => ((await a.classify(PRIOR)) === 'stop' ? { ok: true, anchor: PRIOR, hops: [] } : { ok: false, reason: 'x', hops: [] });
  const r = await d.decide({ ...base, github: githubWith({}, true), walk });
  assert.equal(r.skip, false);
  assert.match(r.reason, /checks 500/);
});

test('decide: a walk that throws fails closed (full build)', async () => {
  const r = await d.decide({ ...base, github: githubWith({}), walk: async () => { throw new Error('boom'); } });
  assert.equal(r.skip, false);
  assert.match(r.reason, /boom/);
});

test('decide: a walk that reports an API error fails closed (full build)', async () => {
  const walk = async () => ({ ok: false, apiError: true, reason: 'API error while checking merge-only tip: 502', hops: [] });
  const r = await d.decide({ ...base, github: githubWith({}), walk });
  assert.equal(r.skip, false);
});

test('decide: unknown head or base runs the full build', async () => {
  const walk = async () => { throw new Error('walk must not run'); };
  assert.equal((await d.decide({ ...base, headSha: undefined, github: githubWith({}), walk })).skip, false);
  assert.equal((await d.decide({ ...base, baseRef: undefined, github: githubWith({}), walk })).skip, false);
});

function fakeCore() {
  const outputs = {};
  return { outputs, setOutput: (k, v) => { outputs[k] = v; }, info() {} };
}
const prCtx = () => ({ repo: { owner: 'o', repo: 'r' }, payload: { pull_request: { base: { ref: 'main' }, head: { sha: HEAD } } } });

test('run: publishes skip_heavy=true and a reason for a green-gate merge-only tip', async () => {
  const core = fakeCore();
  let seen;
  const walk = async (a) => { seen = a; return { ok: true, anchor: PRIOR, hops: [{}] }; };
  const r = await d.run({ github: githubWith({}), context: prCtx(), core, walk });
  assert.equal(r.skip, true);
  assert.equal(core.outputs.skip_heavy, 'true');
  assert.match(core.outputs.reason, /merge-from-base only \(1 commit\)/);
  assert.equal(seen.headSha, HEAD);
  assert.equal(seen.baseRef, 'main');
});

test('run: publishes skip_heavy=false when the tip is not merge-only', async () => {
  const core = fakeCore();
  const walk = async () => ({ ok: false, reason: 'not a two-parent merge commit', hops: [] });
  await d.run({ github: githubWith({}), context: prCtx(), core, walk });
  assert.equal(core.outputs.skip_heavy, 'false');
});

test('run: a non pull_request event publishes skip_heavy=false', async () => {
  const core = fakeCore();
  await d.run({ github: githubWith({}), context: { repo: { owner: 'o', repo: 'r' }, payload: {} }, core });
  assert.equal(core.outputs.skip_heavy, 'false');
});

// Static guards on the workflow wiring so the fail-closed contract can't regress.
const workflow = fs.readFileSync(path.join(__dirname, '..', 'workflows', 'pr-gate.yml'), 'utf8').replace(/\r\n/g, '\n');
const code = workflow.split('\n').filter((l) => !/^\s*#/.test(l)).join('\n');

test('workflow: build needs detect and is skipped only when skip_heavy is exactly true', () => {
  assert.match(code, /^  build:\s*\n    needs: detect\s*\n    if: .*needs\.detect\.outputs\.skip_heavy != 'true'/m);
});

test('workflow: gate waits for detect and accepts a skipped build only when skip_heavy=true', () => {
  assert.match(code, /needs: \[ detect, build, actionlint, bicep \]/);
  assert.match(code, /SKIP_HEAVY: \$\{\{ needs\.detect\.outputs\.skip_heavy \}\}/);
  assert.match(code, /\$SKIP_HEAVY" = "true"/);
});

test('workflow: actionlint and bicep never depend on detect, so they run on every tip', () => {
  const section = (name) => code.slice(code.indexOf(`\n  ${name}:`)).split(/\n  [a-z]+:\n/)[0];
  assert.doesNotMatch(section('actionlint'), /needs:/);
  assert.doesNotMatch(section('bicep'), /needs:/);
});

test('workflow: detect has read-only checks permission and SHA-pinned actions', () => {
  assert.match(code, /^  detect:[\s\S]*?permissions:\s*\n      contents: read\s*\n      checks: read/m);
  const uses = [...code.matchAll(/^\s*(?:-\s+)?uses:\s*(\S+)/gm)].map((m) => m[1]).filter((u) => !u.startsWith('./'));
  assert.ok(uses.length >= 3);
  for (const u of uses) assert.match(u, /@[0-9a-f]{40}$/, u);
});
