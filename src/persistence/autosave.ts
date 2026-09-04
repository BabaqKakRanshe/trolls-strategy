export const SAVE_STORAGE_KEY = 'troll-strategy.save.v1';

export interface SavePort {
  load(): string | null;
  save(serialized: string): void;
  clear(): void;
}

interface BrowserStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export class BrowserSaveStore implements SavePort {
  constructor(private readonly storage: BrowserStorage = globalThis.localStorage) {}

  load(): string | null {
    return this.storage.getItem(SAVE_STORAGE_KEY);
  }

  save(serialized: string): void {
    this.storage.setItem(SAVE_STORAGE_KEY, serialized);
  }

  clear(): void {
    this.storage.removeItem(SAVE_STORAGE_KEY);
  }
}
