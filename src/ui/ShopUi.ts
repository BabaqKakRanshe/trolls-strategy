import { MapInteractionController } from '../application/MapInteractionController';
import { renderShopPanel, type ShopCategory } from './renderShopPanel';

export class ShopUi {
  private category: ShopCategory = null;

  public constructor(
    private readonly root: HTMLElement,
    private readonly controller: MapInteractionController,
  ) {
    this.root.addEventListener('click', this.handleClick);
    this.root.addEventListener('input', this.handleInput);
    this.controller.subscribe(() => this.render());
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
      this.controller.beginGoblinPlacement();
      return;
    }

    if (action === 'buy-mine') {
      this.controller.beginMinePlacement();
      return;
    }
    if (action === 'cancel-mode') this.controller.cancelMode();
    if (action === 'confirm-stack') this.controller.confirmStackSelection();
    if (action === 'open-commands') this.controller.openCommands();
    if (action === 'sell-selected') this.controller.sellSelected();
    if (action === 'command-work') this.controller.beginWorkTarget();
    if (action === 'command-haul') this.controller.beginHaulTarget();
    if (action === 'command-barracks') this.controller.sendSelectedToBarracks();
    if (action === 'select-cell') {
      const cell = this.readCell('1');
      if (cell) this.controller.handleCellClick(cell);
    }
    if (action === 'select-area') {
      const from = this.readCell('1');
      const to = this.readCell('2');
      if (from && to) this.controller.handleBoxSelection(from, to);
    }
  };

  private readonly handleInput = (event: Event): void => {
    const input = event.target as HTMLInputElement | null;
    if (input?.dataset.action === 'stack-quantity') {
      this.controller.setStackQuantity(Number(input.value));
      const output = this.root.querySelector<HTMLOutputElement>('[data-stack-output]');
      if (output) output.value = input.value;
    }
  };

  private readCell(suffix: '1' | '2'): { x: number; y: number } | null {
    const x = this.root.querySelector<HTMLInputElement>(`[data-cell-x${suffix}]`);
    const y = this.root.querySelector<HTMLInputElement>(`[data-cell-y${suffix}]`);
    if (!x || !y || !x.checkValidity() || !y.checkValidity()) return null;
    return { x: Number(x.value), y: Number(y.value) };
  }

  private render(): void {
    this.root.innerHTML = renderShopPanel(
      this.controller.getSnapshot(),
      this.category,
      this.controller.getViewState(),
    );
  }
}
