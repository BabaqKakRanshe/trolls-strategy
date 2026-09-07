import { describe, expect, it } from 'vitest';

import terrainPreview from '../../src/content/forgottenMemoriesPreview.json';

describe('Forgotten Memories terrain preview', () => {
  it('covers the current colony grid with 32 px source tiles', () => {
    expect(terrainPreview.tileSize).toBe(32);
    expect(terrainPreview.layout).toHaveLength(14);
    expect(terrainPreview.layout.every((row) => row.length === 14)).toBe(true);
    const knownTiles = new Set(Object.keys(terrainPreview.palette));
    expect([...new Set(terrainPreview.layout.join(''))].every((tile) => knownTiles.has(tile))).toBe(true);
  });

  it('describes a varied closed forest outside the playable grid', () => {
    expect(Object.keys(terrainPreview.forest.frames)).toHaveLength(5);
    expect(terrainPreview.forest.columns).toHaveLength(6);
    expect(terrainPreview.forest.rows * terrainPreview.forest.columns.length).toBeGreaterThanOrEqual(60);
    expect(terrainPreview.lockedTerrain.columns.every((column) => column < 0 || column >= 14)).toBe(true);
    expect(terrainPreview.lockedTerrain.baseFrame).toBeTypeOf('number');
    expect(terrainPreview.lockedTerrain.detailFrames.length).toBeGreaterThan(1);
    expect(terrainPreview.props.placements).toHaveLength(10);
  });
});
