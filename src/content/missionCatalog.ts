export const MISSION_ONE = {
  id: 'mission-1',
  title: 'Местные бандиты',
  objective: 'Бандиты перекрыли дорогу к поселению. Соберите свободный отряд и выберите позиции.',
  difficulty: 'Средняя',
  enemySpecies: 'goblin',
  enemyLabel: 'гоблина',
  enemyCells: [
    { x: 1, y: 0 },
    { x: 4, y: 0 },
    { x: 1, y: 1 },
    { x: 4, y: 1 },
  ],
  maxFormationSize: 4,
  maxDurationMs: 120_000,
  cooldownMs: 120_000,
} as const;
