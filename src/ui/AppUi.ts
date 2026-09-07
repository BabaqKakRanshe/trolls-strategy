import manifest from '../../assets/curated-assets.json';
import { BUILDING_CATALOG, UNIT_CATALOG, type UnitKind } from '../content/catalog';
import { MapInteractionController } from '../application/MapInteractionController';
import { GameSession, type GameSnapshot } from '../application/GameSession';

const SHOP_IMAGES = {
  mine: assetUrl('building.mine'),
  goblin: assetUrl('unit.goblin.idle'),
  troll: assetUrl('unit.troll.idle'),
};

export class AppUi {
  #snapshot: GameSnapshot;
  #rosterSignature = '';
  #targetSignature = '';

  constructor(
    private readonly root: HTMLElement,
    private readonly session: GameSession,
    private readonly interactions: MapInteractionController,
  ) {
    this.#snapshot = session.snapshot();
    this.root.innerHTML = shellMarkup();
    this.root.addEventListener('click', (event) => this.#handleClick(event));
    this.root.addEventListener('input', (event) => {
      if ((event.target as Element | null)?.matches('#hire-amount')) this.#updateShop();
    });
    this.root.addEventListener('pointerover', (event) => {
      this.#positionShopTooltip(event.target as Element | null);
    });
    this.root.addEventListener('focusin', (event) => {
      this.#positionShopTooltip(event.target as Element | null);
    });
    this.root.addEventListener('change', (event) => {
      const input = event.target as HTMLInputElement | null;
      if (input?.id === 'show-collisions') this.interactions.setCollisionDebugVisible(input.checked);
    });
    this.root.ownerDocument.addEventListener('keydown', (event) => {
      if (event.key !== 'F1' || event.repeat) return;
      event.preventDefault();
      this.interactions.toggleDebugPanel();
      if (this.interactions.debugPanelOpen) query<HTMLInputElement>(this.root, '#show-collisions').focus();
    });
    this.session.subscribe((snapshot) => {
      this.#snapshot = snapshot;
      this.render();
    });
    this.interactions.subscribe(() => this.render());
    this.render();
  }

  render(): void {
    setText(this.root, '#gold-value', formatNumber(this.#snapshot.gold));
    setText(this.root, '#ore-value', formatNumber(this.#snapshot.totalOre));
    setText(this.root, '#sold-value', formatNumber(this.#snapshot.soldOre));
    setText(this.root, '#population-value', String(this.#snapshot.units.length));
    setText(this.root, '#selected-value', String(this.interactions.selectedIds().length));
    setText(this.root, '#game-status', this.interactions.message);
    this.#updateShop();
    this.#renderRoster();
    this.#renderCommandDock();
    this.#renderDebugPanel();
  }

  #handleClick(event: MouseEvent): void {
    const button = (event.target as Element | null)?.closest<HTMLButtonElement>('button[data-action]');
    if (!button) return;
    const action = button.dataset.action;
    if (action === 'buy-unit') {
      this.interactions.beginUnitPlacement(button.dataset.unitKind as UnitKind, this.#hireAmount());
    }
    if (action === 'decrease-hire-amount' || action === 'increase-hire-amount') {
      const input = query<HTMLInputElement>(this.root, '#hire-amount');
      const current = Number.isFinite(input.valueAsNumber) ? input.valueAsNumber : 1;
      const change = action === 'increase-hire-amount' ? 1 : -1;
      input.value = String(Math.min(20, Math.max(1, current + change)));
      this.#updateShop();
    }
    if (action === 'build-mine') this.interactions.beginMinePlacement();
    if (action === 'place-automatically') this.interactions.placeMineAutomatically();
    if (action === 'select-unit') this.interactions.clickUnit(button.dataset.unitId!, event.ctrlKey);
    if (action === 'select-three') this.interactions.selectFirstIdle(3);
    if (action === 'select-next') this.interactions.selectNextIdle();
    if (action === 'work') this.interactions.beginWorkTarget();
    if (action === 'haul') this.interactions.beginHaulTarget();
    if (action === 'release') this.interactions.releaseSelected();
    if (action === 'cancel') this.interactions.cancelOrClear();
    if (action === 'choose-building') this.interactions.chooseBuilding(button.dataset.buildingId!);
    if (action === 'toggle-debug') this.interactions.toggleDebugPanel();
  }

  #renderDebugPanel(): void {
    query<HTMLElement>(this.root, '#collision-debug-panel').hidden = !this.interactions.debugPanelOpen;
    query<HTMLInputElement>(this.root, '#show-collisions').checked = this.interactions.collisionDebugVisible;
  }

  #updateShop(): void {
    const mineButton = query<HTMLButtonElement>(this.root, '[data-action="build-mine"]');
    const goblinButton = query<HTMLButtonElement>(this.root, '[data-unit-kind="goblin"]');
    const trollButton = query<HTMLButtonElement>(this.root, '[data-unit-kind="troll"]');
    const amount = this.#hireAmount();
    const validAmount = Number.isInteger(amount) && amount >= 1 && amount <= 20;
    const goblinCost = UNIT_CATALOG.goblin.price * amount;
    const trollCost = UNIT_CATALOG.troll.price * amount;
    mineButton.disabled = this.#snapshot.gold < BUILDING_CATALOG.mine.price;
    goblinButton.disabled = !validAmount || this.#snapshot.gold < goblinCost;
    trollButton.disabled = !validAmount || this.#snapshot.gold < trollCost;
    goblinButton.setAttribute('aria-label', `Выбрать клетку: гоблин, количество ${amount}, стоимость ${goblinCost} золота`);
    trollButton.setAttribute('aria-label', `Выбрать клетку: тролль, количество ${amount}, стоимость ${trollCost} золота`);
    setText(this.root, '[data-hire-cost="goblin"]', validAmount ? formatNumber(goblinCost) : '—');
    setText(this.root, '[data-hire-cost="troll"]', validAmount ? formatNumber(trollCost) : '—');

    const autoButton = query<HTMLButtonElement>(this.root, '[data-action="place-automatically"]');
    autoButton.hidden = this.interactions.mode.kind !== 'placing-mine';
    const placement = query<HTMLElement>(this.root, '#unit-placement');
    const mode = this.interactions.mode;
    placement.hidden = mode.kind !== 'placing-units';
    if (mode.kind === 'placing-units') {
      setText(this.root, '#unit-placement-title', `${UNIT_CATALOG[mode.unitKind].name} ×${mode.amount}`);
    }
  }

  #hireAmount(): number {
    return query<HTMLInputElement>(this.root, '#hire-amount').valueAsNumber;
  }

  #positionShopTooltip(target: Element | null): void {
    const card = target?.closest<HTMLElement>('.shop-card');
    const tooltip = card?.querySelector<HTMLElement>('.shop-tooltip');
    if (!card || !tooltip) return;

    const cardRect = card.getBoundingClientRect();
    const width = tooltip.offsetWidth || 272;
    const height = tooltip.offsetHeight || 80;
    const edge = 12;
    tooltip.style.left = `${Math.max(edge, cardRect.left - width - edge)}px`;
    tooltip.style.top = `${Math.min(
      window.innerHeight - height - edge,
      Math.max(edge, cardRect.top + (cardRect.height - height) / 2),
    )}px`;
  }

  #renderRoster(): void {
    const selected = new Set(this.interactions.selectedIds());
    const signature = this.#snapshot.units
      .map((unit) => `${unit.id}:${unit.status}:${selected.has(unit.id)}`)
      .join('|');
    if (signature === this.#rosterSignature) return;
    this.#rosterSignature = signature;

    const list = query<HTMLUListElement>(this.root, '#unit-roster');
    if (this.#snapshot.units.length === 0) {
      const empty = document.createElement('li');
      empty.className = 'empty-state';
      empty.textContent = 'Наймите первого работника в магазине.';
      list.replaceChildren(empty);
      return;
    }
    list.replaceChildren(
      ...this.#snapshot.units.map((unit) => {
        const item = document.createElement('li');
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `roster-unit roster-unit--${unit.unitKind}`;
        button.dataset.action = 'select-unit';
        button.dataset.unitId = unit.id;
        button.setAttribute('aria-pressed', String(selected.has(unit.id)));
        const accusativeName = unit.unitKind === 'troll' ? 'тролля' : 'гоблина';
        button.setAttribute('aria-label', `Выбрать ${accusativeName} ${unit.number}`);
        button.innerHTML = `<span class="roster-unit__portrait" aria-hidden="true"></span><span><strong>${unit.name} ${unit.number}</strong><small>${unit.status}</small></span>`;
        item.append(button);
        return item;
      }),
    );
  }

  #renderCommandDock(): void {
    const dock = query<HTMLElement>(this.root, '#game-controls');
    const hasSelection = this.interactions.selectedIds().length > 0;
    dock.dataset.active = String(hasSelection || this.interactions.mode.kind !== 'neutral');
    query<HTMLButtonElement>(dock, '[data-action="work"]').disabled = !hasSelection;
    query<HTMLButtonElement>(dock, '[data-action="haul"]').disabled = !hasSelection;
    query<HTMLButtonElement>(dock, '[data-action="release"]').disabled = !hasSelection;
    this.#renderTargets();
  }

  #renderTargets(): void {
    const mode = this.interactions.mode;
    const targetIds = new Set(this.interactions.targetBuildingIds());
    const candidates = this.#snapshot.buildings.filter((building) => targetIds.has(building.id));
    const signature = `${mode.kind}:${mode.kind === 'choosing-haul-destination' ? mode.sourceId : ''}:${candidates.map((item) => item.id).join(',')}`;
    if (signature === this.#targetSignature) return;
    this.#targetSignature = signature;

    const region = query<HTMLElement>(this.root, '#target-choices');
    const keepKeyboardFlow = query<HTMLElement>(this.root, '#game-controls').contains(document.activeElement);
    if (candidates.length === 0) {
      region.replaceChildren();
      region.hidden = true;
      return;
    }
    const prompt = document.createElement('span');
    prompt.className = 'target-prompt';
    prompt.textContent = mode.kind === 'choosing-haul-source' ? 'Источник:' : 'Цель:';
    const buttons = candidates.map((building) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'target-button';
      button.dataset.action = 'choose-building';
      button.dataset.buildingId = building.id;
      const prefix = mode.kind === 'choosing-haul-source' ? 'Источник' : 'Получатель';
      button.setAttribute('aria-label', `${prefix} ${building.name}`);
      button.textContent = building.name;
      return button;
    });
    region.hidden = false;
    region.replaceChildren(prompt, ...buttons);
    if (keepKeyboardFlow) buttons[0]?.focus();
  }
}

function shellMarkup(): string {
  return `
    <div class="game-app" style="--shop-mine-image: url('${SHOP_IMAGES.mine}'); --shop-goblin-image: url('${SHOP_IMAGES.goblin}'); --shop-troll-image: url('${SHOP_IMAGES.troll}')">
      <div class="game-layout">
        <section class="accessibility-controls" aria-label="Управление юнитами с клавиатуры">
          <span><span id="selected-value">0</span> выбрано</span>
          <button type="button" data-action="select-three" aria-label="Выбрать первых 3 свободных">Первые 3</button>
          <button type="button" data-action="select-next" aria-label="Выбрать следующего свободного">Следующий</button>
          <ul id="unit-roster" class="unit-roster"></ul>
        </section>
        <main class="world-panel" tabindex="-1">
          <div class="world-frame">
            <div class="world-frame__label"><span>Участок 01</span><small>ЛКМ — выбор · Ctrl — группа · ПКМ — приказы · <kbd>G</kbd> — сетка и маршруты · <kbd>F1</kbd> — коллизии</small></div>
            <div id="game-canvas" class="game-canvas" aria-label="Игровое поле поселения"></div>
            <section id="collision-debug-panel" class="debug-panel" aria-labelledby="collision-debug-title" hidden>
              <div class="debug-panel__head"><h2 id="collision-debug-title">Отладка коллизий</h2><button type="button" data-action="toggle-debug" aria-label="Закрыть окно отладки">F1</button></div>
              <label for="show-collisions"><input id="show-collisions" type="checkbox">Показывать коллизии</label>
              <p><i class="debug-swatch debug-swatch--building" aria-hidden="true"></i>Здания — занятые клетки</p>
              <p><i class="debug-swatch debug-swatch--unit" aria-hidden="true"></i>Существа — область выбора</p>
            </section>
            <section id="game-controls" class="command-dock" data-active="false" aria-labelledby="commands-title">
              <div class="command-dock__title"><span aria-hidden="true">✦</span><h2 id="commands-title">Приказы</h2></div>
              <div class="command-actions">
                <button type="button" data-action="work">Работать</button>
                <button type="button" data-action="haul">Переносить</button>
                <button type="button" data-action="release">Освободить</button>
                <button type="button" data-action="cancel" class="button-muted">Отмена</button>
              </div>
              <div id="target-choices" class="target-choices" hidden></div>
            </section>
          </div>
        </main>
        <aside class="panel shop-panel" aria-labelledby="shop-title">
          <div class="panel__cap panel__cap--copper"><span aria-hidden="true">▰</span><h2 id="shop-title">Гильдейский реестр</h2></div>
          <dl class="resource-grid" aria-label="Ресурсы поселения">
            <div><dt>Золото</dt><dd><span class="resource-icon resource-icon--gold" aria-hidden="true"></span><span id="gold-value">0</span></dd></div>
            <div><dt>Руда</dt><dd><span class="resource-icon resource-icon--ore" aria-hidden="true"></span><span id="ore-value">0</span></dd></div>
            <div><dt>Продано</dt><dd><span class="resource-icon resource-icon--sold" aria-hidden="true"></span><span id="sold-value" data-testid="sold-ore">0</span></dd></div>
            <div><dt>Население</dt><dd><span aria-hidden="true">♟</span><span id="population-value">0</span></dd></div>
          </dl>
          <section class="shop-section">
            <div class="shop-section__header"><h3>Постройки</h3><span>1 доступно</span></div>
            <article class="shop-card shop-card--mine">
              <div class="shop-card__art" aria-hidden="true"></div>
              <div class="shop-card__copy"><strong>Шахта</strong><small>3×3 · до 5 рабочих</small></div>
              <button type="button" data-action="build-mine" aria-label="Купить шахту за 200 золота"><span>Купить</span><b>200</b></button>
              <div class="shop-tooltip">Производит железную руду. Скорость зависит от суммы силы рабочих.</div>
            </article>
            <button type="button" class="auto-place" data-action="place-automatically" hidden>Поставить автоматически</button>
          </section>
          <section class="shop-section">
            <div class="shop-section__header">
              <h3>Найм</h3>
              <div class="hire-quantity" role="group" aria-label="Количество для найма">
                <button type="button" data-action="decrease-hire-amount" aria-label="Уменьшить количество">−</button>
                <input id="hire-amount" type="number" min="1" max="20" value="1" inputmode="numeric" aria-label="За один клик">
                <button type="button" data-action="increase-hire-amount" aria-label="Увеличить количество">+</button>
              </div>
            </div>
            <article class="shop-card shop-card--goblin">
              <div class="shop-card__art" aria-hidden="true"></div>
              <div class="shop-card__copy"><strong>Гоблин</strong><small>Скорость 5 · груз 10</small></div>
              <button type="button" data-action="buy-unit" data-unit-kind="goblin"><span>Нанять</span><b data-hire-cost="goblin">40</b></button>
              <div class="shop-tooltip"><span><small>Сила</small><b>3</b></span><span><small>Скорость</small><b>5</b></span><span><small>Груз</small><b>10</b></span></div>
            </article>
            <article class="shop-card shop-card--troll">
              <div class="shop-card__art" aria-hidden="true"></div>
              <div class="shop-card__copy"><strong>Тролль</strong><small>Сила 9 · груз 30</small></div>
              <button type="button" data-action="buy-unit" data-unit-kind="troll"><span>Нанять</span><b data-hire-cost="troll">170</b></button>
              <div class="shop-tooltip"><span><small>Сила</small><b>9</b></span><span><small>Скорость</small><b>2</b></span><span><small>Груз</small><b>30</b></span></div>
            </article>
            <div id="unit-placement" class="unit-placement" hidden>
              <p><strong id="unit-placement-title"></strong><span>Кликните по нужной клетке поля</span></p>
              <button type="button" data-action="cancel" class="button-muted">Отмена</button>
            </div>
          </section>
          <div id="game-status" class="game-status" aria-live="polite"></div>
        </aside>
      </div>
      <footer class="credits">Прототип · спрайты и тайлы: Krishna Palacio / Minifantasy (некоммерческая лицензия)</footer>
    </div>`;
}

function assetUrl(key: string): string {
  const asset = manifest.assets.find((entry) => entry.key === key);
  if (!asset) throw new Error(`Missing curated asset: ${key}`);
  return `${import.meta.env.BASE_URL}${asset.output.replace(/^public\//, '')}`;
}

function query<T extends Element>(root: ParentNode, selector: string): T {
  const element = root.querySelector<T>(selector);
  if (!element) throw new Error(`Missing UI element: ${selector}`);
  return element;
}

function setText(root: ParentNode, selector: string, value: string): void {
  query<HTMLElement>(root, selector).textContent = value;
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value);
}
