using System;
using UnityEngine;

namespace TrollStrategy.Content
{
    // Values are serialized by index: append new kinds, never reorder.
    public enum UpgradeEffect
    {
        /// <summary>Creatures walk faster in the colony, in percent.</summary>
        WalkSpeedPercent,
        /// <summary>Haulers lift more per trip: percent added to their stamina.</summary>
        CarryPercent,
        /// <summary>Loading and unloading take less time, in percent.</summary>
        HandlingTimePercent,
        /// <summary>More haulers load at a door at once.</summary>
        LoadersPerDoor,
        /// <summary>More fighters go into a battle.</summary>
        SquadSize,
        /// <summary>The colony's fighters have more health, in percent.</summary>
        FighterHealthPercent,
        /// <summary>The colony's fighters deal more damage per hit.</summary>
        FighterDamage,
        /// <summary>Arena missions recover faster, in percent.</summary>
        BattleCooldownPercent,
        /// <summary>Won battles pay more gold, in percent.</summary>
        BattleRewardPercent
    }

    /// <summary>
    /// A colony improvement bought level by level in its host building (the haulers' guild, the barracks).
    /// Each level adds <see cref="AmountPerLevel"/> of its effect for the whole colony.
    /// </summary>
    [Serializable]
    public sealed class UpgradeDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [Tooltip("What it changes, in words the player can act on.")]
        [SerializeField, TextArea] private string _description;
        [Tooltip("Building where it is bought; the colony must have one.")]
        [SerializeField] private BuildingKind _host;
        [SerializeField] private UpgradeEffect _effect;
        [SerializeField] private int _amountPerLevel = 1;
        [Tooltip("Gold for each level, in order; the count is the highest level.")]
        [SerializeField] private int[] _costs = Array.Empty<int>();

        // for the serializer
        private UpgradeDefinition() { }

        public UpgradeDefinition(string id, string displayName, string description, BuildingKind host,
            UpgradeEffect effect, int amountPerLevel, int[] costs)
        {
            _id = id;
            _displayName = displayName;
            _description = description;
            _host = host;
            _effect = effect;
            _amountPerLevel = amountPerLevel;
            _costs = costs ?? Array.Empty<int>();
        }

        public string Id => _id ?? string.Empty;
        public string DisplayName => _displayName ?? string.Empty;
        public string Description => _description ?? string.Empty;
        public BuildingKind Host => _host;
        public UpgradeEffect Effect => _effect;
        public int AmountPerLevel => _amountPerLevel;
        public int MaxLevel => _costs?.Length ?? 0;

        /// <summary>Gold for raising the upgrade from <paramref name="level"/> to the next one; -1 at the top.</summary>
        public int CostFrom(int level) => level >= 0 && level < MaxLevel ? _costs[level] : -1;
    }
}
