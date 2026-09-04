import type { GameState } from '../domain/model';

type Primitive = string | number | boolean | bigint | symbol | null | undefined;

export type DeepReadonly<T> = T extends Primitive
  ? T
  : T extends (...args: never[]) => unknown
  ? T
  : T extends readonly (infer TValue)[]
    ? readonly DeepReadonly<TValue>[]
    : T extends object
      ? { readonly [TKey in keyof T]: DeepReadonly<T[TKey]> }
      : T;

export type GameSnapshot = DeepReadonly<GameState & { revision: number }>;

export function createSnapshot(state: GameState, revision: number): GameSnapshot {
  return deepFreeze({
    ...structuredClone(state),
    revision,
  });
}

function deepFreeze<T>(value: T): DeepReadonly<T> {
  if (value !== null && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.values(value).forEach((nestedValue) => {
      deepFreeze(nestedValue);
    });
    Object.freeze(value);
  }

  return value as DeepReadonly<T>;
}
