import {
  GameSession,
  type GameCommandContractMap,
  type GameCommandOf,
  type GameCommandType,
} from '../application/GameSession';
import type { CommandResult } from '../domain/results';
import type { SavePort } from './autosave';
import { encodeSave } from './saveCodec';

const PERIODIC_SAVE_INTERVAL_MS = 10_000;

export class AutosaveCoordinator {
  private simulatedSinceSaveMs = 0;

  constructor(
    private readonly session: GameSession,
    private readonly savePort: SavePort,
  ) {}

  dispatch<TType extends GameCommandType>(
    command: GameCommandOf<TType>,
  ): CommandResult<GameCommandContractMap[TType]['value']> {
    const result = this.session.dispatch(command);
    if (result.ok) {
      this.flush();
    }
    return result;
  }

  advance(realDeltaMs: number): void {
    const before = this.session.getSimulationTimeMs();
    this.session.advance(realDeltaMs);
    const elapsed = this.session.getSimulationTimeMs() - before;
    this.simulatedSinceSaveMs += elapsed;

    if (this.simulatedSinceSaveMs >= PERIODIC_SAVE_INTERVAL_MS) {
      this.flush();
    }
  }

  flush(): void {
    this.savePort.save(encodeSave(this.session.exportState()));
    this.simulatedSinceSaveMs = 0;
  }

  getSession(): GameSession {
    return this.session;
  }
}
