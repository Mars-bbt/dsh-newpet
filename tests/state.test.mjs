import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { decodeSession, readTaskState } from '../lib/state.js';
import { resourcePath } from '../lib/index.js';

test('versioned cache rows with null values do not imply work or failure', () => {
  const document = { record: { rows: { openStep: { ver: 1, seq: 9, val: null },
    llmRetry: { ver: 1, seq: 9, val: null }, failure: { ver: 1, val: {} },
    lastStepBoundary: { val: { kind: 'end' } }, pendingCalls: { val: {} } } } };
  const result = decodeSession(document, 1000, 6000);
  assert.equal(result.busy, false); assert.equal(result.failure, false);
});
test('long running open steps remain active, including tool names and user task', () => {
  const document = { record: { rows: { openStep: { val: { turn: 2, step: 4 } },
    pendingCalls: { val: { call_one: { name: 'read_file' }, call_two: { name: 'search' } } },
    turnOutline: { val: [{ prompt: 'Old task' }, { prompt: 'Build my tool' }] } } } };
  const result = decodeSession(document, 1000, 11000);
  assert.equal(result.busy, true); assert.equal(result.failure, false);
  assert.equal(result.step, '2.4'); assert.equal(result.task, 'Build my tool');
  assert.equal(result.tools, 'read_file、search');
});
test('stale caches cannot keep a pet working forever', () => {
  assert.equal(decodeSession({ openStep: { step: 1 }, failure: { message: 'error' } }, 0, 200000).busy, false);
  assert.equal(decodeSession({ failure: { message: 'error' } }, 0, 200000).failure, false);
});
test('resource routing rejects traversal and absolute paths', () => {
  assert.equal(resourcePath('../LICENSE'), null); assert.equal(resourcePath('../../secret'), null);
  assert.equal(resourcePath('C:\\Windows\\win.ini'), null);
  assert.ok(resourcePath('generated/idle.png').endsWith(path.join('generated', 'idle.png')));
});
test('task reader tolerates a partially written newest cache', () => {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'mars-pet-state-'));
  const folder = path.join(home, 'storages/session_projcache/sessions'); fs.mkdirSync(folder, { recursive: true });
  try {
    const valid = path.join(folder, 'session-valid.json');
    fs.writeFileSync(valid, JSON.stringify({ prompt: 'Ready', openStep: { step: 3 } }));
    fs.utimesSync(valid, new Date(1000), new Date(1000));
    fs.writeFileSync(path.join(folder, 'session-incomplete.json'), '{');
    const result = readTaskState(home, 5000); assert.equal(result.task, 'Ready'); assert.equal(result.session, 'valid');
  } finally {
    if (path.dirname(path.resolve(home)) !== path.resolve(os.tmpdir()) || !path.basename(home).startsWith('mars-pet-state-'))
      throw new Error('Refusing to delete a path outside the test temporary directory');
    fs.rmSync(home, { recursive: true });
  }
});
