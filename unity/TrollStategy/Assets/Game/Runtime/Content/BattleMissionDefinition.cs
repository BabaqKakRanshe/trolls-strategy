using System;
using System.Collections.Generic;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Content
{
    [Serializable]
    public struct BattleEnemyStart
    {
        public UnitKind Kind;
        public Cell Cell;
        [Tooltip("Снаряжение врага: id предметов каталога, по одному на слот. Действует в бою, как у бойцов колонии.")]
        public string[] Gear;
        [Tooltip("Чемпион вехи: здоровье умножается на процент чемпиона уровня.")]
        public bool Champion;

        /// <summary>The item ids the enemy wears; empty when it wears none.</summary>
        public IReadOnlyList<string> GearIds => Gear ?? Array.Empty<string>();
    }

    [CreateAssetMenu(fileName = "BattleMission", menuName = "TrollStrategy/Content/Battle Mission")]
    public sealed class BattleMissionDefinition : ScriptableObject
    {
        [SerializeField] private string _missionId = "mission-1";
        [SerializeField] private string _displayName = "Первый бой";
        [Tooltip("Place on the arena ladder, from 1; a win opens the mission one level higher.")]
        [SerializeField, Min(1)] private int _level = 1;
        [Header("Сила противников")]
        [Tooltip("Здоровье врагов в процентах от их вида.")]
        [SerializeField, Min(10)] private int _enemyHealthPercent = 100;
        [Tooltip("Прибавка к урону каждого врага.")]
        [SerializeField, Min(0)] private int _enemyDamageBonus;
        [Tooltip("Прибавка к броне каждого врага.")]
        [SerializeField, Min(0)] private int _enemyArmorBonus;
        [Tooltip("Первая победа открывает найм этого существа в колонию.")]
        [SerializeField] private bool _unlocksUnit;
        [SerializeField] private UnitKind _unlockUnit;
        [Header("Лестница")]
        [Tooltip("Как враги стоят в своей зоне; соседние уровни стоят по-разному.")]
        [SerializeField] private BattleFormation _formation;
        [Tooltip("Уровень-веха: на нём чемпион, окно арены отмечает его звездой.")]
        [SerializeField] private bool _milestone;
        [Tooltip("Здоровье чемпиона в процентах от здоровья врагов уровня; 0 — чемпиона нет.")]
        [SerializeField, Min(0)] private int _championHealthPercent;
        [Tooltip("Окружение уровня; его вид — префаб окружения ниже.")]
        [SerializeField] private ArenaBiome _biome;
        [SerializeField, Min(3)] private int _width = 9;
        [SerializeField, Min(3)] private int _height = 5;
        [SerializeField, Min(1)] private int _maxPlayerUnits = 4;
        [SerializeField] private List<Cell> _playerDeployment = new();
        [SerializeField] private List<Cell> _enemyDeployment = new();
        [SerializeField] private List<Cell> _blockedCells = new();
        [SerializeField] private List<BattleEnemyStart> _enemies = new();
        [Header("Награда за победу: случайная сумма в диапазоне, игрок узнаёт её после боя")]
        [Tooltip("Меньше всего золота за первую победу.")]
        [SerializeField, Min(0)] private int _firstWinGold = 250;
        [Tooltip("Больше всего золота за первую победу; меньше минимума — всегда минимум.")]
        [SerializeField, Min(0)] private int _firstWinGoldMax;
        [Tooltip("Меньше всего золота за повторную победу.")]
        [SerializeField, Min(0)] private int _repeatWinGold = 75;
        [Tooltip("Больше всего золота за повторную победу; меньше минимума — всегда минимум.")]
        [SerializeField, Min(0)] private int _repeatWinGoldMax;
        [Tooltip("Трофеи каждой победы: товары, которые приходят в бараки вместе с золотом.")]
        [SerializeField] private ResourceAmount[] _winGoods = Array.Empty<ResourceAmount>();
        [SerializeField, Min(0f)] private float _unlockAfterActiveSeconds = 120f;
        [SerializeField, Min(0f)] private float _cooldownActiveSeconds = 120f;

        [Header("Optional visual replacements")]
        [SerializeField] private GameObject _environmentPrefab;
        [SerializeField] private GameObject _obstaclePrefab;

        public string MissionId => _missionId;
        public string DisplayName => _displayName;
        public int Level => Math.Max(1, _level);
        public int EnemyHealthPercent => Math.Max(10, _enemyHealthPercent);
        public int EnemyDamageBonus => Math.Max(0, _enemyDamageBonus);
        public int EnemyArmorBonus => Math.Max(0, _enemyArmorBonus);
        /// <summary>The creature a first win here opens for hire, or null.</summary>
        public UnitKind? UnlockUnit => _unlocksUnit ? _unlockUnit : (UnitKind?)null;
        public BattleFormation Formation => _formation;
        /// <summary>A milestone of the ladder: its champion is met nowhere else.</summary>
        public bool Milestone => _milestone;
        /// <summary>The champion's health against the level's other enemies, in percent; 0 when there is none.</summary>
        public int ChampionHealthPercent => Math.Max(0, _championHealthPercent);
        public ArenaBiome Biome => _biome;
        public int Width => _width;
        public int Height => _height;
        public int MaxPlayerUnits => _maxPlayerUnits;
        public IReadOnlyList<Cell> PlayerDeployment => _playerDeployment;
        public IReadOnlyList<Cell> EnemyDeployment => _enemyDeployment;
        public IReadOnlyList<Cell> BlockedCells => _blockedCells;
        public IReadOnlyList<BattleEnemyStart> Enemies => _enemies;
        /// <summary>The least gold a first win pays; the win rolls up to <see cref="FirstWinGoldMax"/>.</summary>
        public int FirstWinGold => _firstWinGold;
        public int FirstWinGoldMax => Math.Max(_firstWinGold, _firstWinGoldMax);
        /// <summary>The least gold a repeat win pays; the win rolls up to <see cref="RepeatWinGoldMax"/>.</summary>
        public int RepeatWinGold => _repeatWinGold;
        public int RepeatWinGoldMax => Math.Max(_repeatWinGold, _repeatWinGoldMax);
        /// <summary>Goods every win here brings to the barracks with its gold: the arena's trophies.</summary>
        public IReadOnlyList<ResourceAmount> WinGoods => _winGoods ?? Array.Empty<ResourceAmount>();
        public float UnlockAfterActiveSeconds => _unlockAfterActiveSeconds;
        public float CooldownActiveSeconds => _cooldownActiveSeconds;
        public GameObject EnvironmentPrefab => _environmentPrefab;
        public GameObject ObstaclePrefab => _obstaclePrefab;

        public BattleBoard CreateBoard()
        {
            if (string.IsNullOrWhiteSpace(_missionId))
                throw new InvalidOperationException("У миссии нет ID");
            var board = new BattleBoard(_width, _height, _blockedCells, _playerDeployment);
            if (_maxPlayerUnits < 1 || _maxPlayerUnits > board.DeploymentCells.Count)
                throw new InvalidOperationException("Неверный размер отряда для зоны расстановки");
            if (_enemies == null || _enemies.Count == 0)
                throw new InvalidOperationException("У миссии нет противников");

            var enemyDeployment = new HashSet<Cell>();
            foreach (var cell in _enemyDeployment)
            {
                if (!board.IsWalkable(cell) || board.CanPlace(cell) || !enemyDeployment.Add(cell))
                    throw new InvalidOperationException($"Неверная клетка зоны врага {cell}");
            }
            var occupied = new HashSet<Cell>();
            foreach (var enemy in _enemies)
            {
                if (!board.IsWalkable(enemy.Cell) || board.CanPlace(enemy.Cell) ||
                    (_enemyDeployment.Count > 0 && !enemyDeployment.Contains(enemy.Cell)) ||
                    !occupied.Add(enemy.Cell))
                    throw new InvalidOperationException($"Неверная стартовая клетка врага {enemy.Cell}");
            }
            return board;
        }

        public void SetDesign(string id, string name, int width, int height, int maxPlayerUnits,
            IEnumerable<Cell> deployment, IEnumerable<Cell> blocked, IEnumerable<BattleEnemyStart> enemies,
            IEnumerable<Cell> enemyDeployment = null)
        {
            _missionId = id;
            _displayName = name;
            _width = width;
            _height = height;
            _maxPlayerUnits = maxPlayerUnits;
            _playerDeployment = new List<Cell>(deployment);
            _enemyDeployment = enemyDeployment != null ? new List<Cell>(enemyDeployment) : new List<Cell>();
            _blockedCells = new List<Cell>(blocked);
            _enemies = new List<BattleEnemyStart>(enemies);
        }

        public void SetTimingAndRewards(float unlockAfterSeconds, float cooldownSeconds,
            int firstWinGold, int repeatWinGold)
        {
            _unlockAfterActiveSeconds = unlockAfterSeconds;
            _cooldownActiveSeconds = cooldownSeconds;
            _firstWinGold = firstWinGold;
            _repeatWinGold = repeatWinGold;
            _firstWinGoldMax = firstWinGold;
            _repeatWinGoldMax = repeatWinGold;
        }

        /// <summary>The mission's place on the arena ladder, how strong its enemies are and whom its first win opens.</summary>
        public void SetArena(int level, int enemyHealthPercent, int enemyDamageBonus, int enemyArmorBonus,
            UnitKind? unlockUnit)
        {
            _level = Math.Max(1, level);
            _enemyHealthPercent = Math.Max(10, enemyHealthPercent);
            _enemyDamageBonus = Math.Max(0, enemyDamageBonus);
            _enemyArmorBonus = Math.Max(0, enemyArmorBonus);
            _unlocksUnit = unlockUnit.HasValue;
            _unlockUnit = unlockUnit ?? default;
        }

        public void SetEnvironment(GameObject environmentPrefab) => _environmentPrefab = environmentPrefab;

        /// <summary>What sets the level apart on the ladder: its formation, whether it is a milestone and its champion.</summary>
        public void SetLadderTraits(BattleFormation formation, bool milestone, int championHealthPercent)
        {
            _formation = formation;
            _milestone = milestone;
            _championHealthPercent = Math.Max(0, championHealthPercent);
        }

        public void SetBiome(ArenaBiome biome) => _biome = biome;

        /// <summary>Lets a win pay a surprise amount between the minimum and these maximums.</summary>
        public void SetRewardRanges(int firstWinGoldMax, int repeatWinGoldMax)
        {
            _firstWinGoldMax = firstWinGoldMax;
            _repeatWinGoldMax = repeatWinGoldMax;
        }

        public void SetWinGoods(params ResourceAmount[] goods) => _winGoods = goods ?? Array.Empty<ResourceAmount>();
    }
}
