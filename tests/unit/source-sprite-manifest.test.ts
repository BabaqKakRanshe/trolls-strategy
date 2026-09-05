import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

interface AssetEntry {
  key: string;
  type: string;
  source: string;
  output: string;
  licenseId: string;
  frameWidth?: number;
  frameHeight?: number;
  frameDurationMs?: number;
}

describe('source sprite manifest', () => {
  const root = resolve(import.meta.dirname, '../..');
  const manifest = JSON.parse(readFileSync(resolve(root, 'assets/curated-assets.json'), 'utf8')) as {
    assets: AssetEntry[];
  };
  const licenses = JSON.parse(readFileSync(resolve(root, 'assets/licenses.json'), 'utf8')) as {
    licenses: Array<{ id: string }>;
  };

  it('curates three buildings and six goblin animation sheets', () => {
    expect(manifest.assets.map((entry) => entry.key)).toEqual([
      'building.market', 'building.mine', 'building.warehouse',
      'unit.goblin.idle', 'unit.goblin.walk', 'unit.goblin.attack',
      'unit.goblin.jump', 'unit.goblin.damage', 'unit.goblin.death',
    ]);
    expect(new Set(manifest.assets.map((entry) => entry.key)).size).toBe(9);
  });

  it('keeps sources and outputs inside their intended roots with license records', () => {
    const licenseIds = new Set(licenses.licenses.map(({ id }) => id));
    for (const entry of manifest.assets) {
      expect(['image', 'spritesheet']).toContain(entry.type);
      expect(entry.source.startsWith('assets/sprites/')).toBe(true);
      expect(entry.output.startsWith('public/assets/runtime/')).toBe(true);
      expect(entry.source).not.toContain('..');
      expect(entry.output).not.toContain('..');
      expect(existsSync(resolve(root, entry.source))).toBe(true);
      expect(licenseIds.has(entry.licenseId)).toBe(true);
      if (entry.type === 'spritesheet') {
        expect(entry.frameWidth).toBe(32);
        expect(entry.frameHeight).toBe(32);
        expect([100, 200]).toContain(entry.frameDurationMs);
      }
    }
  });
});
