import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { apply } from '../lib/index.js';

test('host status, static resources and cleanup remain usable', async () => {
  const routes = new Map(), cleanups = [];
  const context = {
    effect(factory) { const dispose = factory(); if (typeof dispose === 'function') cleanups.push(dispose); },
    get() { return { register({ path, handler }) { routes.set(path, handler); return () => routes.delete(path); } }; },
    inject(names, callback) { callback(context); }, logger: { warn() {} },
  };
  await apply(context);
  async function call(route) {
    const response = new EventEmitter(); let body;
    response.writeHead = code => { response.status = code; };
    response.end = value => { response.writableEnded = true; body = value; };
    await routes.get('/api/dsh-newpet/' + route.split('?')[0])({ url: '/api/dsh-newpet/' + route }, response);
    return { status: response.status, body: body.toString() };
  }
  try {
    const status = JSON.parse((await call('desktop-pet?status=1')).body);
    assert.equal(typeof status.available, 'boolean');
    const rejected = await call('assets?f=../LICENSE'); assert.equal(rejected.status, 404);
    const stopped = JSON.parse((await call('desktop-pet?on=0')).body); assert.equal(stopped.running, false);
  } finally { for (const cleanup of cleanups.reverse()) cleanup(); }
  assert.equal(routes.size, 0);
});
