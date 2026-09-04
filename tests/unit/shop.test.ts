import { describe, expect, it } from 'vitest';
import { GameSession } from '../../src/application/GameSession';
import { unitId } from '../../src/domain/model';
import { renderShopPanel } from '../../src/ui/renderShopPanel';

describe('map shop', () => {
  it('starts with 1000 gold and no creatures', () => {
    const snapshot = GameSession.createNew().getSnapshot();
    const markup = renderShopPanel(snapshot, null);

    expect(snapshot.wallet.gold).toBe(1_000);
    expect(snapshot.units).toHaveLength(0);
    expect(markup).toContain('Gold');
    expect(markup).toContain('data-testid="gold">1000');
    expect(markup).toContain('data-testid="creature-count">0');
  });

  it('recruits a goblin for 40 gold and updates the creature count', () => {
    const session = GameSession.createNew();
    expect(session.dispatch({
      type: 'RECRUIT_UNIT',
      species: 'goblin',
      id: unitId('goblin-1'),
      name: 'Гоблин 1',
    }).ok).toBe(true);

    const snapshot = session.getSnapshot();
    expect(snapshot.wallet.gold).toBe(960);
    expect(snapshot.units).toHaveLength(1);
    expect(renderShopPanel(snapshot, 'minions')).toContain('Гоблин <span>40 золота</span>');
  });

  it('purchases the mine once for 200 gold', () => {
    const session = GameSession.createNew();

    expect(session.dispatch({ type: 'PURCHASE_MINE' })).toEqual({
      ok: true,
      value: undefined,
    });
    expect(session.getSnapshot().wallet.gold).toBe(800);
    expect(session.getSnapshot().unlocks).toContain('building:mine');
    expect(session.dispatch({ type: 'PURCHASE_MINE' })).toEqual({
      ok: false,
      error: { code: 'ALREADY_APPLIED' },
    });
    expect(session.getSnapshot().wallet.gold).toBe(800);
  });

  it('shows all three vertical categories and their current contents', () => {
    const snapshot = GameSession.createNew().getSnapshot();

    expect(renderShopPanel(snapshot, 'minions')).toContain('Миньены');
    expect(renderShopPanel(snapshot, 'buildings')).toContain('Шахта <span>200 золота</span>');
    expect(renderShopPanel(snapshot, 'decorations')).toContain('Пока пусто');
  });
});
