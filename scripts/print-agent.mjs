#!/usr/bin/env node
// Yurt print agent (Phase 6, own_pos). Runs on a computer in the cafe, next to the printers. It asks the server for
// print jobs of its branch and sends each one to a printer on the cafe's own network, which a server in the cloud
// cannot reach. Needs Node.js 18 or newer and nothing else.
//
//   YURT_API_URL=https://api.mycafe.kz YURT_PRINT_AGENT_KEY=<key from the Printers page> node print-agent.mjs
//
// or: node print-agent.mjs --api https://api.mycafe.kz --key <key>
import net from 'node:net';
import { pathToFileURL } from 'node:url';

export const POLL_MS = 3000;
const ERROR_WAIT_MS = 15000;
const SEND_TIMEOUT_MS = 5000;

/** The key starts with the cafe's short name and a dot; the server needs that name in the X-Tenant header. */
export function cafeOf(key) {
  const dot = key.indexOf('.');
  return dot > 0 ? key.slice(0, dot) : '';
}

/** Sends raw bytes to a network printer (port 9100 printing) and resolves once they are written. */
export function sendToPrinter(host, port, bytes, timeoutMs = SEND_TIMEOUT_MS) {
  return new Promise((resolve, reject) => {
    const socket = net.createConnection({ host, port });
    socket.setTimeout(timeoutMs, () => socket.destroy(new Error('The printer did not answer in time.')));
    socket.once('error', reject);
    socket.once('connect', () => socket.end(bytes));
    socket.once('close', (hadError) => {
      if (!hadError) resolve();
    });
  });
}

/** One round: fetch the waiting jobs, print each, report each. Returns how many printed. */
export async function runOnce({ api, key, fetchImpl = fetch, send = sendToPrinter, log = console }) {
  const base = api.replace(/\/+$/, '') + '/api/v1/print-agent';
  const headers = { 'Content-Type': 'application/json', 'X-Print-Agent-Key': key, 'X-Tenant': cafeOf(key) };
  const res = await fetchImpl(`${base}/jobs/claim`, { method: 'POST', headers, body: '{}' });
  if (res.status === 401) throw new Error('The server does not know this key. Make a new key on the Printers page.');
  if (!res.ok) throw new Error(`The server answered ${res.status}.`);
  const jobs = await res.json();

  let printed = 0;
  for (const job of jobs) {
    let result;
    try {
      await send(job.host, job.port, Buffer.from(job.data, 'base64'));
      result = { ok: true };
      printed++;
    } catch (e) {
      result = { ok: false, error: String(e?.message ?? e).slice(0, 300) };
      log.warn(`Could not print to ${job.host}:${job.port}: ${result.error}`);
    }
    await fetchImpl(`${base}/jobs/${job.id}/result`, { method: 'POST', headers, body: JSON.stringify(result) });
  }
  return printed;
}

function option(name, env) {
  const i = process.argv.indexOf(`--${name}`);
  return (i > 0 ? process.argv[i + 1] : undefined) ?? process.env[env] ?? '';
}

async function main() {
  const api = option('api', 'YURT_API_URL');
  const key = option('key', 'YURT_PRINT_AGENT_KEY');
  if (!api || !key) {
    console.error('Set YURT_API_URL and YURT_PRINT_AGENT_KEY (or pass --api and --key).');
    process.exit(1);
  }
  console.log(`Yurt print agent started for ${api}. Press Ctrl+C to stop.`);
  for (;;) {
    let wait = POLL_MS;
    try {
      const printed = await runOnce({ api, key });
      if (printed) console.log(`${new Date().toLocaleTimeString()} printed ${printed}`);
    } catch (e) {
      console.error(`${new Date().toLocaleTimeString()} ${e?.message ?? e}`);
      wait = ERROR_WAIT_MS;
    }
    await new Promise((r) => setTimeout(r, wait));
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
