import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const poses = ['idle', 'work', 'thinking', 'success', 'failure', 'celebrate', 'sleep', 'interact'];
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
for (const pose of poses) {
  const source = path.resolve('assets/generated', pose + '.png');
  assert.ok(fs.existsSync(source), `Missing ${pose} artwork`);
}
assert.equal(fs.readdirSync('assets/generated').length, poses.length, 'Only final poses should ship');
assert.ok(fs.readFileSync('LICENSE', 'utf8').includes('Copyright (c) 2026 Mars'));
assert.equal(fs.existsSync('assets/dsh-whale-moe.js'), false);
assert.equal(fs.existsSync('assets/whale-moe-core.js'), false);
assert.equal(fs.existsSync('assets/dsh-whale-moe.css'), false);
const oldDirectory = path.resolve('../dsh-newpet');
if (fs.existsSync(path.join(oldDirectory, 'assets/generated'))) {
  const oldArtwork = fs.readdirSync(path.join(oldDirectory, 'assets/generated')).map(file => hash(path.join(oldDirectory, 'assets/generated', file)));
  for (const pose of poses) assert.equal(oldArtwork.includes(hash(path.join('assets/generated', pose + '.png'))), false, `Copied old artwork: ${pose}`);
}
console.log('PASS: 8 new poses, Mars license, no old art or presenter files.');
