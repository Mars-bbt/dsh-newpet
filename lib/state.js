import fs from 'node:fs';
import path from 'node:path';

function unwrap(row) {
  if (row && typeof row === 'object') {
    for (const key of ['val', 'value', 'data']) if (Object.hasOwn(row, key)) return row[key];
  }
  return row;
}
function lastField(value, name) {
  let result;
  function visit(item) {
    if (!item || typeof item !== 'object') return;
    for (const [key, child] of Object.entries(item)) {
      if (key === name && child != null) result = child;
      if (child && typeof child === 'object') visit(child);
    }
  }
  visit(value);
  return result;
}
const nonempty = value => value && typeof value === 'object' && Object.keys(value).length > 0;
export function decodeSession(document, timestamp, now = Date.now()) {
  const rows = document.record?.rows ?? document;
  const field = name => unwrap(Object.hasOwn(rows, name) ? rows[name] : lastField(document, name));
  const open = field('openStep'), boundary = field('lastStepBoundary'), calls = field('pendingCalls');
  const error = field('failure'), retry = field('llmRetry');
  const fresh = now - timestamp < 120000;
  const busy = fresh && (nonempty(open) || boundary?.kind === 'start' || now - timestamp < 2200);
  const names = Object.values(calls ?? {}).map(call => call?.name ?? call?.toolName).filter(Boolean).slice(0, 3);
  return {
    busy: Boolean(busy), failure: Boolean(fresh && (nonempty(error) || nonempty(retry))),
    task: String(lastField(document, 'prompt') ?? '').slice(0, 500),
    draft: String(lastField(document, 'draft') ?? '').slice(-160),
    tools: names.length ? names.join('、') : nonempty(calls) ? '工具调用中' : '',
    step: open?.step == null ? '' : `${open.turn ?? ''}.${open.step}`,
  };
}
export function readTaskState(home, now = Date.now()) {
  const directory = path.join(home, 'storages', 'session_projcache', 'sessions');
  let entries;
  try {
    entries = fs.readdirSync(directory).filter(name => name.endsWith('.json')).map(name => {
      const file = path.join(directory, name); return { file, time: fs.statSync(file).mtimeMs };
    }).sort((a, b) => b.time - a.time);
  } catch { return { busy: false, failure: false, task: '', draft: '', tools: '', step: '', session: '' }; }
  for (const entry of entries.slice(0, 5)) {
    try {
      const content = fs.readFileSync(entry.file, 'utf8');
      const state = decodeSession(JSON.parse(content), entry.time, now);
      return { ...state, session: path.basename(entry.file, '.json').replace(/^session-/, '') };
    } catch { /* An atomically replaced cache may be temporarily unreadable. */ }
  }
  return { busy: false, failure: false, task: '', draft: '', tools: '', step: '', session: '' };
}
