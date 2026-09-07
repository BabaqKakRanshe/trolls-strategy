import Phaser from 'phaser';

import './styles.css';
import { GameSession } from './application/GameSession';
import { MapInteractionController } from './application/MapInteractionController';
import { ColonyScene } from './game/ColonyScene';
import { AppUi } from './ui/AppUi';

const root = document.querySelector<HTMLElement>('#app');
if (!root) throw new Error('App root was not found');

const session = new GameSession();
const interactions = new MapInteractionController(session);
new AppUi(root, session, interactions);

const fastClock = import.meta.env.DEV && new URLSearchParams(window.location.search).get('fast') === '1';

new Phaser.Game({
  type: Phaser.AUTO,
  parent: 'game-canvas',
  width: 1070,
  height: 900,
  backgroundColor: '#0b1715',
  pixelArt: true,
  antialias: false,
  roundPixels: true,
  disableContextMenu: true,
  banner: false,
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH,
  },
  scene: [new ColonyScene(session, interactions, fastClock ? 40 : 1)],
});
