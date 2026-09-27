using System;

namespace TrollStrategy.Domain
{
    [Serializable]
    public sealed class EquipmentState
    {
        public string Id { get; set; }
        public string DefinitionId { get; set; }
        public string OwnerUnitId { get; set; }

        public EquipmentState Clone() => new()
        {
            Id = Id,
            DefinitionId = DefinitionId,
            OwnerUnitId = OwnerUnitId
        };
    }

    public readonly struct BattleEquipmentAssignment
    {
        public string ItemId { get; }
        public string OwnerUnitId { get; }

        public BattleEquipmentAssignment(string itemId, string ownerUnitId)
        {
            ItemId = itemId;
            OwnerUnitId = ownerUnitId;
        }
    }
}
