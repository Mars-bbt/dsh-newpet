// Local QA host. The simulated task never leaves this machine or queries account balances.
import http from 'node:http';
import { apply } from '../lib/index.js';
let handlers = new Map(), disposals = [];
const origin = Number(process.env.MARS_PET_QA_PORT || 19408);
const start = Date.now();
const service = { register(route) {
  handlers.set(route.path, route.handler); return () => handlers.delete(route.path);
} };
const context = {
  logger: { info: console.log, warn: console.warn },
  effect(factory) { const dispose = factory(); if (typeof dispose === 'function') disposals.push(dispose); },
  get(name) { return name === 'webServer' ? service : undefined; },
  inject(names, callback) { callback(context); },
};
const server = http.createServer((req, res) => {
  const route = new URL(req.url, 'http://localhost').pathname;
  if (route === '/api/dsh-newpet/state') {
    const age = (Date.now() - start) / 1000;
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ busy: age < 16, failure: false, task: '检查新桌宠',
      draft: age < 8 ? '正在准备素材与设置面板' : '', tools: age >= 8 && age < 16 ? '本地检查' : '', step: '1.2', session: 'qa' })); return;
  }
  if (route === '/api/dsh-newpet/balance') {
    res.writeHead(200, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ ok: true, total: '12.34', currency: 'CNY' })); return;
  }
  const handler = handlers.get(route);
  if (!handler) { res.writeHead(404); res.end(); return; }
  handler(req, res);
});
await new Promise(resolve => server.listen(origin, '127.0.0.1', resolve));
await apply(context);
console.log(`QA preview ready at http://127.0.0.1:${origin}`);
function close() { for (const dispose of disposals.reverse()) dispose(); server.close(() => process.exit(0)); }
process.on('SIGINT', close); process.on('SIGTERM', close);
