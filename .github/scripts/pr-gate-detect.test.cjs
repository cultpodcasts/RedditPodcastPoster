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
  const walk = async (a) => { classify = a.classify; return { ok: true, anchor: PRIOR, hops: [{ paths: ['docs/a.md'] }, { paths: [] }] }; };
  const github = githubWith({ [PRIOR]: [checkRun()] });
  const r = await d.decide({ ...base, github, walk });
  assert.equal(r.skip, true);
  assert.equal(r.skipCoverage, true);
  assert.match(r.reason, /merge-from-base only \(2 commits, non-build files only\) above bbbbbbb with a green gate/);
  assert.equal(await classify(PRIOR), 'stop');
  assert.equal(await classify(HEAD), null);
});

test('decide: a merge-only tip whose chain never reaches a green gate runs the full build', async () => {
  const walk = async () => ({ ok: false, reason: 'no anchor within 10 merge-from-base commits', hops: [] });
  const r = await d.decide({ ...base, github: githubWith({}), walk });
  assert.equal(r.skip, false);
  assert.equal(r.skipCoverage, false);
  assert.match(r.reason, /no anchor/);
});

test('decide: a feature commit, rebase, evil merge or overlap runs the full build with coverage', async () => {
  for (const reason of ['not a two-parent merge commit', 'merge changes differ from the base side', 'PR and base changed the same files']) {
    const walk = async () => ({ ok: false, reason, hops: [] });
    const r = await d.decide({ ...base, github: githubWith({}), walk });
    assert.equal(r.skip, false);
    assert.equal(r.skipCoverage, false);
    assert.equal(r.reason, reason);
  }
});

test('isNonBuildPath: docs, markdown, .cursor and workflow scripts are non-build; code, workflows and contracts are not', () => {
  for (const p of ['docs/deployment.md', 'docs/adr/0001.txt', 'README.md', 'Cloud/Api/architecture.md', 'AGENTS.md', '.cursor/rules/x.mdc',
    '.github/scripts/review-gate.cjs', '.github/ISSUE_TEMPLATE/bug.yml']) {
    assert.equal(d.isNonBuildPath(p), true, p);
  }
  for (const p of ['Cloud/Api/Program.cs', 'RedditPodcastPoster.slnx', 'Directory.Packages.props', 'scripts/assert-slnf.ps1',
    '.github/workflows/dotnet.yml', '.github/workflows/pr-gate.yml', 'docs/contracts/streaming-submit.json',
    'docs/contracts/notes.md', 'contracts/package.json', 'Infrastructure/main.bicep', '', undefined, null]) {
    assert.equal(d.isNonBuildPath(p), false, String(p));
  }
});

test('buildAffectingPaths: lists only non-allowlisted paths, and a hop without paths is unknown (fail closed)', () => {
  assert.deepEqual(d.buildAffectingPaths([]), []);
  assert.deepEqual(d.buildAffectingPaths([{ paths: ['docs/a.md', 'src/b.cs'] }, { paths: ['src/b.cs', 'a.csproj'] }]), ['a.csproj', 'src/b.cs']);
  assert.deepEqual(d.buildAffectingPaths([{ paths: [] }, {}]), ['<unknown paths>']);
});

test('decide: a merge-only tip above a green gate whose merged-in main touches code runs build+test but skips coverage', async () => {
  const walk = async () => ({ ok: true, anchor: PRIOR, hops: [{ paths: ['docs/a.md', 'Cloud/Api/Foo.cs'] }] });
  const r = await d.decide({ ...base, github: githubWith({}), walk });
  assert.equal(r.skip, false);
  assert.equal(r.skipCoverage, true);
  assert.match(r.reason, /build-affecting files \(Cloud\/Api\/Foo\.cs\).*coverage skipped/);
});

test('decide: any hop in the chain touching a build file runs build+test, skips coverage; a long list is summarised', async () => {
  const walk = async () => ({ ok: true, anchor: PRIOR, hops: [{ paths: ['docs/a.md'] }, { paths: ['a.cs', 'b.cs', 'c.cs', 'd.cs'] }] });
  const r = await d.decide({ ...base, github: githubWith({}), walk });
  assert.equal(r.skip, false);
  assert.equal(r.skipCoverage, true);
  assert.match(r.reason, /\(a\.cs, b\.cs, c\.cs and 1 more\).*coverage skipped/);
});

test('decide: an empty merge (no files) above a green gate still skips', async () => {
  const walk = async () => ({ ok: true, anchor: PRIOR, hops: [{ paths: [] }] });
  const r = await d.decide({ ...base, github: githubWith({}), walk });
  assert.equal(r.skip, true);
  assert.equal(r.skipCoverage, true);
});
test('decide: a failing, cancelled or missing prior gate (real classify) runs the full build with coverage', async () => {
  // Walk that asks classify about the prior commit, as the real walker does after one hop.
  const walk = async (a) => ((await a.classify(PRIOR)) === 'stop'
    ? { ok: true, anchor: PRIOR, hops: [{ paths: ['README.md'] }] }
    : { ok: false, reason: 'no anchor within 10 merge-from-base commits', hops: [] });
  for (const runs of [[], [checkRun({ conclusion: 'failure' })], [checkRun({ conclusion: 'cancelled' })]]) {
    const r = await d.decide({ ...base, github: githubWith({ [PRIOR]: runs }), walk });
    assert.equal(r.skip, false);
    assert.equal(r.skipCoverage, false);
  }
  const green = await d.decide({ ...base, github: githubWith({ [PRIOR]: [checkRun()] }), walk });
  assert.equal(green.skip, true);
  assert.equal(green.skipCoverage, true);
});

test('decide: an API error from the checks lookup fails closed (full build with coverage)', async () => {
  const walk = async (a) => ((await a.classify(PRIOR)) === 'stop' ? { ok: true, anchor: PRIOR, hops: [{ paths: [] }] } : { ok: false, reason: 'x', hops: [] });
  const r = await d.decide({ ...base, github: githubWith({}, true), walk });
  assert.equal(r.skip, false);
  assert.equal(r.skipCoverage, false);
  assert.match(r.reason, /checks 500/);
});

test('decide: a walk that throws fails closed (full build with coverage)', async () => {
  const r = await d.decide({ ...base, github: githubWith({}), walk: async () => { throw new Error('boom'); } });
  assert.equal(r.skip, false);
  assert.equal(r.skipCoverage, false);
  assert.match(r.reason, /boom/);
});

test('decide: a walk that reports an API error fails closed (full build with coverage)', async () => {
  const walk = async () => ({ ok: false, apiError: true, reason: 'API error while checking merge-only tip: 502', hops: [] });
  const r = await d.decide({ ...base, github: githubWith({}), walk });
  assert.equal(r.skip, false);
  assert.equal(r.skipCoverage, false);
});

test('decide: unknown head or base runs the full build with coverage', async () => {
  const walk = async () => { throw new Error('walk must not run'); };
  const noHead = await d.decide({ ...base, headSha: undefined, github: githubWith({}), walk });
  const noBase = await d.decide({ ...base, baseRef: undefined, github: githubWith({}), walk });
  assert.equal(noHead.skip, false);
  assert.equal(noHead.skipCoverage, false);
  assert.equal(noBase.skip, false);
  assert.equal(noBase.skipCoverage, false);
});

function fakeCore() {
  const outputs = {};
  return { outputs, setOutput: (k, v) => { outputs[k] = v; }, info() {} };
}
const prCtx = () => ({ repo: { owner: 'o', repo: 'r' }, payload: { pull_request: { base: { ref: 'main' }, head: { sha: HEAD } } } });

test('run: publishes skip_heavy=true and a reason for a green-gate merge-only tip', async () => {
  const core = fakeCore();
  let seen;
  const walk = async (a) => { seen = a; return { ok: true, anchor: PRIOR, hops: [{ paths: ['README.md'] }] }; };
  const r = await d.run({ github: githubWith({}), context: prCtx(), core, walk });
  assert.equal(r.skip, true);
  assert.equal(r.skipCoverage, true);
  assert.equal(core.outputs.skip_heavy, 'true');
  assert.equal(core.outputs.skip_coverage, 'true');
  assert.match(core.outputs.reason, /merge-from-base only \(1 commit, non-build files only\)/);
  assert.equal(seen.headSha, HEAD);
  assert.equal(seen.baseRef, 'main');
});

test('run: publishes skip_heavy=false and skip_coverage=true when merge-only tip absorbed build-affecting main', async () => {
  const core = fakeCore();
  const walk = async () => ({ ok: true, anchor: PRIOR, hops: [{ paths: ['Cloud/Api/Foo.cs'] }] });
  const r = await d.run({ github: githubWith({}), context: prCtx(), core, walk });
  assert.equal(r.skip, false);
  assert.equal(r.skipCoverage, true);
  assert.equal(core.outputs.skip_heavy, 'false');
  assert.equal(core.outputs.skip_coverage, 'true');
});

test('run: publishes skip_heavy=false and skip_coverage=false when the tip is not merge-only', async () => {
  const core = fakeCore();
  const walk = async () => ({ ok: false, reason: 'not a two-parent merge commit', hops: [] });
  await d.run({ github: githubWith({}), context: prCtx(), core, walk });
  assert.equal(core.outputs.skip_heavy, 'false');
  assert.equal(core.outputs.skip_coverage, 'false');
});

test('run: a non pull_request event publishes skip_heavy=false and skip_coverage=false', async () => {
  const core = fakeCore();
  await d.run({ github: githubWith({}), context: { repo: { owner: 'o', repo: 'r' }, payload: {} }, core });
  assert.equal(core.outputs.skip_heavy, 'false');
  assert.equal(core.outputs.skip_coverage, 'false');
});

// Static guards on the workflow wiring so the fail-closed contract can't regress.
const workflow = fs.readFileSync(path.join(__dirname, '..', 'workflows', 'pr-gate.yml'), 'utf8').replace(/\r\n/g, '\n');
const code = workflow.split('\n').filter((l) => !/^\s*#/.test(l)).join('\n');

test('workflow: build needs detect and is skipped only when skip_heavy is exactly true', () => {
  assert.match(code, /^  build:\s*\n    needs: detect\s*\n    if: .*needs\.detect\.outputs\.skip_heavy != 'true'/m);
  // `!cancelled()` is what lets a FAILED detect still run the full build (fail closed).
  assert.match(code, /^  build:\s*\n    needs: detect\s*\n    if: \$\{\{ !cancelled\(\) && needs\.detect\.outputs\.skip_heavy != 'true' \}\}/m);
});

test('workflow: build passes skip_coverage from detect into the reusable Build workflow', () => {
  assert.match(code, /skip_heavy: \$\{\{ steps\.detect\.outputs\.skip_heavy \}\}/);
  assert.match(code, /skip_coverage: \$\{\{ steps\.detect\.outputs\.skip_coverage \}\}/);
  assert.match(
    code,
    /uses: \.\/\.github\/workflows\/dotnet\.yml\s*\n\s*with:\s*\n\s*skip_coverage: \$\{\{ needs\.detect\.outputs\.skip_coverage == 'true' \}\}/,
  );
});

test('workflow: gate waits for detect and accepts a skipped build only when skip_heavy=true', () => {
  assert.match(code, /needs: \[ detect, build, actionlint, bicep \]/);
  assert.match(code, /SKIP_HEAVY: \$\{\{ needs\.detect\.outputs\.skip_heavy \}\}/);
  assert.match(code, /DETECT_RESULT: \$\{\{ needs\.detect\.result \}\}/);
  // The verdict logic is unit tested in pr-gate-verdict.test.cjs; the workflow must call it, not re-implement it.
  assert.match(code, /run: node \.github\/scripts\/pr-gate-verdict\.cjs/);
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
