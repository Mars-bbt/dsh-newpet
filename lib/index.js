import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import { readTaskState } from './state.js';

export const name = 'dsh-newpet';
const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const desktop = path.join(root, 'desktop-pet');
const program = path.join(desktop, 'WhaleOverlay.exe');
const home = process.env.DSH_HOME || path.join(os.homedir(), '.dsh');
const preferences = process.env.MARS_PET_PREFS || path.join(process.env.APPDATA || os.homedir(), 'MarsNewPet');
const namesFile = path.join(preferences, 'names.json');

export function resourcePath(relative) {
  if (!relative || path.isAbsolute(relative)) return null;
  const folder = path.join(root, 'assets');
  const target = path.resolve(folder, relative);
  return target.startsWith(folder + path.sep) ? target : null;
}
function readNames() {
  try { return JSON.parse(fs.readFileSync(namesFile, 'utf8')); }
  catch { return { title: '主人', selfName: '鲸鱼娘' }; }
}
function updateNames(url) {
  const result = readNames();
  for (const [query, key] of [['title', 'title'], ['self', 'selfName']]) {
    const next = url.searchParams.get(query)?.trim();
    if (next) result[key] = next.slice(0, 24);
  }
  fs.mkdirSync(preferences, { recursive: true });
  fs.writeFileSync(namesFile, JSON.stringify(result), 'utf8');
  return result;
}
async function balance() {
  let text;
  try { text = fs.readFileSync(path.join(home, '.credentials.yaml'), 'utf8'); }
  catch { return { ok: false, error: '未找到 DeepSeek API Key' }; }
  const key = text.match(/DEEPSEEK_API_KEY:\s*["']?([A-Za-z0-9_-]+)/)?.[1];
  if (!key) return { ok: false, error: '未配置 DeepSeek API Key' };
  const response = await fetch('https://api.deepseek.com/user/balance', {
    headers: { authorization: `Bearer ${key}` }, signal: AbortSignal.timeout(15000),
  });
  if (!response.ok) return { ok: false, error: `余额接口返回 ${response.status}` };
  const body = await response.json();
  const item = body.balance_infos?.[0] ?? {};
  return { ok: true, total: item.total_balance, granted: item.granted_balance,
    topped: item.topped_up_balance, currency: item.currency, available: body.is_available };
}
export async function apply(ctx) {
  let child;
  let stopped = false;
  const alive = () => Boolean(child && child.exitCode === null && !child.killed);
  function start() {
    if (alive() || !fs.existsSync(program)) return;
    child = spawn(program, [], { cwd: desktop, stdio: 'ignore', detached: true,
      env: { ...process.env, MARS_PET_DSH_HOME: home, MARS_PET_PREFS: preferences }, windowsHide: false });
    child.on('error', error => ctx.logger?.warn?.(`桌宠启动失败：${error.message}`));
    child.unref();
  }
  function stop() { if (alive()) child.kill(); child = undefined; }
  const status = () => ({ running: alive(), available: fs.existsSync(program) });
  start();
  ctx.effect(() => () => { stopped = true; stop(); }, 'Mars 桌宠进程清理');
  ctx.inject(['webServer'], context => {
    const server = context.get('webServer');
    const register = (route, handler) => {
      const dispose = server.register({ kind: 'exact', path: `/api/dsh-newpet/${route}`, handler: async (req, res) => {
        const url = new URL(req.url, 'http://localhost');
        try {
          const value = await handler(url, res);
          if (res.writableEnded) return;
          res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
          res.end(JSON.stringify(value));
        } catch (error) {
          res.writeHead(500, { 'Content-Type': 'application/json; charset=utf-8' });
          res.end(JSON.stringify({ ok: false, error: error.message }));
        }
      }});
      context.effect(() => dispose, `Mars 桌宠接口 ${route}`);
    };
    register('desktop-pet', url => {
      if (url.searchParams.get('on') === '0') stop();
      if (url.searchParams.get('on') === '1' && !stopped) start();
      return status();
    });
    register('names', url => url.searchParams.has('title') || url.searchParams.has('self') ? updateNames(url) : readNames());
    register('state', () => readTaskState(home));
    register('balance', () => balance());
    register('assets', (url, res) => {
      const file = resourcePath(url.searchParams.get('f'));
      if (!file || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
        res.writeHead(404); res.end('Not found'); return;
      }
      const types = { '.png': 'image/png', '.webp': 'image/webp', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json' };
      res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
      res.end(fs.readFileSync(file));
    });
  });
}
