import Phaser from 'phaser';

import manifest from '../../assets/curated-assets.json';
import terrainPreview from '../content/forgottenMemoriesPreview.json';
import { CELL_SIZE, GRID_HEIGHT, GRID_WIDTH, type UnitKind } from '../content/catalog';
import { GameSession, type BuildingSnapshot, type GameSnapshot, type UnitSnapshot } from '../application/GameSession';
import { MapInteractionController } from '../application/MapInteractionController';

const WORLD_WIDTH = 1070;
const WORLD_HEIGHT = 900;
const GRID_Y = 112;
const GROUND_WIDTH = GRID_WIDTH * CELL_SIZE;
const GROUND_HEIGHT = GRID_HEIGHT * CELL_SIZE;
const GRID_X = (WORLD_WIDTH - GROUND_WIDTH) / 2;
const GRID_LINE_SIZE = 2;
const BUILDING_COLLISION_COLOR = 0x38e7ff;
const UNIT_COLLISION_COLOR = 0xffdd55;
const TARGET_BUILDING_COLOR = 0xdfff7a;
const TARGET_SOURCE_COLOR = 0x66e8ff;
const UNIT_SPRITE_SCALE: Record<UnitKind, number> = { goblin: 4, troll: 4.5 };
const UI_FONT = '"Trebuchet MS", "Segoe UI", sans-serif';

interface BuildingVisual {
  readonly container: Phaser.GameObjects.Container;
  readonly outline: Phaser.GameObjects.Sprite;
  readonly sprite: Phaser.GameObjects.Sprite;
  readonly label: Phaser.GameObjects.Text;
  readonly meter: Phaser.GameObjects.Graphics;
}

interface UnitVisual {
  readonly container: Phaser.GameObjects.Container;
  readonly sprite: Phaser.GameObjects.Sprite;
  readonly selection: Phaser.GameObjects.Graphics;
  readonly cargo: Phaser.GameObjects.Graphics;
  readonly cargoText: Phaser.GameObjects.Text;
}

type HoverTarget = { readonly kind: 'building' | 'unit'; readonly id: string };

export class ColonyScene extends Phaser.Scene {
  readonly #buildingVisuals = new Map<string, BuildingVisual>();
  readonly #unitVisuals = new Map<string, UnitVisual>();
  #snapshot: GameSnapshot;
  #grid!: Phaser.GameObjects.Graphics;
  #routes!: Phaser.GameObjects.Graphics;
  #targetingShade!: Phaser.GameObjects.Graphics;
  #targetingVfx!: Phaser.GameObjects.Graphics;
  #placementPreview!: Phaser.GameObjects.Graphics;
  #dragBox!: Phaser.GameObjects.Graphics;
  #commandFan!: Phaser.GameObjects.Container;
  #tooltip!: Phaser.GameObjects.Container;
  #tooltipBackground!: Phaser.GameObjects.Graphics;
  #tooltipText!: Phaser.GameObjects.Text;
  #dragStart: Phaser.Math.Vector2 | null = null;
  #objectPointerDown = false;
  #pointerCell: { x: number; y: number } | null = null;
  #hoverTarget: HoverTarget | null = null;
  #guidesVisible = true;

  constructor(
    private readonly session: GameSession,
    private readonly interactions: MapInteractionController,
    private readonly clockScale = 1,
  ) {
    super({ key: 'colony' });
    this.#snapshot = session.snapshot();
  }

  preload(): void {
    for (const asset of manifest.assets) {
      const url = `${import.meta.env.BASE_URL}${asset.output.replace(/^public\//, '')}`;
      if (asset.type === 'spritesheet' && 'frameWidth' in asset && 'frameHeight' in asset) {
        this.load.spritesheet(asset.key, url, {
          frameWidth: asset.frameWidth,
          frameHeight: asset.frameHeight,
        });
      } else {
        this.load.image(asset.key, url);
      }
    }
  }

  create(): void {
    this.#registerEnvironmentFrames();
    this.#drawEnvironment();
    this.#createAnimations();
    this.#grid = this.add.graphics().setDepth(5);
    this.#routes = this.add.graphics().setDepth(8);
    this.#placementPreview = this.add.graphics().setDepth(70);
    this.#dragBox = this.add.graphics().setDepth(80);
    this.#targetingShade = this.add.graphics().setDepth(100).setVisible(false);
    this.#targetingVfx = this.add.graphics().setDepth(130).setVisible(false);
    this.#createCommandFan();
    this.#createTooltip();
    this.#drawGrid();
    this.#bindInput();
    this.#syncSnapshot(this.#snapshot);

    const unsubscribeSession = this.session.subscribe((snapshot) => this.#syncSnapshot(snapshot));
    const unsubscribeInteraction = this.interactions.subscribe(() => this.#syncInteraction());
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      unsubscribeSession();
      unsubscribeInteraction();
    });
  }

  update(time: number, delta: number): void {
    this.#drawTargetingVfx(time);
    if (document.visibilityState !== 'visible') return;
    this.session.advance(Math.min(delta, 1000) * this.clockScale);
  }

  #drawEnvironment(): void {
    const background = this.add.graphics();
    background.fillStyle(0x0b1715).fillRect(0, 0, WORLD_WIDTH, WORLD_HEIGHT);
    background.fillStyle(0x283824).fillRoundedRect(22, 68, WORLD_WIDTH - 44, 748, 18);
    background.lineStyle(4, 0x253b36, 1).strokeRoundedRect(22, 68, WORLD_WIDTH - 44, 748, 18);
    background.fillStyle(0x98734c).fillRect(GRID_X, GRID_Y, GROUND_WIDTH, GROUND_HEIGHT);
    background.lineStyle(4, 0x90ad54, 0.6).strokeRect(GRID_X, GRID_Y, GROUND_WIDTH, GROUND_HEIGHT);

    this.#drawLockedTerrain();
    this.#drawTerrainPreview();
    this.#drawProps();
    this.#drawLockedForest();
  }

  #drawTerrainPreview(): void {
    const palette: Readonly<Record<string, number>> = terrainPreview.palette;
    const scale = CELL_SIZE / terrainPreview.tileSize;

    terrainPreview.layout.forEach((row, y) => {
      [...row].forEach((tile, x) => {
        const frame = palette[tile];
        if (frame === undefined) throw new Error(`Unknown terrain tile: ${tile}`);
        this.add
          .image(GRID_X + x * CELL_SIZE, GRID_Y + y * CELL_SIZE, terrainPreview.textureKey, frame)
          .setOrigin(0)
          .setScale(scale)
          .setDepth(1);
      });
    });
  }

  #drawLockedTerrain(): void {
    const locked = terrainPreview.lockedTerrain;
    const scale = CELL_SIZE / terrainPreview.tileSize;
    const drawTile = (column: number, row: number): void => {
      const variant = Math.abs(column * 5 + row * 7);
      this.add
        .image(GRID_X + column * CELL_SIZE, GRID_Y + row * CELL_SIZE, terrainPreview.textureKey, locked.baseFrame)
        .setOrigin(0)
        .setScale(scale)
        .setTint(0x8f9d72)
        .setDepth(0.5);
      if (variant % 3 !== 0) return;
      const detailFrame = locked.detailFrames[variant % locked.detailFrames.length];
      if (detailFrame === undefined) throw new Error('Locked terrain has no detail frames');
      this.add
        .image(GRID_X + column * CELL_SIZE, GRID_Y + row * CELL_SIZE, terrainPreview.textureKey, detailFrame)
        .setOrigin(0)
        .setScale(scale)
        .setTint(0xaab88a)
        .setAlpha(0.24)
        .setDepth(0.6);
    };

    for (const column of locked.columns) {
      for (const row of locked.rows) drawTile(column, row);
    }
    for (const column of locked.topBottomColumns) {
      for (const row of locked.topBottomRows) drawTile(column, row);
    }
  }

  #registerEnvironmentFrames(): void {
    const texture = this.textures.get(terrainPreview.forest.textureKey);
    const frames = { ...terrainPreview.forest.frames, ...terrainPreview.props.frames };
    for (const [name, frame] of Object.entries(frames)) {
      const [x, y, width, height] = frame;
      if (x === undefined || y === undefined || width === undefined || height === undefined) {
        throw new Error(`Invalid environment frame: ${name}`);
      }
      if (!texture.has(name)) texture.add(name, 0, x, y, width, height);
    }
  }

  #drawProps(): void {
    const textureKey = terrainPreview.forest.textureKey;
    for (const prop of terrainPreview.props.placements) {
      this.add
        .image(prop.x, prop.y, textureKey, prop.frame)
        .setOrigin(0.5, 1)
        .setScale(prop.scale)
        .setDepth(2.8 + prop.y / 1000);
    }
  }

  #drawLockedForest(): void {
    const forest = terrainPreview.forest;
    const variants = Object.keys(forest.frames);
    forest.columns.forEach((baseX, column) => {
      for (let row = 0; row < forest.rows; row += 1) {
        const frame = variants[(row + column * 3) % variants.length];
        if (!frame) continue;
        const x = baseX + (((row * 7 + column * 11) % 23) - 11);
        const y = forest.startY + row * forest.stepY + ((column + row) % 3) * 7;
        this.add
          .image(x, y, forest.textureKey, frame)
          .setOrigin(0.5, 1)
          .setScale(forest.scale + ((row + column) % 3) * 0.035)
          .setFlipX((row + column) % 2 === 0)
          .setDepth(2 + y / 1000);
      }
    });
  }

  #drawGrid(): void {
    this.#grid.clear();
    this.#grid.fillStyle(0xe2f4b0, 0.28);
    for (let x = 0; x <= GRID_WIDTH; x += 1) {
      this.#grid.fillRect(
        GRID_X + x * CELL_SIZE - GRID_LINE_SIZE / 2,
        GRID_Y,
        GRID_LINE_SIZE,
        GROUND_HEIGHT,
      );
    }
    for (let y = 0; y <= GRID_HEIGHT; y += 1) {
      this.#grid.fillRect(
        GRID_X,
        GRID_Y + y * CELL_SIZE - GRID_LINE_SIZE / 2,
        GROUND_WIDTH,
        GRID_LINE_SIZE,
      );
    }
    this.#grid.setAlpha(0.55);
  }

  #createAnimations(): void {
    for (const asset of manifest.assets) {
      if (asset.type !== 'spritesheet' || !('animationStart' in asset)) continue;
      const end = asset.animationEnd;
      const frameDurationMs = asset.frameDurationMs;
      if (typeof end !== 'number' || typeof frameDurationMs !== 'number') continue;
      if (!this.textures.exists(asset.key)) this.#createUnitFallback(asset.key);
      const animationKey = `anim:${asset.key}`;
      if (this.anims.exists(animationKey)) continue;
      this.anims.create({
        key: animationKey,
        frames: this.anims.generateFrameNumbers(asset.key, {
          start: asset.animationStart,
          end,
        }),
        frameRate: 1000 / frameDurationMs,
        repeat: -1,
      });
    }
  }

  #createUnitFallback(key: string): void {
    const fallback = this.add.graphics();
    fallback.fillStyle(key.includes('troll') ? 0x6c9b44 : 0xa6d85d).fillCircle(16, 16, 12);
    fallback.lineStyle(3, 0x17231b).strokeCircle(16, 16, 12);
    fallback.generateTexture(key, 32, 32);
    fallback.destroy();
  }

  #bindInput(): void {
    this.input.on('pointerdown', (pointer: Phaser.Input.Pointer) => this.#onPointerDown(pointer));
    this.input.on('pointermove', (pointer: Phaser.Input.Pointer) => this.#onPointerMove(pointer));
    this.input.on('pointerup', (pointer: Phaser.Input.Pointer) => this.#onPointerUp(pointer));
    this.input.keyboard?.on('keydown-G', (event: KeyboardEvent) => {
      if (event.repeat) return;
      this.#guidesVisible = !this.#guidesVisible;
      this.#grid.setVisible(this.#guidesVisible);
      this.#routes.setVisible(this.#guidesVisible);
    });
  }

  #createCommandFan(): void {
    this.#commandFan = this.add.container(0, 0).setDepth(190).setVisible(false);
    const actions = [
      { label: 'Работа', icon: '⚒', angle: Math.PI * 1.17, run: () => this.interactions.beginWorkTarget() },
      { label: 'Перенос', icon: '➜', angle: Math.PI * 1.5, run: () => this.interactions.beginHaulTarget() },
      { label: 'Свободны', icon: '↺', angle: Math.PI * 1.83, run: () => this.interactions.releaseSelected() },
    ];

    for (const action of actions) {
      const x = Math.cos(action.angle) * 78;
      const y = Math.sin(action.angle) * 66;
      const hex = this.add
        .polygon(x, y, [0, -34, 30, -17, 30, 17, 0, 34, -30, 17, -30, -17], 0x29453b, 0.98)
        .setStrokeStyle(3, 0xd8ef84, 1)
        .setInteractive({ useHandCursor: true });
      const icon = this.add.text(x, y - 8, action.icon, {
        fontFamily: UI_FONT,
        fontSize: '20px',
        color: '#f4f1cf',
      }).setOrigin(0.5).setResolution(2);
      const label = this.add.text(x, y + 14, action.label, {
        fontFamily: UI_FONT,
        fontSize: '11px',
        fontStyle: 'bold',
        color: '#f4f1cf',
      }).setOrigin(0.5).setResolution(2);
      hex.on('pointerover', () => hex.setFillStyle(0x4f7148, 1));
      hex.on('pointerout', () => hex.setFillStyle(0x29453b, 0.98));
      hex.on('pointerdown', (
        pointer: Phaser.Input.Pointer,
        _localX: number,
        _localY: number,
        event: Phaser.Types.Input.EventData,
      ) => {
        if (!pointer.leftButtonDown()) return;
        event.stopPropagation();
        this.#objectPointerDown = true;
        this.#hideCommandFan();
        action.run();
      });
      this.#commandFan.add([hex, icon, label]);
    }
  }

  #showCommandFan(pointer: Phaser.Input.Pointer): void {
    this.#hideTooltip();
    this.#commandFan
      .setPosition(
        Phaser.Math.Clamp(pointer.x, 105, WORLD_WIDTH - 105),
        Phaser.Math.Clamp(pointer.y, 105, WORLD_HEIGHT - 45),
      )
      .setVisible(true);
  }

  #hideCommandFan(): void {
    this.#commandFan.setVisible(false);
  }

  #onPointerDown(pointer: Phaser.Input.Pointer): void {
    if (this.#objectPointerDown) return;
    if (pointer.rightButtonDown()) {
      this.#dragStart = null;
      this.#dragBox.clear();
      if (this.#isNearSelectedUnit(pointer.x, pointer.y)) {
        this.#showCommandFan(pointer);
        return;
      }
      this.#hideCommandFan();
      if (this.interactions.mode.kind !== 'neutral') this.interactions.cancelOrClear();
      return;
    }
    if (!pointer.leftButtonDown() || !this.#insideGround(pointer.x, pointer.y)) return;
    this.#hideCommandFan();
    if (this.interactions.mode.kind === 'placing-units') {
      const cell = this.#cellAt(pointer.x, pointer.y);
      if (cell) this.interactions.placeUnits(cell);
      return;
    }
    if (this.interactions.mode.kind === 'placing-mine') {
      const cell = this.#cellAt(pointer.x, pointer.y);
      if (cell) this.interactions.placeMine(cell);
      return;
    }
    this.#dragStart = new Phaser.Math.Vector2(pointer.x, pointer.y);
  }

  #onPointerMove(pointer: Phaser.Input.Pointer): void {
    this.#pointerCell = this.#cellAt(pointer.x, pointer.y);
    this.#drawPlacementPreview();
    if (this.#hoverTarget) this.#positionTooltip(pointer.x, pointer.y);
    if (!this.#dragStart || !pointer.isDown) return;
    this.#drawDragBox(this.#dragStart.x, this.#dragStart.y, pointer.x, pointer.y);
  }

  #onPointerUp(pointer: Phaser.Input.Pointer): void {
    if (this.#objectPointerDown) {
      this.#objectPointerDown = false;
      this.#dragStart = null;
      this.#dragBox.clear();
      return;
    }
    if (!this.#dragStart) return;
    const distance = Phaser.Math.Distance.Between(this.#dragStart.x, this.#dragStart.y, pointer.x, pointer.y);
    if (distance >= 8) {
      const rectangle = new Phaser.Geom.Rectangle(
        Math.min(this.#dragStart.x, pointer.x),
        Math.min(this.#dragStart.y, pointer.y),
        Math.abs(pointer.x - this.#dragStart.x),
        Math.abs(pointer.y - this.#dragStart.y),
      );
      const selected = this.#snapshot.units
        .filter(
          (unit) =>
            unit.assignment.kind !== 'work' &&
            rectangle.contains(GRID_X + unit.position.x, GRID_Y + unit.position.y),
        )
        .map((unit) => unit.id);
      this.interactions.selectUnits(selected);
    } else {
      this.interactions.cancelOrClear();
    }
    this.#dragStart = null;
    this.#dragBox.clear();
  }

  #drawDragBox(startX: number, startY: number, endX: number, endY: number): void {
    this.#dragBox.clear();
    const x = Math.min(startX, endX);
    const y = Math.min(startY, endY);
    const width = Math.abs(endX - startX);
    const height = Math.abs(endY - startY);
    this.#dragBox.fillStyle(0xd9f58b, 0.12).fillRect(x, y, width, height);
    this.#dragBox.lineStyle(2, 0xeaffae, 0.9).strokeRect(x, y, width, height);
  }

  #drawPlacementPreview(): void {
    this.#placementPreview.clear();
    if (!this.#pointerCell) return;
    const mode = this.interactions.mode;
    if (mode.kind !== 'placing-mine' && mode.kind !== 'placing-units') return;
    const result = mode.kind === 'placing-mine'
      ? this.session.canBuildMine(this.#pointerCell)
      : this.session.canBuyUnits(mode.unitKind, mode.amount, this.#pointerCell);
    const color = result.ok ? 0x8df06c : 0xf06457;
    const x = GRID_X + this.#pointerCell.x * CELL_SIZE;
    const y = GRID_Y + this.#pointerCell.y * CELL_SIZE;
    const size = mode.kind === 'placing-mine' ? CELL_SIZE * 3 : CELL_SIZE;
    this.#placementPreview.fillStyle(color, 0.25).fillRect(x, y, size, size);
    this.#placementPreview.lineStyle(3, color, 0.95).strokeRect(x, y, size, size);
    if (!result.ok) {
      this.#placementPreview.lineBetween(x, y, x + size, y + size);
      this.#placementPreview.lineBetween(x + size, y, x, y + size);
    }
  }

  #syncSnapshot(snapshot: GameSnapshot): void {
    this.#snapshot = snapshot;
    for (const building of snapshot.buildings) {
      const visual = this.#buildingVisuals.get(building.id) ?? this.#createBuilding(building);
      this.#syncBuilding(visual, building);
    }
    for (const unit of snapshot.units) {
      const visual = this.#unitVisuals.get(unit.id) ?? this.#createUnit(unit);
      this.#syncUnit(visual, unit);
    }
    this.#syncCollisionDebug();
    this.#drawRoutes();
    this.#refreshTooltip();
    this.#syncTargetingPresentation();
  }

  #syncInteraction(): void {
    const hasActiveCommand = this.interactions.mode.kind !== 'neutral';
    this.#grid.setAlpha(hasActiveCommand ? 1 : 0.55);
    if (hasActiveCommand || this.interactions.selectedIds().length === 0) this.#hideCommandFan();
    this.#drawPlacementPreview();
    for (const unit of this.#snapshot.units) {
      const visual = this.#unitVisuals.get(unit.id);
      if (visual) this.#drawSelection(visual.selection, this.interactions.selectedIds().includes(unit.id), unit.unitKind);
    }
    this.#syncCollisionDebug();
    this.#drawRoutes();
    this.#syncTargetingPresentation();
  }

  #createBuilding(building: BuildingSnapshot): BuildingVisual {
    const key = `building.${building.kind}`;
    if (!this.textures.exists(key)) this.#createBuildingFallback(key, building);
    const displaySize = Math.max(building.width, building.height) * CELL_SIZE + 22;
    const outline = this.add.sprite(0, -4, key)
      .setDisplaySize(displaySize + 12, displaySize + 12)
      .setTint(0xdcff87)
      .setAlpha(0.9)
      .setVisible(false);
    const sprite = this.add.sprite(0, -4, key);
    sprite.setDisplaySize(displaySize, displaySize);
    const label = this.add.text(0, -displaySize / 2 - 10, building.name, {
      fontFamily: UI_FONT,
      fontSize: '18px',
      fontStyle: 'bold',
      color: '#f6edca',
      backgroundColor: '#172522dd',
      padding: { x: 8, y: 4 },
      stroke: '#0b1110',
      strokeThickness: 3,
    }).setOrigin(0.5, 1).setResolution(2).setVisible(false);
    const meter = this.add.graphics();
    const container = this.add.container(0, 0, [outline, sprite, meter, label]);
    const collisionWidth = building.width * CELL_SIZE;
    const collisionHeight = building.height * CELL_SIZE;
    container.setSize(collisionWidth, collisionHeight);
    container.setInteractive(
      new Phaser.Geom.Rectangle(
        0,
        0,
        collisionWidth,
        collisionHeight,
      ),
      Phaser.Geom.Rectangle.Contains,
    );
    container.on('pointerdown', (
      pointer: Phaser.Input.Pointer,
      _localX: number,
      _localY: number,
      event: Phaser.Types.Input.EventData,
    ) => {
      event.stopPropagation();
      this.#objectPointerDown = true;
      this.#dragStart = null;
      this.#hideCommandFan();
      if (!pointer.leftButtonDown()) return;
      this.interactions.chooseBuilding(building.id);
    });
    container.on('pointerover', (pointer: Phaser.Input.Pointer) => {
      if (!this.#canEmphasizeBuilding(building.id)) return;
      outline.setVisible(true);
      label.setVisible(true);
      this.#showTooltip({ kind: 'building', id: building.id }, pointer);
    });
    container.on('pointerout', () => {
      this.#hideTooltip();
      this.#syncTargetingPresentation();
    });
    const visual = { container, outline, sprite, label, meter };
    this.#buildingVisuals.set(building.id, visual);
    return visual;
  }

  #createBuildingFallback(key: string, building: BuildingSnapshot): void {
    const colors: Record<BuildingSnapshot['kind'], number> = {
      mine: 0x475d57,
      warehouse: 0x7a4b35,
      market: 0xb87539,
    };
    const fallback = this.add.graphics();
    fallback.fillStyle(colors[building.kind], 1).fillRoundedRect(4, 4, 140, 140, 18);
    fallback.lineStyle(6, 0x17201e).strokeRoundedRect(4, 4, 140, 140, 18);
    fallback.generateTexture(key, 148, 148);
    fallback.destroy();
  }

  #syncBuilding(visual: BuildingVisual, building: BuildingSnapshot): void {
    const x = GRID_X + (building.cell.x + building.width / 2) * CELL_SIZE;
    const y = GRID_Y + (building.cell.y + building.height / 2) * CELL_SIZE;
    visual.container.setPosition(x, y).setDepth(20 + building.cell.y);
    visual.label.setText(building.name);
    visual.meter.clear();
    if (building.kind === 'market') return;
    const width = building.width * CELL_SIZE - 22;
    const ratio = building.maxOre === 0 ? 0 : Math.min(1, building.ore / building.maxOre);
    const meterY = building.height * CELL_SIZE * 0.45;
    visual.meter.fillStyle(0x101a18, 0.9).fillRoundedRect(-width / 2, meterY, width, 9, 4);
    visual.meter.fillStyle(building.kind === 'mine' ? 0xb7ccd0 : 0xe0a74f, 1).fillRoundedRect(-width / 2 + 2, meterY + 2, (width - 4) * ratio, 5, 3);
  }

  #syncTargetingPresentation(): void {
    const mode = this.interactions.mode;
    const active = mode.kind === 'choosing-work-target' || mode.kind === 'choosing-haul-source' || mode.kind === 'choosing-haul-destination';
    const targetIds = new Set(this.interactions.targetBuildingIds());
    const sourceId = mode.kind === 'choosing-haul-destination' ? mode.sourceId : '';
    const hoveredId = this.#hoverTarget?.kind === 'building' ? this.#hoverTarget.id : '';

    this.#targetingShade.clear().setVisible(active);
    if (active) this.#targetingShade.fillStyle(0x06100e, 0.58).fillRect(0, 0, WORLD_WIDTH, WORLD_HEIGHT);
    this.input.setDefaultCursor(active ? 'crosshair' : 'default');

    for (const building of this.#snapshot.buildings) {
      const visual = this.#buildingVisuals.get(building.id);
      if (!visual) continue;
      const isTarget = targetIds.has(building.id);
      const isSource = building.id === sourceId;
      const isHovered = building.id === hoveredId && (!active || isTarget || isSource);
      visual.container.setDepth(active && (isTarget || isSource) ? 112 + building.cell.y : 20 + building.cell.y);
      visual.outline
        .setTint(isSource ? TARGET_SOURCE_COLOR : TARGET_BUILDING_COLOR)
        .setVisible(isTarget || isSource || isHovered);
      visual.label.setVisible(isTarget || isSource || isHovered);
    }
  }

  #canEmphasizeBuilding(buildingId: string): boolean {
    const mode = this.interactions.mode;
    const active = mode.kind === 'choosing-work-target' || mode.kind === 'choosing-haul-source' || mode.kind === 'choosing-haul-destination';
    if (!active) return true;
    return this.interactions.targetBuildingIds().includes(buildingId) ||
      (mode.kind === 'choosing-haul-destination' && mode.sourceId === buildingId);
  }

  #drawTargetingVfx(time: number): void {
    const mode = this.interactions.mode;
    const active = mode.kind === 'choosing-work-target' || mode.kind === 'choosing-haul-source' || mode.kind === 'choosing-haul-destination';
    this.#targetingVfx.clear().setVisible(active);
    if (!active) return;

    const pointer = this.input.activePointer;
    const cursorX = Phaser.Math.Clamp(pointer.x, 8, WORLD_WIDTH - 8);
    const cursorY = Phaser.Math.Clamp(pointer.y, 8, WORLD_HEIGHT - 8);
    const color = mode.kind === 'choosing-haul-destination' ? TARGET_SOURCE_COLOR : TARGET_BUILDING_COLOR;
    const pulse = (Math.sin(time / 130) + 1) / 2;

    if (mode.kind === 'choosing-haul-destination') {
      const source = this.#snapshot.buildings.find((building) => building.id === mode.sourceId);
      if (source) {
        const from = this.#buildingScreenCenter(source);
        const distance = Phaser.Math.Distance.Between(from.x, from.y, cursorX, cursorY);
        this.#targetingVfx.lineStyle(8, 0x06100e, 0.72).lineBetween(from.x, from.y, cursorX, cursorY);
        this.#targetingVfx.lineStyle(3, color, 0.95).lineBetween(from.x, from.y, cursorX, cursorY);
        const offset = ((time % 700) / 700) * 30;
        this.#targetingVfx.fillStyle(0xeaffff, 0.95);
        for (let step = offset; step < distance; step += 30) {
          const progress = distance === 0 ? 0 : step / distance;
          this.#targetingVfx.fillCircle(
            Phaser.Math.Linear(from.x, cursorX, progress),
            Phaser.Math.Linear(from.y, cursorY, progress),
            3,
          );
        }
      }
    }

    this.#targetingVfx.fillStyle(color, 0.13 + pulse * 0.1).fillCircle(cursorX, cursorY, 28 + pulse * 5);
    this.#targetingVfx.fillStyle(0xf4ffff, 0.95).fillCircle(cursorX, cursorY, 4);
    this.#targetingVfx.lineStyle(3, color, 1).strokeCircle(cursorX, cursorY, 19 + pulse * 4);
    this.#targetingVfx.lineStyle(2, 0xf4ffff, 0.9).strokeCircle(cursorX, cursorY, 10);
    this.#targetingVfx.lineBetween(cursorX - 31, cursorY, cursorX - 12, cursorY);
    this.#targetingVfx.lineBetween(cursorX + 12, cursorY, cursorX + 31, cursorY);
    this.#targetingVfx.lineBetween(cursorX, cursorY - 31, cursorX, cursorY - 12);
    this.#targetingVfx.lineBetween(cursorX, cursorY + 12, cursorX, cursorY + 31);
  }

  #createUnit(unit: UnitSnapshot): UnitVisual {
    const collisionRadius = unitCollisionRadius(unit.unitKind);
    const selection = this.add.graphics();
    const shadow = this.add.ellipse(0, collisionRadius * 0.55, collisionRadius * 1.5, collisionRadius * 0.5, 0x0a1714, 0.42);
    const textureKey = `unit.${unit.unitKind}.idle`;
    if (!this.textures.exists(textureKey)) this.#createUnitFallback(textureKey);
    const sprite = this.add.sprite(0, 0, textureKey, 0);
    sprite.setScale(UNIT_SPRITE_SCALE[unit.unitKind]);
    const cargo = this.add.graphics();
    const cargoText = this.add.text(7, -9, '', {
      fontFamily: UI_FONT,
      fontSize: '9px',
      color: '#fff4c4',
      stroke: '#1a211d',
      strokeThickness: 2,
    }).setOrigin(0.5).setResolution(2);
    const container = this.add.container(0, 0, [selection, shadow, sprite, cargo, cargoText]);
    container.setSize(collisionRadius * 2, collisionRadius * 2).setInteractive(
      new Phaser.Geom.Circle(collisionRadius, collisionRadius, collisionRadius),
      Phaser.Geom.Circle.Contains,
    );
    container.on('pointerdown', (
      pointer: Phaser.Input.Pointer,
      _localX: number,
      _localY: number,
      event: Phaser.Types.Input.EventData,
    ) => {
      event.stopPropagation();
      this.#objectPointerDown = true;
      this.#dragStart = null;
      if (pointer.rightButtonDown()) {
        if (this.interactions.mode.kind === 'placing-units') {
          this.interactions.cancelOrClear();
          return;
        }
        if (this.interactions.selectedIds().length === 0) this.interactions.clickUnit(unit.id, false);
        this.#showCommandFan(pointer);
        return;
      }
      if (!pointer.leftButtonDown()) return;
      this.#hideCommandFan();
      if (this.interactions.mode.kind === 'placing-units') {
        const cell = this.#cellAt(pointer.x, pointer.y);
        if (cell) this.interactions.placeUnits(cell);
        return;
      }
      this.interactions.clickUnit(unit.id, pointer.event instanceof MouseEvent && pointer.event.ctrlKey);
    });
    container.on('pointerover', (pointer: Phaser.Input.Pointer) => this.#showTooltip({ kind: 'unit', id: unit.id }, pointer));
    container.on('pointerout', () => this.#hideTooltip());
    const visual = { container, sprite, selection, cargo, cargoText };
    this.#unitVisuals.set(unit.id, visual);
    return visual;
  }

  #syncUnit(visual: UnitVisual, unit: UnitSnapshot): void {
    const visible = unit.assignment.kind !== 'work';
    visual.container.setVisible(visible);
    if (visual.container.input) visual.container.input.enabled = visible;
    if (!visible) return;
    visual.container
      .setPosition(GRID_X + unit.position.x, GRID_Y + unit.position.y)
      .setDepth(45 + unit.position.y / CELL_SIZE);
    const moving = unit.assignment.kind === 'to-work' || (
      unit.assignment.kind === 'haul' &&
      (unit.assignment.phase === 'to-source' || unit.assignment.phase === 'to-destination')
    );
    const animationKey = `anim:unit.${unit.unitKind}.${moving ? 'walk' : 'idle'}`;
    if (this.anims.exists(animationKey) && visual.sprite.anims.currentAnim?.key !== animationKey) visual.sprite.play(animationKey);
    if (moving) {
      const targetId = unit.assignment.kind === 'to-work'
        ? unit.assignment.buildingId
        : unit.assignment.phase === 'to-source' ? unit.assignment.sourceId : unit.assignment.destinationId;
      const target = this.#snapshot.buildings.find((building) => building.id === targetId);
      if (target) visual.sprite.setFlipX(target.cell.x * CELL_SIZE < unit.position.x);
    }
    const carried = unit.assignment.kind === 'haul' ? unit.assignment.carried : 0;
    visual.cargo.clear();
    visual.cargoText.setText(carried > 0 ? formatNumber(carried) : '');
    if (carried > 0) {
      visual.cargo.fillStyle(0xcad7d2).fillTriangle(2, -13, 12, -13, 7, -5);
      visual.cargo.lineStyle(1, 0x24332f).strokeTriangle(2, -13, 12, -13, 7, -5);
    }
    this.#drawSelection(visual.selection, this.interactions.selectedIds().includes(unit.id), unit.unitKind);
  }

  #drawSelection(graphics: Phaser.GameObjects.Graphics, selected: boolean, unitKind: UnitKind): void {
    graphics.clear();
    if (!selected) return;
    const radius = unitCollisionRadius(unitKind);
    graphics.fillStyle(0xeaff86, 0.2).fillCircle(0, 0, radius);
    graphics.lineStyle(1.5, 0xf2ff9f, 1).strokeCircle(0, 0, radius);
  }

  #syncCollisionDebug(): void {
    for (const visual of this.#buildingVisuals.values()) {
      this.#syncHitAreaDebug(visual.container, BUILDING_COLLISION_COLOR);
    }
    for (const visual of this.#unitVisuals.values()) {
      this.#syncHitAreaDebug(visual.container, UNIT_COLLISION_COLOR);
    }
  }

  #syncHitAreaDebug(container: Phaser.GameObjects.Container, color: number): void {
    const hasDebugShape = Boolean(container.input?.hitAreaDebug);
    const shouldShow = this.interactions.collisionDebugVisible && container.visible;
    if (shouldShow && !hasDebugShape) this.input.enableDebug(container, color);
    if (!shouldShow && hasDebugShape) this.input.removeDebug(container);
  }

  #drawRoutes(): void {
    this.#routes.clear();
    const selected = new Set(this.interactions.selectedIds());
    const seen = new Set<string>();
    for (const unit of this.#snapshot.units) {
      const assignment = unit.assignment;
      if (assignment.kind !== 'haul') continue;
      const routeKey = `${assignment.sourceId}:${assignment.destinationId}`;
      if (seen.has(routeKey) && !selected.has(unit.id)) continue;
      seen.add(routeKey);
      const source = this.#snapshot.buildings.find((building) => building.id === assignment.sourceId);
      const destination = this.#snapshot.buildings.find((building) => building.id === assignment.destinationId);
      if (!source || !destination) continue;
      const from = this.#buildingScreenCenter(source);
      const to = this.#buildingScreenCenter(destination);
      const color = selected.has(unit.id) ? 0xffd66f : 0xe5c86b;
      this.#drawDottedRoute(from.x, from.y, to.x, to.y, color, selected.has(unit.id) ? 0.95 : 0.28);
    }
  }

  #drawDottedRoute(fromX: number, fromY: number, toX: number, toY: number, color: number, alpha: number): void {
    const distance = Phaser.Math.Distance.Between(fromX, fromY, toX, toY);
    const parts = Math.max(1, Math.floor(distance / 18));
    this.#routes.lineStyle(3, color, alpha);
    for (let index = 0; index < parts; index += 2) {
      const start = index / parts;
      const end = Math.min(1, (index + 1) / parts);
      this.#routes.lineBetween(
        Phaser.Math.Linear(fromX, toX, start),
        Phaser.Math.Linear(fromY, toY, start),
        Phaser.Math.Linear(fromX, toX, end),
        Phaser.Math.Linear(fromY, toY, end),
      );
    }
    const angle = Phaser.Math.Angle.Between(fromX, fromY, toX, toY);
    const arrowX = toX - Math.cos(angle) * 42;
    const arrowY = toY - Math.sin(angle) * 42;
    this.#routes.fillStyle(color, alpha).fillTriangle(
      arrowX,
      arrowY,
      arrowX - Math.cos(angle - 0.55) * 14,
      arrowY - Math.sin(angle - 0.55) * 14,
      arrowX - Math.cos(angle + 0.55) * 14,
      arrowY - Math.sin(angle + 0.55) * 14,
    );
  }

  #createTooltip(): void {
    this.#tooltipBackground = this.add.graphics();
    this.#tooltipText = this.add.text(12, 10, '', {
      fontFamily: UI_FONT,
      fontSize: '16px',
      lineSpacing: 5,
      color: '#f6edca',
      wordWrap: { width: 250 },
    }).setResolution(2);
    this.#tooltip = this.add.container(0, 0, [this.#tooltipBackground, this.#tooltipText]).setDepth(200).setVisible(false);
  }

  #showTooltip(target: HoverTarget, pointer: Phaser.Input.Pointer): void {
    this.#hoverTarget = target;
    this.#refreshTooltip();
    this.#positionTooltip(pointer.x, pointer.y);
    this.#tooltip.setVisible(true);
  }

  #hideTooltip(): void {
    this.#hoverTarget = null;
    this.#tooltip.setVisible(false);
  }

  #refreshTooltip(): void {
    if (!this.#hoverTarget) return;
    const text = this.#hoverTarget.kind === 'building' ? this.#buildingTooltip(this.#hoverTarget.id) : this.#unitTooltip(this.#hoverTarget.id);
    this.#tooltipText.setText(text);
    const width = this.#tooltipText.width + 24;
    const height = this.#tooltipText.height + 20;
    this.#tooltipBackground.clear();
    this.#tooltipBackground.fillStyle(0x13231f, 0.97).fillRoundedRect(0, 0, width, height, 8);
    this.#tooltipBackground.lineStyle(3, 0x91a77a, 1).strokeRoundedRect(0, 0, width, height, 8);
  }

  #positionTooltip(pointerX: number, pointerY: number): void {
    const width = this.#tooltipText.width + 24;
    const height = this.#tooltipText.height + 20;
    this.#tooltip.setPosition(
      Phaser.Math.Clamp(pointerX + 18, 8, WORLD_WIDTH - width - 8),
      Phaser.Math.Clamp(pointerY + 18, 8, WORLD_HEIGHT - height - 8),
    );
  }

  #buildingTooltip(id: string): string {
    const building = this.#snapshot.buildings.find((candidate) => candidate.id === id);
    if (!building) return '';
    if (building.kind === 'mine') {
      return `${building.name}\nРуда: ${formatNumber(building.ore)} / ${building.maxOre}\nРабочие: ${building.workerCount} / ${building.maxWorkers}\nДобыча: ${Math.round(building.productionPerSecond * 60)} руды/мин`;
    }
    if (building.kind === 'warehouse') return `Склад\nРуда: ${formatNumber(building.ore)} / ${building.maxOre}\nСюда носильщики складывают добычу.`;
    return `Рынок\nПринимает руду и сразу продаёт её.\nЦена: 3 золота за единицу.`;
  }

  #unitTooltip(id: string): string {
    const unit = this.#snapshot.units.find((candidate) => candidate.id === id);
    if (!unit) return '';
    const carried = unit.assignment.kind === 'haul' ? `\nНесёт руды: ${formatNumber(unit.assignment.carried)}` : '';
    return `${unit.name} ${unit.number}\nСила: ${unit.strength}\nСкорость: ${unit.speed}\nГруз: ${unit.cargoCapacity}\n${unit.status}${carried}`;
  }

  #buildingScreenCenter(building: BuildingSnapshot): Phaser.Math.Vector2 {
    return new Phaser.Math.Vector2(
      GRID_X + (building.cell.x + building.width / 2) * CELL_SIZE,
      GRID_Y + (building.cell.y + building.height / 2) * CELL_SIZE,
    );
  }

  #insideGround(x: number, y: number): boolean {
    return x >= GRID_X && x < GRID_X + GROUND_WIDTH && y >= GRID_Y && y < GRID_Y + GROUND_HEIGHT;
  }

  #isNearSelectedUnit(x: number, y: number): boolean {
    const selected = new Set(this.interactions.selectedIds());
    return this.#snapshot.units.some((unit) =>
      selected.has(unit.id) &&
      unit.assignment.kind !== 'work' &&
      Phaser.Math.Distance.Between(x, y, GRID_X + unit.position.x, GRID_Y + unit.position.y) <= CELL_SIZE * 0.55,
    );
  }

  #cellAt(x: number, y: number): { x: number; y: number } | null {
    if (!this.#insideGround(x, y)) return null;
    return {
      x: Math.floor((x - GRID_X) / CELL_SIZE),
      y: Math.floor((y - GRID_Y) / CELL_SIZE),
    };
  }
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value);
}

function unitCollisionRadius(unitKind: UnitKind): number {
  return unitKind === 'goblin' ? 16 : 20;
}
