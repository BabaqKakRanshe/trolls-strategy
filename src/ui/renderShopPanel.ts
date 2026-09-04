import { BUILDING_CATALOG, UNIT_CATALOG } from '../content/catalog';
import type { GameSnapshot } from '../application/snapshot';

export type ShopCategory = 'minions' | 'buildings' | 'decorations' | null;

export function renderShopPanel(
  snapshot: GameSnapshot,
  category: ShopCategory,
  message = '',
): string {
  const mineOwned = snapshot.unlocks.includes(BUILDING_CATALOG.mine.unlockId);

  return `
    <section class="stats" aria-labelledby="gold-heading">
      <h1 id="gold-heading">Gold</h1>
      <output data-testid="gold">${snapshot.wallet.gold}</output>
      <p>Существа на карте</p>
      <output data-testid="creature-count">${snapshot.units.length}</output>
    </section>
    <aside class="shop" aria-label="Магазин">
      ${categoryButton('minions', 'Миньены', category)}
      ${category === 'minions' ? `
        <div class="shop__submenu">
          <button type="button" data-action="buy-goblin">
            Гоблин <span>${UNIT_CATALOG.goblin.cost} золота</span>
          </button>
        </div>` : ''}
      ${categoryButton('buildings', 'Постройки', category)}
      ${category === 'buildings' ? `
        <div class="shop__submenu">
          <button type="button" data-action="buy-mine"${mineOwned ? ' disabled' : ''}>
            ${mineOwned ? 'Шахта куплена' : `Шахта <span>${BUILDING_CATALOG.mine.purchaseCost} золота</span>`}
          </button>
        </div>` : ''}
      ${categoryButton('decorations', 'Декорации', category)}
      ${category === 'decorations' ? '<p class="shop__empty">Пока пусто</p>' : ''}
      <p class="visually-hidden" aria-live="polite">${message}</p>
    </aside>
  `;
}

function categoryButton(
  action: Exclude<ShopCategory, null>,
  label: string,
  active: ShopCategory,
): string {
  return `<button type="button" class="shop__category" data-action="category" data-category="${action}" aria-expanded="${active === action}">${label}</button>`;
}
