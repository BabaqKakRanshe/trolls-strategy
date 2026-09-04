import Phaser from 'phaser';
import type { GameSnapshot } from '../../application/snapshot';

export const SHOP_RUNTIME_REGISTRY_KEY = 'shop-runtime';

export interface MinimalSceneRuntime {
  getSnapshot(): GameSnapshot;
}

export const MINIMAL_SCENE_LAYOUT = [
  { kind: 'market', x: 420, y: 360, width: 240, height: 180, color: 0xff0000 },
  { kind: 'warehouse', x: 860, y: 360, width: 240, height: 180, color: 0x0000ff },
] as const;

export class MinimalScene extends Phaser.Scene {
  private runtime: MinimalSceneRuntime | null = null;
  private lastRevision = -1;
  private readonly dynamicObjects: Phaser.GameObjects.GameObject[] = [];

  public constructor() {
    super('minimal');
  }

  public create(): void {
    this.cameras.main.setBackgroundColor('#ffffff');
    this.runtime = this.registry.get(SHOP_RUNTIME_REGISTRY_KEY) as MinimalSceneRuntime | null;

    for (const shape of MINIMAL_SCENE_LAYOUT) {
      this.add.rectangle(
        shape.x,
        shape.y,
        shape.width,
        shape.height,
        shape.color,
      );
    }

    this.refreshDynamicObjects();
  }

  public update(): void {
    const revision = this.runtime?.getSnapshot().revision ?? -1;
    if (revision !== this.lastRevision) this.refreshDynamicObjects();
  }

  private refreshDynamicObjects(): void {
    const snapshot = this.runtime?.getSnapshot();
    if (!snapshot) return;

    this.dynamicObjects.forEach((object) => object.destroy());
    this.dynamicObjects.length = 0;

    snapshot.units.forEach((_unit, index) => {
      const column = index % 8;
      const row = Math.floor(index / 8);
      this.dynamicObjects.push(
        this.add.circle(190 + column * 58, 570 - row * 58, 18, 0x22aa44),
      );
    });

    if (snapshot.unlocks.includes('building:mine')) {
      this.dynamicObjects.push(this.add.rectangle(640, 155, 220, 130, 0x777777));
    }

    this.lastRevision = snapshot.revision;
  }
}
