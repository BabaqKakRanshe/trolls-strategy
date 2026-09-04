import type {
  GameCommandContractMap,
  GameCommandOf,
  GameCommandType,
} from '../application/GameSession';
import type { GameSnapshot } from '../application/snapshot';
import { unitId } from '../domain/model';
import type { CommandResult } from '../domain/results';
import { renderShopPanel, type ShopCategory } from './renderShopPanel';

export interface ShopRuntime {
  getSnapshot(): GameSnapshot;
  dispatch<TType extends GameCommandType>(
    command: GameCommandOf<TType>,
  ): CommandResult<GameCommandContractMap[TType]['value']>;
}

export class ShopUi {
  private category: ShopCategory = null;

  public constructor(
    private readonly root: HTMLElement,
    private readonly runtime: ShopRuntime,
  ) {
    this.root.addEventListener('click', this.handleClick);
    this.render();
  }

  private readonly handleClick = (event: MouseEvent): void => {
    const button = (event.target as Element | null)?.closest<HTMLButtonElement>('button[data-action]');
    if (!button || !this.root.contains(button)) return;

    const action = button.dataset.action;
    if (action === 'category') {
      const next = button.dataset.category as Exclude<ShopCategory, null>;
      this.category = this.category === next ? null : next;
      this.render();
      return;
    }

    if (action === 'buy-goblin') {
      const snapshot = this.runtime.getSnapshot();
      const sequence = snapshot.units.length + 1;
      const result = this.runtime.dispatch({
        type: 'RECRUIT_UNIT',
        species: 'goblin',
        id: unitId(`goblin-${sequence}`),
        name: `Гоблин ${sequence}`,
      });
      this.render(result.ok ? 'Гоблин появился на карте' : errorMessage(result.error.code));
      return;
    }

    if (action === 'buy-mine') {
      const result = this.runtime.dispatch({ type: 'PURCHASE_MINE' });
      this.render(result.ok ? 'Шахта построена' : errorMessage(result.error.code));
    }
  };

  private render(message = ''): void {
    this.root.innerHTML = renderShopPanel(
      this.runtime.getSnapshot(),
      this.category,
      message,
    );
  }
}

function errorMessage(code: string): string {
  if (code === 'INSUFFICIENT_GOLD') return 'Недостаточно золота';
  if (code === 'ALREADY_APPLIED') return 'Шахта уже построена';
  return 'Действие недоступно';
}
