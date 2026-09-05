import { BUILDING_CATALOG, UNIT_CATALOG } from '../content/catalog';
import type { GameSnapshot } from '../application/snapshot';
import type { InteractionViewState } from '../application/MapInteractionController';
import { GRID_COLUMNS, GRID_ROWS } from '../domain/map/grid';

export type ShopCategory = 'minions' | 'buildings' | 'decorations' | null;

export function renderShopPanel(
  snapshot: GameSnapshot,
  category: ShopCategory,
  interaction: InteractionViewState | null = null,
): string {
  const mineOwned = snapshot.unlocks.includes(BUILDING_CATALOG.mine.unlockId);
  const ore = snapshot.buildings.reduce((sum, building) => sum + building.inventory.ironOre, 0);

  return `
    <section class="stats pixel-panel" aria-labelledby="gold-heading">
      <div class="stats__row stats__row--gold">
        <span class="stat-icon stat-icon--gold" aria-hidden="true"></span>
        <div><h1 id="gold-heading">GOLD</h1><output data-testid="gold">${snapshot.wallet.gold}</output></div>
      </div>
      <div class="stats__section"><h2>MINIONS</h2><output data-testid="creature-count">${snapshot.units.length}</output></div>
      <div class="stats__section"><h2>RESOURCES</h2><p><span class="stat-icon stat-icon--ore" aria-hidden="true"></span><output>${Math.floor(ore)}</output></p></div>
    </section>
    <aside class="shop" aria-label="Магазин">
      ${category === null ? '' : `<div class="shop__drawer pixel-panel">
        <h2>${categoryLabel(category)}</h2>
        ${category === 'minions' ? `
          <button type="button" data-action="buy-goblin">
            Гоблин <span>${UNIT_CATALOG.goblin.cost} золота</span>
          </button>` : ''}
        ${category === 'buildings' ? `
          <button type="button" data-action="buy-mine"${mineOwned ? ' disabled' : ''}>
            ${mineOwned ? 'Шахта куплена' : `Шахта <span>${BUILDING_CATALOG.mine.purchaseCost} золота</span>`}
          </button>` : ''}
        ${category === 'decorations' ? '<p class="shop__empty">Пока пусто</p>' : ''}
      </div>`}
      ${categoryButton('minions', 'Миньены', category)}
      ${categoryButton('buildings', 'Постройки', category)}
      ${categoryButton('decorations', 'Декорации', category)}
    </aside>
    ${interaction === null ? '' : renderInteraction(interaction)}
  `;
}

function renderInteraction(state: InteractionViewState): string {
  const selected = state.selectedUnitIds.length;
  const targeting = state.mode !== 'idle';
  return `
    ${targeting ? `<section class="mode-prompt"><strong>${state.message}</strong><button type="button" data-action="cancel-mode">Отмена</button></section>` : ''}
    ${state.stackPicker ? `
      <section class="selection-panel stack-picker" aria-label="Выбор юнитов в клетке">
        <label for="stack-quantity">Выбрать: <output data-stack-output>${state.stackPicker.quantity}</output> из ${state.stackPicker.unitIds.length}</label>
        <input id="stack-quantity" data-action="stack-quantity" type="range" min="1" max="${state.stackPicker.unitIds.length}" value="${state.stackPicker.quantity}">
        <button type="button" data-action="confirm-stack">Выбрать</button>
      </section>` : ''}
    ${selected > 0 && state.stackPicker === null ? `
      <section class="selection-panel" aria-label="Действия выбранных юнитов">
        <strong>Выбрано: ${selected}</strong>
        ${state.commandsOpen ? `
          <button type="button" data-action="command-work">Работать</button>
          <button type="button" data-action="command-haul">Переносить ресурсы</button>
          <button type="button" data-action="command-barracks">В бараки</button>
        ` : `
          <button type="button" data-action="open-commands">Команды</button>
          <button type="button" data-action="sell-selected">Продать (50%)</button>
        `}
      </section>` : ''}
    <section class="coordinate-picker" aria-label="Управление картой с клавиатуры">
      <strong>Клетки</strong>
      <label>X1 <input data-cell-x1 type="number" min="0" max="${GRID_COLUMNS - 1}" value="0"></label>
      <label>Y1 <input data-cell-y1 type="number" min="0" max="${GRID_ROWS - 1}" value="0"></label>
      <button type="button" data-action="select-cell">Выбрать клетку</button>
      <label>X2 <input data-cell-x2 type="number" min="0" max="${GRID_COLUMNS - 1}" value="0"></label>
      <label>Y2 <input data-cell-y2 type="number" min="0" max="${GRID_ROWS - 1}" value="0"></label>
      <button type="button" data-action="select-area">Выделить область</button>
    </section>
    <p class="status-message" aria-live="polite">${targeting ? '' : state.message}</p>
  `;
}

function categoryButton(
  action: Exclude<ShopCategory, null>,
  label: string,
  active: ShopCategory,
): string {
  return `<button type="button" class="shop__category" data-action="category" data-category="${action}" aria-label="${label}" title="${label}" aria-expanded="${active === action}"><span class="tab-icon tab-icon--${action}" aria-hidden="true"></span><small>${label}</small></button>`;
}

function categoryLabel(category: Exclude<ShopCategory, null>): string {
  if (category === 'minions') return 'МИНЬОНЫ';
  if (category === 'buildings') return 'ПОСТРОЙКИ';
  return 'ДЕКОРАЦИИ';
}
