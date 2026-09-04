export type CommandErrorCode =
  | 'NOT_FOUND'
  | 'INSUFFICIENT_GOLD'
  | 'INSUFFICIENT_RESOURCES'
  | 'INVALID_ASSIGNMENT'
  | 'INVALID_EQUIPMENT'
  | 'INVALID_AMOUNT'
  | 'INVALID_FORMATION'
  | 'INVALID_MISSION'
  | 'MISSION_UNAVAILABLE'
  | 'CAPACITY_EXCEEDED'
  | 'DUPLICATE_ID'
  | 'ALREADY_APPLIED'
  | 'INVALID_SAVE'
  | 'UNSUPPORTED_SCHEMA';

export interface CommandError {
  code: CommandErrorCode;
}

export type CommandResult<T> =
  | { ok: true; value: T }
  | { ok: false; error: CommandError };
