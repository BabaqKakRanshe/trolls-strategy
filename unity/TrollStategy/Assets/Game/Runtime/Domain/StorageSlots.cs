using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    public readonly struct StorageSlot
    {
        public ResourceKind Resource { get; }
        public int Amount { get; }
        public bool IsEmpty => Amount <= 0;

        public StorageSlot(ResourceKind resource, int amount)
        {
            Resource = resource;
            Amount = amount;
        }
    }

    /// <summary>
    /// Splits a stockpile into fixed-size inventory slots. Each good starts a new slot and fills
    /// front to back in ResourceKind order; the last slot holds only the remaining capacity.
    /// </summary>
    public static class StorageSlots
    {
        public static int Count(int capacity, int stackSize) =>
            capacity > 0 && stackSize > 0 ? (capacity + stackSize - 1) / stackSize : 0;

        public static int SlotCapacity(int index, int capacity, int stackSize) =>
            Math.Min(stackSize, capacity - index * stackSize);

        public static StorageSlot[] Fill(IReadOnlyDictionary<ResourceKind, int> stock, int capacity, int stackSize)
        {
            int count = Count(capacity, stackSize);
            if (count == 0) return Array.Empty<StorageSlot>();

            var slots = new StorageSlot[count];
            int next = 0;
            foreach (ResourceKind resource in Enum.GetValues(typeof(ResourceKind)))
            {
                int remaining = stock != null && stock.TryGetValue(resource, out int amount) ? amount : 0;
                while (remaining > 0 && next < count)
                {
                    int take = Math.Min(SlotCapacity(next, capacity, stackSize), remaining);
                    slots[next++] = new StorageSlot(resource, take);
                    remaining -= take;
                }
            }
            return slots;
        }

        // Free room for one good: the unfilled part of its own slots plus every empty slot.
        public static int Room(IReadOnlyDictionary<ResourceKind, int> stock, ResourceKind resource, int capacity, int stackSize)
        {
            var slots = Fill(stock, capacity, stackSize);
            int room = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                int slotCapacity = SlotCapacity(i, capacity, stackSize);
                if (slots[i].IsEmpty) room += slotCapacity;
                else if (slots[i].Resource == resource) room += slotCapacity - slots[i].Amount;
            }
            return room;
        }
    }
}
