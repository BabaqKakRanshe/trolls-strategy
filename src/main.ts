import Phaser from 'phaser';
import { GameSession } from './application/GameSession';
import { MapInteractionController } from './application/MapInteractionController';
import { createGameConfig } from './game/config';
import { ShopUi } from './ui/ShopUi';
import './styles.css';

function startGame(): void {
  const uiRoot = document.querySelector<HTMLElement>('#ui-root');
  if (!uiRoot) throw new Error('Game UI root is missing.');

  const session = GameSession.createNew();
  const controller = new MapInteractionController(session);
  new ShopUi(uiRoot, controller);
  new Phaser.Game(createGameConfig('game-canvas', controller));
}

document.addEventListener('DOMContentLoaded', startGame, { once: true });
