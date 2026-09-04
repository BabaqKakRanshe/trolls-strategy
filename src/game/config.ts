import Phaser from 'phaser';
import {
  MinimalScene,
  SHOP_RUNTIME_REGISTRY_KEY,
  type MinimalSceneRuntime,
} from './scenes/MinimalScene';

export function createGameConfig(
  parent: string,
  runtime?: MinimalSceneRuntime,
): Phaser.Types.Core.GameConfig {
  return {
    type: Phaser.WEBGL,
    parent,
    width: 1280,
    height: 720,
    backgroundColor: '#ffffff',
    scale: {
      mode: Phaser.Scale.FIT,
      autoCenter: Phaser.Scale.CENTER_BOTH,
    },
    ...(runtime ? {
      callbacks: {
        preBoot: (game: Phaser.Game) => {
          game.registry.set(SHOP_RUNTIME_REGISTRY_KEY, runtime);
        },
      },
    } : {}),
    scene: [MinimalScene],
  };
}
