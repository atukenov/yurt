import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { cafeOf, runOnce, sendToPrinter } from './print-agent.mjs';

/** A pretend printer on a free local port that collects what each connection sent. */
async function fakePrinter() {
  const received = [];
  const server = net.createServer((socket) => {
    const chunks = [];
    socket.on('data', (c) => chunks.push(c));
    socket.on('end', () => received.push(Buffer.concat(chunks)));
  });
  await new Promise((r) => server.listen(0, '127.0.0.1', r));
  return { port: server.address().port, received, close: () => new Promise((r) => server.close(r)) };
}

test('the cafe name is the part of the key before the first dot', () => {
  assert.equal(cafeOf('default.abc.def'), 'default');
  assert.equal(cafeOf('nodot'), '');
});

test('sends bytes to the printer, and fails clearly when nothing listens', async () => {
  const printer = await fakePrinter();
  await sendToPrinter('127.0.0.1', printer.port, Buffer.from([0x1b, 0x40, 65]));
  await new Promise((r) => setTimeout(r, 50));
  assert.deepEqual([...printer.received[0]], [0x1b, 0x40, 65]);
  await printer.close();
  await assert.rejects(sendToPrinter('127.0.0.1', printer.port, Buffer.from('x')));
});

test('one round prints each job and reports how it went', async () => {
  const printer = await fakePrinter();
  const calls = [];
  const fetchImpl = async (url, init) => {
    calls.push({ url, init });
    if (url.endsWith('/jobs/claim'))
      return new Response(JSON.stringify([
        { id: 'j1', host: '127.0.0.1', port: printer.port, data: Buffer.from('hello').toString('base64') },
        { id: 'j2', host: '127.0.0.1', port: 1, data: Buffer.from('lost').toString('base64') },
      ]), { status: 200 });
    return new Response('true', { status: 200 });
  };
  const printed = await runOnce({ api: 'https://api.test/', key: 'brew.secret', fetchImpl, log: { warn() {} } });
  await new Promise((r) => setTimeout(r, 50));
  await printer.close();

  assert.equal(printed, 1);
  assert.equal(printer.received[0].toString(), 'hello');
  assert.equal(calls[0].url, 'https://api.test/api/v1/print-agent/jobs/claim');
  assert.equal(calls[0].init.headers['X-Print-Agent-Key'], 'brew.secret');
  assert.equal(calls[0].init.headers['X-Tenant'], 'brew');
  assert.deepEqual(JSON.parse(calls[1].init.body), { ok: true });
  assert.ok(calls[2].url.endsWith('/jobs/j2/result'));
  assert.equal(JSON.parse(calls[2].init.body).ok, false);
});

test('an unknown key stops with a clear message', async () => {
  const fetchImpl = async () => new Response('', { status: 401 });
  await assert.rejects(runOnce({ api: 'https://api.test', key: 'x.y', fetchImpl }), /Printers page/);
});
