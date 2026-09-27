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
    }

    [CreateAssetMenu(fileName = "BattleMission", menuName = "TrollStrategy/Content/Battle Mission")]
    public sealed class BattleMissionDefinition : ScriptableObject
    {
        [SerializeField] private string _missionId = "mission-1";
        [SerializeField] private string _displayName = "Первый бой";
        [SerializeField, Min(3)] private int _width = 9;
        [SerializeField, Min(3)] private int _height = 5;
        [SerializeField, Min(1)] private int _maxPlayerUnits = 4;
        [SerializeField] private List<Cell> _playerDeployment = new();
        [SerializeField] private List<Cell> _enemyDeployment = new();
        [SerializeField] private List<Cell> _blockedCells = new();
        [SerializeField] private List<BattleEnemyStart> _enemies = new();
        [SerializeField, Min(0)] private int _firstWinGold = 250;
        [SerializeField, Min(0)] private int _repeatWinGold = 75;
        [SerializeField, Min(0f)] private float _unlockAfterActiveSeconds = 120f;
        [SerializeField, Min(0f)] private float _cooldownActiveSeconds = 120f;

        [Header("Optional visual replacements")]
        [SerializeField] private GameObject _environmentPrefab;
        [SerializeField] private GameObject _obstaclePrefab;

        public string MissionId => _missionId;
        public string DisplayName => _displayName;
        public int Width => _width;
        public int Height => _height;
        public int MaxPlayerUnits => _maxPlayerUnits;
        public IReadOnlyList<Cell> PlayerDeployment => _playerDeployment;
        public IReadOnlyList<Cell> EnemyDeployment => _enemyDeployment;
        public IReadOnlyList<Cell> BlockedCells => _blockedCells;
        public IReadOnlyList<BattleEnemyStart> Enemies => _enemies;
        public int FirstWinGold => _firstWinGold;
        public int RepeatWinGold => _repeatWinGold;
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
        }
    }
}
