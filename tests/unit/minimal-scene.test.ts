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
  it('registers the colony scene in a 16:9 viewport without white letterboxing', () => {
    const config = createGameConfig('game-canvas');

    expect(config.backgroundColor).toBe('#102f31');
    expect(config.width).toBe(1280);
    expect(config.height).toBe(720);
    expect(config.scene).toEqual([MinimalScene]);
  });

  it('aligns the red market, blue warehouse, and barracks to grid cells', () => {
    expect(MINIMAL_SCENE_LAYOUT).toEqual([
      { kind: 'market', cell: { x: 9, y: 10 }, color: 0xff0000 },
      { kind: 'warehouse', cell: { x: 29, y: 10 }, color: 0x0000ff },
      { kind: 'barracks', cell: { x: 19, y: 18 }, color: 0xff9900 },
    ]);
  });

  it('loads only the approved background and building images', () => {
    const source = readFileSync(
      new URL('../../src/game/scenes/MinimalScene.ts', import.meta.url),
      'utf8',
    );

    expect(source.match(/this\.load\.image/g)).toHaveLength(3);
    expect(source.match(/this\.load\.spritesheet/g)).toHaveLength(6);
    expect(source).not.toContain("'/assets/runtime/world/background.png'");
    expect(source).toContain("'/assets/runtime/buildings/market.png'");
    expect(source).toContain("'/assets/runtime/buildings/mine.png'");
    expect(source).toContain("'/assets/runtime/buildings/warehouse.png'");
    expect(source).toContain("'goblin.idle'");
    expect(source).toContain("'goblin.walk'");
    expect(source).toContain("'goblin.attack'");
    expect(source).toContain("'goblin.jump'");
    expect(source).toContain("'goblin.damage'");
    expect(source).toContain("'goblin.death'");
    expect(source).not.toMatch(/\.text\(|this\.sound|\.audio\(/i);
    expect(source).toContain('.circle(');
    expect(source).not.toContain('lineBetween(');
    expect(source).toContain('isBuildingPlacementValid');
  });
});
