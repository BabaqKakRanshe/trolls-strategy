import Phaser from 'phaser';
import type { GameSnapshot } from '../../application/snapshot';
import type { InteractionViewState } from '../../application/MapInteractionController';
import type { GridCell } from '../../domain/model';
import {
  BARRACKS_CELL,
  BARRACKS_FOOTPRINT,
  BUILDING_FOOTPRINTS,
  GRID_CELL_SIZE,
  GRID_COLUMNS,
  GRID_ROWS,
  MARKET_CELL,
  WAREHOUSE_CELL,
  isBuildingPlacementValid,
} from '../../domain/map/grid';

export const SHOP_RUNTIME_REGISTRY_KEY = 'shop-runtime';

export interface MinimalSceneRuntime {
  getSnapshot(): GameSnapshot;
  getViewState(): InteractionViewState;
  handleCellClick(cell: GridCell): void;
  handleBoxSelection(from: GridCell, to: GridCell): void;
  subscribe(listener: () => void): () => void;
}

export const MINIMAL_SCENE_LAYOUT = [
  { kind: 'market', cell: MARKET_CELL, color: 0xff0000 },
  { kind: 'warehouse', cell: WAREHOUSE_CELL, color: 0x0000ff },
  { kind: 'barracks', cell: BARRACKS_CELL, color: 0xff9900 },
] as const;

const BUILDING_COLORS: Record<string, number> = {
  market: 0xff0000,
  warehouse: 0x0000ff,
  mine: 0x777777,
};

const TEXTURE_KEYS: Record<string, string> = {
  market: 'building.market',
  warehouse: 'building.warehouse',
  mine: 'building.mine',
};

export class MinimalScene extends Phaser.Scene {
  private runtime: MinimalSceneRuntime | null = null;
  private lastRevision = -1;
  private lastSelection = '';
  private readonly dynamicObjects: Phaser.GameObjects.GameObject[] = [];
  private dragStart: { x: number; y: number; cell: GridCell } | null = null;
  private dragGraphics: Phaser.GameObjects.Graphics | null = null;
  private hoverGraphics: Phaser.GameObjects.Graphics | null = null;
  private unsubscribe: (() => void) | null = null;

  public constructor() { super('minimal'); }

  public preload(): void {
    this.load.image('building.market', '/assets/runtime/buildings/market.png');
    this.load.image('building.mine', '/assets/runtime/buildings/mine.png');
    this.load.image('building.warehouse', '/assets/runtime/buildings/warehouse.png');
    this.load.spritesheet('unit.goblin.idle', '/assets/runtime/units/goblin/idle.png', {
      frameWidth: 32, frameHeight: 32,
    });
    this.load.spritesheet('unit.goblin.walk', '/assets/runtime/units/goblin/walk.png', {
      frameWidth: 32, frameHeight: 32,
    });
    this.load.spritesheet('unit.goblin.attack', '/assets/runtime/units/goblin/attack.png', {
      frameWidth: 32, frameHeight: 32,
    });
    this.load.spritesheet('unit.goblin.jump', '/assets/runtime/units/goblin/jump.png', {
      frameWidth: 32, frameHeight: 32,
    });
    this.load.spritesheet('unit.goblin.damage', '/assets/runtime/units/goblin/damage.png', {
      frameWidth: 32, frameHeight: 32,
    });
    this.load.spritesheet('unit.goblin.death', '/assets/runtime/units/goblin/death.png', {
      frameWidth: 32, frameHeight: 32,
    });
  }

  public create(): void {
    this.cameras.main.setBackgroundColor('#102f31');
    this.runtime = this.registry.get(SHOP_RUNTIME_REGISTRY_KEY) as MinimalSceneRuntime | null;
    this.drawTileMap();
    this.createGoblinAnimations();
    this.dragGraphics = this.add.graphics().setDepth(20);
    this.hoverGraphics = this.add.graphics().setDepth(19);
    this.input.on('pointerdown', this.onPointerDown);
    this.input.on('pointermove', this.onPointerMove);
    this.input.on('pointerup', this.onPointerUp);
    this.unsubscribe = this.runtime?.subscribe(() => this.refreshDynamicObjects()) ?? null;
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => this.unsubscribe?.());
    this.refreshDynamicObjects();
  }

  public update(): void {
    const revision = this.runtime?.getSnapshot().revision ?? -1;
    const selection = this.runtime?.getViewState().selectedUnitIds.join('|') ?? '';
    if (revision !== this.lastRevision || selection !== this.lastSelection) this.refreshDynamicObjects();
  }

  private drawTileMap(): void {
    const world = this.add.graphics().setDepth(-10);
    const grass = [0x72b64b, 0x6eae46, 0x78ba50, 0x69a943];
    for (let y = 0; y < GRID_ROWS; y += 1) {
      for (let x = 0; x < GRID_COLUMNS; x += 1) {
        const color = grass[(x * 7 + y * 11) % grass.length]!;
        const px = x * GRID_CELL_SIZE;
        const py = y * GRID_CELL_SIZE;
        world.fillStyle(color, 1).fillRect(px, py, GRID_CELL_SIZE, GRID_CELL_SIZE);
        world.fillStyle(0x397b3c, 0.45).fillRect(px + 7 + (y % 5), py + 9 + (x % 7), 2, 3);
        world.fillStyle(0xa3cf63, 0.4).fillRect(px + 21 - (y % 4), py + 22 - (x % 5), 2, 2);
        if (x < 3 || x >= GRID_COLUMNS - 3 || y < 2 || y >= GRID_ROWS - 2) {
          this.drawForestTile(world, px, py, x, y);
        }
      }
    }
  }

  private drawForestTile(
    graphics: Phaser.GameObjects.Graphics,
    x: number,
    y: number,
    column: number,
    row: number,
  ): void {
    const offset = ((column * 13 + row * 17) % 7) - 3;
    graphics.fillStyle(0x173f35, 0.9).fillRect(x, y, GRID_CELL_SIZE, GRID_CELL_SIZE);
    graphics.fillStyle(0x49372c, 1).fillRect(x + 14 + offset, y + 20, 5, 12);
    graphics.fillStyle(0x0c2f31, 1).fillTriangle(x + 3 + offset, y + 24, x + 29 + offset, y + 24, x + 16 + offset, y + 1);
    graphics.fillStyle(0x164f45, 1).fillTriangle(x + 6 + offset, y + 18, x + 27 + offset, y + 18, x + 16 + offset, y + 2);
    graphics.fillStyle(0x23705a, 0.9).fillTriangle(x + 10 + offset, y + 12, x + 24 + offset, y + 12, x + 17 + offset, y + 2);
  }

  private refreshDynamicObjects(): void {
    const snapshot = this.runtime?.getSnapshot();
    const view = this.runtime?.getViewState();
    if (!snapshot || !view) return;
    this.dynamicObjects.forEach((object) => object.destroy());
    this.dynamicObjects.length = 0;

    for (const building of snapshot.buildings) {
      if (building.mapCell === null || (building.kind === 'mine'
        && !snapshot.unlocks.includes('building:mine'))) continue;
      const color = BUILDING_COLORS[building.kind];
      if (color !== undefined) this.addBuilding(building.kind, building.mapCell, color);
    }
    this.addBarracks();

    const selected = new Set(view.selectedUnitIds);
    const stacks = new Map<string, typeof snapshot.units>();
    for (const unit of snapshot.units) {
      if (unit.mapCell === null) continue;
      const key = `${unit.mapCell.x}:${unit.mapCell.y}`;
      stacks.set(key, [...(stacks.get(key) ?? []), unit]);
    }
    for (const units of stacks.values()) {
      units.forEach((unit, index) => {
        const cell = unit.mapCell!;
        const x = cell.x * GRID_CELL_SIZE + 10 + (index % 5) * 10;
        const y = cell.y * GRID_CELL_SIZE + 22 + Math.floor(index / 5) * 18;
        if (this.textures.exists('unit.goblin.idle')) {
          const sprite = this.add.sprite(x, y, 'unit.goblin.idle')
            .setDisplaySize(24, 24)
            .setDepth(10);
          const requestedAnimation = unit.assignment.kind === 'idle' ? 'goblin.idle' : 'goblin.walk';
          sprite.play(this.anims.exists(requestedAnimation) ? requestedAnimation : 'goblin.idle');
          if (selected.has(unit.id)) sprite.setTint(0xffff66);
          this.dynamicObjects.push(sprite);
        } else {
          const circle = this.add.circle(x, y, 7, 0x22aa44).setDepth(10);
          if (selected.has(unit.id)) circle.setStrokeStyle(3, 0x111111);
          this.dynamicObjects.push(circle);
        }
      });
    }
    this.lastRevision = snapshot.revision;
    this.lastSelection = view.selectedUnitIds.join('|');
  }

  private createGoblinAnimations(): void {
    const definitions = [
      { key: 'goblin.idle', texture: 'unit.goblin.idle', end: 15, frameRate: 5, repeat: -1 },
      { key: 'goblin.walk', texture: 'unit.goblin.walk', end: 5, frameRate: 5, repeat: -1 },
      { key: 'goblin.attack', texture: 'unit.goblin.attack', end: 3, frameRate: 10, repeat: 0 },
      { key: 'goblin.jump', texture: 'unit.goblin.jump', end: 5, frameRate: 10, repeat: 0 },
      { key: 'goblin.damage', texture: 'unit.goblin.damage', end: 3, frameRate: 10, repeat: 0 },
      { key: 'goblin.death', texture: 'unit.goblin.death', end: 13, frameRate: 10, repeat: 0 },
    ] as const;
    for (const definition of definitions) {
      if (!this.textures.exists(definition.texture) || this.anims.exists(definition.key)) continue;
      this.anims.create({
        key: definition.key,
        frames: this.anims.generateFrameNumbers(definition.texture, { start: 0, end: definition.end }),
        frameRate: definition.frameRate,
        repeat: definition.repeat,
      });
    }
  }

  private addBuilding(kind: string, cell: GridCell, fallbackColor: number): void {
    const footprint = BUILDING_FOOTPRINTS[kind as keyof typeof BUILDING_FOOTPRINTS];
    if (footprint === undefined) return;
    const width = footprint.width * GRID_CELL_SIZE;
    const height = footprint.height * GRID_CELL_SIZE;
    const centerX = cell.x * GRID_CELL_SIZE + width / 2;
    const centerY = cell.y * GRID_CELL_SIZE + height / 2;
    const textureKey = TEXTURE_KEYS[kind];
    if (textureKey !== undefined && this.textures.exists(textureKey)) {
      this.dynamicObjects.push(this.add.image(
        centerX,
        centerY,
        textureKey,
      ).setDisplaySize(width - 4, height - 4).setDepth(2));
      return;
    }
    this.dynamicObjects.push(this.add.rectangle(
      centerX, centerY, width - 4, height - 4, fallbackColor,
    ).setDepth(2));
  }

  private addBarracks(): void {
    const x = BARRACKS_CELL.x * GRID_CELL_SIZE;
    const y = BARRACKS_CELL.y * GRID_CELL_SIZE;
    const width = BARRACKS_FOOTPRINT.width * GRID_CELL_SIZE;
    const height = BARRACKS_FOOTPRINT.height * GRID_CELL_SIZE;
    this.dynamicObjects.push(
      this.add.rectangle(x + width / 2, y + height / 2, width - 4, height - 4, 0x5e4a3b).setDepth(2),
      this.add.rectangle(x + width / 2, y + 12, width - 10, 20, 0x8f3f32).setDepth(3),
      this.add.rectangle(x + width / 2, y + 44, 18, 30, 0x263b4f).setDepth(3),
    );
  }

  private readonly onPointerDown = (pointer: Phaser.Input.Pointer): void => {
    const cell = this.pointerCell(pointer);
    if (cell) this.dragStart = { x: pointer.worldX, y: pointer.worldY, cell };
  };

  private readonly onPointerMove = (pointer: Phaser.Input.Pointer): void => {
    const hoveredCell = this.pointerCell(pointer);
    if (hoveredCell !== null) this.drawHover(hoveredCell);
    if (!pointer.isDown || this.dragStart === null || this.dragGraphics === null) return;
    const x = Math.min(this.dragStart.x, pointer.worldX);
    const y = Math.min(this.dragStart.y, pointer.worldY);
    const width = Math.abs(pointer.worldX - this.dragStart.x);
    const height = Math.abs(pointer.worldY - this.dragStart.y);
    this.dragGraphics.clear().lineStyle(2, 0x111111, 1).fillStyle(0x5599ff, 0.12);
    this.dragGraphics.fillRect(x, y, width, height).strokeRect(x, y, width, height);
  };

  private drawHover(cell: GridCell): void {
    if (this.hoverGraphics === null) return;
    const view = this.runtime?.getViewState();
    const snapshot = this.runtime?.getSnapshot();
    const footprint = view?.mode === 'place-mine' ? BUILDING_FOOTPRINTS.mine : { width: 1, height: 1 };
    const valid = view?.mode !== 'place-mine'
      || (snapshot !== undefined && isBuildingPlacementValid(snapshot, 'mine', cell));
    const color = valid ? 0x7dff6a : 0xff4a4a;
    this.hoverGraphics.clear().lineStyle(2, color, 1).fillStyle(color, 0.22);
    this.hoverGraphics.fillRect(
      cell.x * GRID_CELL_SIZE,
      cell.y * GRID_CELL_SIZE,
      footprint.width * GRID_CELL_SIZE,
      footprint.height * GRID_CELL_SIZE,
    ).strokeRect(
      cell.x * GRID_CELL_SIZE,
      cell.y * GRID_CELL_SIZE,
      footprint.width * GRID_CELL_SIZE,
      footprint.height * GRID_CELL_SIZE,
    );
  }

  private readonly onPointerUp = (pointer: Phaser.Input.Pointer): void => {
    if (this.dragStart === null) return;
    const endCell = this.pointerCell(pointer);
    const moved = Math.hypot(pointer.worldX - this.dragStart.x, pointer.worldY - this.dragStart.y) > 8;
    this.dragGraphics?.clear();
    if (endCell !== null) {
      if (moved) this.runtime?.handleBoxSelection(this.dragStart.cell, endCell);
      else this.runtime?.handleCellClick(endCell);
    }
    this.dragStart = null;
  };

  private pointerCell(pointer: Phaser.Input.Pointer): GridCell | null {
    const x = Math.floor(pointer.worldX / GRID_CELL_SIZE);
    const y = Math.floor(pointer.worldY / GRID_CELL_SIZE);
    return x >= 0 && x < GRID_COLUMNS && y >= 0 && y < GRID_ROWS ? { x, y } : null;
  }
}
