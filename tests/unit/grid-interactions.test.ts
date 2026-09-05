import { describe, expect, it } from 'vitest';
import { GameSession } from '../../src/application/GameSession';
import { MapInteractionController } from '../../src/application/MapInteractionController';
import { unitId } from '../../src/domain/model';
import {
  BARRACKS_CELL, MARKET_CELL, MAX_UNITS_PER_CELL, WAREHOUSE_CELL,
} from '../../src/domain/map/grid';

describe('grid map interactions', () => {
  it('spawns at most ten units in a player-selected cell', () => {
    const session = GameSession.createNew();
    for (let index = 1; index <= MAX_UNITS_PER_CELL; index += 1) {
      expect(session.dispatch({
        type: 'SPAWN_UNIT', species: 'goblin', id: unitId(`g-${index}`),
        name: `G ${index}`, cell: { x: 1, y: 1 },
      }).ok).toBe(true);
    }
    expect(session.dispatch({
      type: 'SPAWN_UNIT', species: 'goblin', id: unitId('g-11'),
      name: 'G 11', cell: { x: 1, y: 1 },
    })).toEqual({ ok: false, error: { code: 'CELL_FULL' } });
    expect(session.getSnapshot().units.every((unit) => (
      unit.mapCell?.x === 1 && unit.mapCell.y === 1
    ))).toBe(true);
  });

  it('uses complete building footprints for collision checks', () => {
    const session = GameSession.createNew();
    expect(session.dispatch({
      type: 'SPAWN_UNIT', species: 'goblin', id: unitId('blocked'), name: 'Blocked',
      cell: { x: MARKET_CELL.x + 2, y: MARKET_CELL.y + 2 },
    })).toEqual({ ok: false, error: { code: 'CELL_OCCUPIED' } });
    expect(session.dispatch({ type: 'PURCHASE_MINE', cell: MARKET_CELL })).toEqual({
      ok: false, error: { code: 'CELL_OCCUPIED' },
    });
    expect(session.dispatch({ type: 'PURCHASE_MINE', cell: { x: 39, y: 22 } })).toEqual({
      ok: false, error: { code: 'CELL_OCCUPIED' },
    });
    expect(session.getSnapshot().wallet.gold).toBe(1_000);
  });

  it('opens stack selection, selects a quantity, and sells it for half price', () => {
    const session = GameSession.createNew();
    const controller = new MapInteractionController(session);
    for (let index = 0; index < 3; index += 1) {
      controller.beginGoblinPlacement();
      controller.handleCellClick({ x: 2, y: 2 });
    }
    controller.handleCellClick({ x: 2, y: 2 });
    expect(controller.getViewState().stackPicker?.unitIds).toHaveLength(3);
    controller.setStackQuantity(2);
    controller.confirmStackSelection();
    expect(controller.getViewState().selectedUnitIds).toHaveLength(2);
    controller.sellSelected();
    expect(session.getSnapshot().units).toHaveLength(1);
    expect(session.getSnapshot().wallet.gold).toBe(920);
  });

  it('selects units with an RTS box', () => {
    const session = GameSession.createNew();
    for (const [id, x, y] of [['a', 1, 1], ['b', 3, 2], ['c', 8, 8]] as const) {
      session.dispatch({
        type: 'SPAWN_UNIT', species: 'goblin', id: unitId(id), name: id, cell: { x, y },
      });
    }
    const controller = new MapInteractionController(session);
    controller.handleBoxSelection({ x: 0, y: 0 }, { x: 4, y: 3 });
    expect(controller.getViewState().selectedUnitIds).toEqual([unitId('a'), unitId('b')]);
  });

  it('assigns selected units to work, hauling, and barracks through map targets', () => {
    const session = GameSession.createNew();
    const controller = new MapInteractionController(session);
    controller.beginMinePlacement();
    controller.handleCellClick({ x: 10, y: 2 });
    controller.beginGoblinPlacement();
    controller.handleCellClick({ x: 1, y: 1 });
    controller.handleCellClick({ x: 1, y: 1 });

    controller.beginWorkTarget();
    controller.handleCellClick({ x: 10, y: 2 });
    expect(session.getSnapshot().units[0]!.assignment.kind).toBe('work');

    session.dispatch({ type: 'SET_IDLE', unitIds: [unitId('goblin-1')] });
    controller.beginHaulTarget();
    controller.handleCellClick(MARKET_CELL);
    controller.handleCellClick(WAREHOUSE_CELL);
    expect(session.getSnapshot().units[0]!.assignment.kind).toBe('haul');

    controller.sendSelectedToBarracks();
    expect(session.getSnapshot().units[0]!.assignment.kind).toBe('idle');
    expect(session.getSnapshot().units[0]!.mapCell).toEqual(BARRACKS_CELL);
  });
});
