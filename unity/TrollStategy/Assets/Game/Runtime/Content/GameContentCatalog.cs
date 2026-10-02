using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.Content
{
    [CreateAssetMenu(fileName = "GameContentCatalog", menuName = "TrollStrategy/Content/Game Content Catalog")]
    public class GameContentCatalog : ScriptableObject
    {
        [SerializeField] private EconomyConfig _economy;
        [SerializeField] private List<BuildingDefinition> _buildings = new();
        [SerializeField] private List<UnitDefinition> _units = new();
        [SerializeField] private List<BattleMissionDefinition> _missions = new();
        [SerializeField] private List<EquipmentDefinition> _equipment = new();
        [SerializeField] private List<ResourceDefinition> _resources = new();
        [Tooltip("Colony improvements bought in the haulers' guild and the barracks.")]
        [SerializeField] private List<UpgradeDefinition> _upgrades = new();
        [Tooltip("Quest chain and starting unlocks; a campaign session plays it, a sandbox session ignores it.")]
        [SerializeField] private ProgressionDefinition _progression;

        public EconomyConfig Economy => _economy;
        public ProgressionDefinition Progression => _progression;
        public IReadOnlyList<BuildingDefinition> Buildings => _buildings;
        public IReadOnlyList<UnitDefinition> Units => _units;
        public IReadOnlyList<BattleMissionDefinition> Missions => _missions;
        public IReadOnlyList<EquipmentDefinition> Equipment => _equipment;
        public IReadOnlyList<ResourceDefinition> Resources => _resources;
        public IReadOnlyList<UpgradeDefinition> Upgrades => _upgrades ?? (IReadOnlyList<UpgradeDefinition>)Array.Empty<UpgradeDefinition>();

        public void SetContent(EconomyConfig economy, List<BuildingDefinition> buildings, List<UnitDefinition> units)
        {
            _economy = economy;
            _buildings = buildings;
            _units = units;
        }

        public void Init(EconomyConfig economy, IEnumerable<UnitDefinition> units, IEnumerable<BuildingDefinition> buildings,
            IEnumerable<BattleMissionDefinition> missions = null,
            IEnumerable<EquipmentDefinition> equipment = null,
            IEnumerable<ResourceDefinition> resources = null)
        {
            _economy = economy;
            _units = new List<UnitDefinition>(units);
            _buildings = new List<BuildingDefinition>(buildings);
            _missions = missions != null ? new List<BattleMissionDefinition>(missions) : new List<BattleMissionDefinition>();
            _equipment = equipment != null ? new List<EquipmentDefinition>(equipment) : new List<EquipmentDefinition>();
            _resources = resources != null ? new List<ResourceDefinition>(resources) : new List<ResourceDefinition>();
        }

        public void SetResources(IEnumerable<ResourceDefinition> resources) =>
            _resources = new List<ResourceDefinition>(resources);

        public void SetProgression(ProgressionDefinition progression) => _progression = progression;

        public void SetUpgrades(IEnumerable<UpgradeDefinition> upgrades) =>
            _upgrades = upgrades != null ? new List<UpgradeDefinition>(upgrades) : new List<UpgradeDefinition>();

        public void SetMissions(IEnumerable<BattleMissionDefinition> missions) =>
            _missions = missions != null ? new List<BattleMissionDefinition>(missions) : new List<BattleMissionDefinition>();

        public void SetUnits(IEnumerable<UnitDefinition> units) => _units = new List<UnitDefinition>(units);

        public void SetEquipment(IEnumerable<EquipmentDefinition> equipment) =>
            _equipment = new List<EquipmentDefinition>(equipment);

        public UpgradeDefinition TryGetUpgrade(string id)
        {
            if (_upgrades == null || id == null) return null;
            foreach (var upgrade in _upgrades)
                if (upgrade != null && upgrade.Id == id) return upgrade;
            return null;
        }

        public UnitDefinition TryGetUnit(UnitKind kind)
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i] != null && _units[i].Kind == kind) return _units[i];
            return null;
        }

        public BattleMissionDefinition TryGetMission(string id)
        {
            foreach (var mission in _missions)
                if (mission != null && mission.MissionId == id) return mission;
            return null;
        }

        public BuildingDefinition GetBuilding(BuildingKind kind)
        {
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i] != null && _buildings[i].Kind == kind)
                    return _buildings[i];
            }
            throw new ArgumentOutOfRangeException(nameof(kind), $"Building definition for {kind} not found");
        }

        public UnitDefinition GetUnit(UnitKind kind)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i] != null && _units[i].Kind == kind)
                    return _units[i];
            }
            throw new ArgumentOutOfRangeException(nameof(kind), $"Unit definition for {kind} not found");
        }

        public ResourceDefinition GetResource(ResourceKind kind) =>
            TryGetResource(kind) ?? throw new ArgumentOutOfRangeException(nameof(kind), $"Resource definition for {kind} not found");

        public ResourceDefinition TryGetResource(ResourceKind kind)
        {
            for (int i = 0; i < _resources.Count; i++)
            {
                if (_resources[i] != null && _resources[i].Kind == kind)
                    return _resources[i];
            }
            return null;
        }

        public BattleMissionDefinition GetMission(string id)
        {
            foreach (var mission in _missions)
                if (mission != null && mission.MissionId == id) return mission;
            throw new ArgumentOutOfRangeException(nameof(id), $"Mission definition for {id} not found");
        }

        public EquipmentDefinition GetEquipment(string id)
        {
            foreach (var item in _equipment)
                if (item != null && item.ItemId == id) return item;
            throw new ArgumentOutOfRangeException(nameof(id), $"Equipment definition for {id} not found");
        }
    }
}
