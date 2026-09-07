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

        public EconomyConfig Economy => _economy;
        public IReadOnlyList<BuildingDefinition> Buildings => _buildings;
        public IReadOnlyList<UnitDefinition> Units => _units;

        public void SetContent(EconomyConfig economy, List<BuildingDefinition> buildings, List<UnitDefinition> units)
        {
            _economy = economy;
            _buildings = buildings;
            _units = units;
        }

        public void Init(EconomyConfig economy, IEnumerable<UnitDefinition> units, IEnumerable<BuildingDefinition> buildings)
        {
            _economy = economy;
            _units = new List<UnitDefinition>(units);
            _buildings = new List<BuildingDefinition>(buildings);
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
    }
}
