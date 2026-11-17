'use strict';
/**
 * Reads Terraform outputs from an HCP Terraform workspace's current state and exports them to the
 * GitHub Actions job environment (RPP #1460, SP-2.5).
 *
 * Used by deploy.yml (provision) to hand the Auth0 `search-index-writer` M2M credentials, which the
 * Api repo's Auth0 Terraform stack keeps only in HCP state, to functions.bicepparam with no hand copy.
 *
 * - Sensitive values are only returned by the single-output endpoint
 *   (GET /state-version-outputs/:id), so each sensitive output is fetched on its own.
 * - Every sensitive value is masked (`::add-mask::`) before it is written to $GITHUB_ENV, and no value
 *   is ever printed.
 * - No token: a warning, nothing exported, exit 0 (bicep falls back to '' and the D1 sync targets stay
 *   unconfigured). A token that cannot read the outputs is an error: a half-configured deploy is worse
 *   than a failed one.
 *
 * CLI:
 *   node hcp-terraform-outputs.cjs --org cultpodcasts --workspace cultpodcasts-auth0 \
 *     --output client_id=SEARCH_INDEX_WRITER_CLIENT_ID --output client_secret=SEARCH_INDEX_WRITER_CLIENT_SECRET
 * Env: TF_TOKEN (HCP Terraform API token), GITHUB_ENV (set by Actions).
 */
const fs = require('node:fs');

const API = 'https://app.terraform.io/api/v2';
const ENV_NAME = /^[A-Z][A-Z0-9_]*$/;

function parseArgs(argv) {
  const args = { outputs: [] };
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    const value = argv[i + 1];
    if (flag === '--org') { args.org = value; i++; }
    else if (flag === '--workspace') { args.workspace = value; i++; }
    else if (flag === '--output') {
      const [name, env] = String(value ?? '').split('=');
      if (!name || !env || !ENV_NAME.test(env)) {
        throw new Error(`--output must be <terraform output>=<ENV_NAME>, got '${value}'`);
      }
      args.outputs.push({ name, env });
      i++;
    } else {
      throw new Error(`Unknown argument '${flag}'`);
    }
  }
  if (!args.org || !args.workspace || args.outputs.length === 0) {
    throw new Error('Usage: --org <org> --workspace <workspace> --output <name>=<ENV_NAME> [...]');
  }
  return args;
}

async function getJson(fetchImpl, token, url) {
  const response = await fetchImpl(url, {
    headers: { Authorization: `Bearer ${token}`, Accept: 'application/vnd.api+json' },
  });
  if (!response.ok) {
    // The body can name the workspace but never holds a secret; still keep it out of the log.
    throw new Error(`HCP Terraform ${new URL(url).pathname} answered ${response.status}`);
  }
  return response.json();
}

/**
 * Returns [{ name, env, value, sensitive }] for the requested outputs. Throws when the workspace,
 * its current state or any requested output is missing, or a value is not a single-line string.
 */
async function readOutputs({ org, workspace, outputs, token, fetchImpl = fetch }) {
  const ws = await getJson(fetchImpl, token,
    `${API}/organizations/${encodeURIComponent(org)}/workspaces/${encodeURIComponent(workspace)}`);
  const list = await getJson(fetchImpl, token,
    `${API}/workspaces/${encodeURIComponent(ws.data.id)}/current-state-version-outputs`);
  const byName = new Map((list.data ?? []).map((o) => [o.attributes.name, o]));

  const result = [];
  for (const wanted of outputs) {
    const item = byName.get(wanted.name);
    if (!item) {
      throw new Error(`Workspace ${workspace} has no output '${wanted.name}' (has the stack been applied?)`);
    }
    let value = item.attributes.value;
    const sensitive = item.attributes.sensitive === true;
    if (sensitive) {
      const single = await getJson(fetchImpl, token, `${API}/state-version-outputs/${encodeURIComponent(item.id)}`);
      value = single.data.attributes.value;
    }
    if (typeof value !== 'string' || value.length === 0 || /[\r\n]/.test(value)) {
      throw new Error(`Output '${wanted.name}' of ${workspace} is not a non-empty single-line string`);
    }
    result.push({ name: wanted.name, env: wanted.env, value, sensitive });
  }
  return result;
}

/** Lines for $GITHUB_ENV, and the workflow commands that must be printed first (masks). */
function exportLines(values) {
  return {
    masks: values.filter((v) => v.sensitive).map((v) => `::add-mask::${v.value}`),
    env: values.map((v) => `${v.env}=${v.value}`),
  };
}

async function main(argv, env, { fetchImpl = fetch, log = console.log, write = fs.appendFileSync } = {}) {
  const args = parseArgs(argv);
  if (!env.TF_TOKEN) {
    log(`::warning::TF_TOKEN is not set: ${args.outputs.map((o) => o.env).join(', ')} not exported from ${args.workspace}. ` +
      'The D1 search sync targets stay unconfigured. Add the RPP repo secret TF_TOKEN_APP_TERRAFORM_IO (docs/search-platform/sync-and-replication.md).');
    return 0;
  }
  if (!env.GITHUB_ENV) {
    throw new Error('GITHUB_ENV is not set (run inside GitHub Actions)');
  }
  const values = await readOutputs({ org: args.org, workspace: args.workspace, outputs: args.outputs, token: env.TF_TOKEN, fetchImpl });
  const { masks, env: lines } = exportLines(values);
  for (const mask of masks) log(mask);
  write(env.GITHUB_ENV, lines.map((l) => `${l}\n`).join(''));
  log(`Exported ${values.map((v) => v.env).join(', ')} from ${args.org}/${args.workspace}.`);
  return 0;
}

module.exports = { parseArgs, readOutputs, exportLines, main };

if (require.main === module) {
  main(process.argv.slice(2), process.env).then(
    (code) => process.exit(code),
    (error) => { console.log(`::error::${error.message}`); process.exit(1); },
  );
}
