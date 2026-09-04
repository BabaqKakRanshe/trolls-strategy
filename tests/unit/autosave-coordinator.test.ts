import { describe, expect, it, vi } from 'vitest';
import { GameSession } from '../../src/application/GameSession';
import { buildingId, itemId, unitId, type GameState, type UnitId } from '../../src/domain/model';
import { BrowserSaveStore, SAVE_STORAGE_KEY, type SavePort } from '../../src/persistence/autosave';
import { AutosaveCoordinator } from '../../src/persistence/autosaveCoordinator';

function mutableState(session: GameSession): GameState {
  return (session as unknown as { state: GameState }).state;
}

class MemorySavePort implements SavePort {
  value: string | null = null;
  saves = 0;

  load(): string | null {
    return this.value;
  }

  save(serialized: string): void {
    this.value = serialized;
    this.saves += 1;
  }

  clear(): void {
    this.value = null;
  }
}

describe('AutosaveCoordinator', () => {
  it('saves successful commands immediately but does not save rejected commands', () => {
    const session = GameSession.createNew();
    const port = new MemorySavePort();
    const autosave = new AutosaveCoordinator(session, port);

    expect(autosave.dispatch({ type: 'RECRUIT_UNIT', species: 'goblin', id: unitId('g1'), name: 'G1' }).ok).toBe(true);
    expect(port.saves).toBe(1);
    expect(autosave.dispatch({ type: 'RECRUIT_UNIT', species: 'goblin', id: unitId('g1'), name: 'G1' }).ok).toBe(false);
    expect(port.saves).toBe(1);
  });

  it('saves once after ten seconds of foreground simulation', () => {
    const port = new MemorySavePort();
    const autosave = new AutosaveCoordinator(GameSession.createNew(), port);
    autosave.advance(5_000);
    expect(port.saves).toBe(0);
    autosave.advance(5_000);
    expect(port.saves).toBe(1);
    autosave.advance(5_000);
    expect(port.saves).toBe(1);
  });

  it('tracks simulated time without materializing snapshots', () => {
    const session = GameSession.createNew();
    const port = new MemorySavePort();
    const autosave = new AutosaveCoordinator(session, port);
    const snapshotSpy = vi.spyOn(session, 'getSnapshot');

    autosave.advance(5_000);

    expect(snapshotSpy).not.toHaveBeenCalled();
  });

  it('flushes every successful colony mutation', () => {
    const session = GameSession.createNew();
    mutableState(session).wallet.gold = 1_000;
    const port = new MemorySavePort();
    const autosave = new AutosaveCoordinator(session, port);
    const id = unitId('worker');

    expect(autosave.dispatch({ type: 'RECRUIT_UNIT', species: 'goblin', id, name: 'Worker' }).ok).toBe(true);
    expect(autosave.dispatch({ type: 'ASSIGN_WORKERS', unitIds: [id], buildingId: buildingId('mine') }).ok).toBe(true);
    expect(autosave.dispatch({ type: 'SET_IDLE', unitIds: [id] }).ok).toBe(true);
    expect(autosave.dispatch({ type: 'EQUIP_ITEM', unitId: id, itemId: itemId('rusty-sword-1') }).ok).toBe(true);
    expect(autosave.dispatch({ type: 'UNEQUIP_ITEM', unitId: id, slot: 'weapon' }).ok).toBe(true);
    expect(autosave.dispatch({
      type: 'ASSIGN_HAULER',
      unitId: id,
      fromId: buildingId('mine'),
      toId: buildingId('warehouse'),
    }).ok).toBe(true);
    expect(autosave.dispatch({ type: 'SET_IDLE', unitIds: [id] }).ok).toBe(true);
    mutableState(session).buildings.find(({ kind }) => kind === 'warehouse')!.inventory.ironOre = 5;
    expect(autosave.dispatch({ type: 'SELL_ORE', buildingId: buildingId('warehouse'), amount: 1 }).ok).toBe(true);

    expect(port.saves).toBe(8);
  });

  it('flushes mission start, committed abort, and resolution', () => {
    const prepare = (): { autosave: AutosaveCoordinator; port: MemorySavePort; ids: UnitId[] } => {
      const session = GameSession.createNew(99);
      mutableState(session).wallet.gold = 10_000;
      const port = new MemorySavePort();
      const autosave = new AutosaveCoordinator(session, port);
      const ids = ['a', 'b', 'c', 'd'].map((name) => {
        const id = unitId(name);
        autosave.dispatch({ type: 'RECRUIT_UNIT', species: 'troll', id, name });
        return id;
      });
      for (let elapsed = 0; elapsed < 120_000; elapsed += 5_000) autosave.advance(5_000);
      port.saves = 0;
      return { autosave, port, ids };
    };
    const formationFor = (ids: UnitId[]) => ({
      unitIds: ids,
      slots: [{ x: 1, y: 2 }, { x: 4, y: 2 }, { x: 1, y: 3 }, { x: 4, y: 3 }],
    });

    const aborted = prepare();
    const abortStart = aborted.autosave.dispatch({
      type: 'START_MISSION', missionId: 'mission-1', formation: formationFor(aborted.ids),
    });
    if (!abortStart.ok) throw new Error('fixture');
    expect(aborted.autosave.dispatch({ type: 'ABORT_MISSION', runId: abortStart.value }).ok).toBe(true);
    expect(aborted.port.saves).toBe(2);

    const resolved = prepare();
    const resolveStart = resolved.autosave.dispatch({
      type: 'START_MISSION', missionId: 'mission-1', formation: formationFor(resolved.ids),
    });
    if (!resolveStart.ok) throw new Error('fixture');
    expect(resolved.autosave.dispatch({ type: 'RESOLVE_MISSION', runId: resolveStart.value }).ok).toBe(true);
    expect(resolved.port.saves).toBe(2);
  });

  it('uses the versioned browser storage key', () => {
    const values = new Map<string, string>();
    const store = new BrowserSaveStore({
      getItem: (key) => values.get(key) ?? null,
      setItem: (key, value) => { values.set(key, value); },
      removeItem: (key) => { values.delete(key); },
    });
    store.save('saved');
    expect(values.get(SAVE_STORAGE_KEY)).toBe('saved');
    expect(store.load()).toBe('saved');
    store.clear();
    expect(store.load()).toBeNull();
  });
});
