import { readFileSync } from 'node:fs';
import { describe, expect, it, vi } from 'vitest';

vi.mock('phaser', () => ({
  default: {
    WEBGL: 2,
    Scale: { FIT: 3, CENTER_BOTH: 1 },
    Scene: class {},
  },
}));
import { createGameConfig } from '../../src/game/config';
import {
  MINIMAL_SCENE_LAYOUT,
  MinimalScene,
} from '../../src/game/scenes/MinimalScene';

describe('minimal visual runtime', () => {
  it('registers only the geometry-only scene on a white background', () => {
    const config = createGameConfig('game-canvas');

    expect(config.backgroundColor).toBe('#ffffff');
    expect(config.scene).toEqual([MinimalScene]);
  });

  it('defines only a red market and a blue warehouse', () => {
    expect(MINIMAL_SCENE_LAYOUT).toEqual([
      { kind: 'market', x: 420, y: 360, width: 240, height: 180, color: 0xff0000 },
      { kind: 'warehouse', x: 860, y: 360, width: 240, height: 180, color: 0x0000ff },
    ]);
  });

  it('does not load or render sprites, images, text, or audio', () => {
    const source = readFileSync(
      new URL('../../src/game/scenes/MinimalScene.ts', import.meta.url),
      'utf8',
    );

    expect(source).not.toMatch(/\.sprite\(|\.image\(|\.text\(|this\.load|this\.sound|texture/i);
    expect(source).toContain('.circle(');
  });
});
