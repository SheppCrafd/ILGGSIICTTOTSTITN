using System;
using UnityEngine;

/// <summary>
/// Core item and inventory backend (no UI).
/// </summary>
[Serializable]
public class Item
{
    public string id;
    public string name;
    public int maxStack = 64;

    public Item(string id, string name, int maxStack = 64)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("id must be non-empty", nameof(id));
        this.id = id;
        this.name = name ?? string.Empty;
        this.maxStack = Math.Max(1, maxStack);
    }
}

[Serializable]
public class ItemStack
{
    public Item item;
    public int count;

    public ItemStack(Item item, int count)
    {
        this.item = item ?? throw new ArgumentNullException(nameof(item));
        // clamp count to [0, item.maxStack]
        this.count = Mathf.Clamp(count, 0, item.maxStack);
    }
}

/// <summary>
/// Simple inventory with 27 slots. First 9 slots are the hotbar (view into inventory).
/// AddItem stacks into existing stacks first, then fills empty slots.
/// Returns leftover amount that could not be added.
/// </summary>
public class Inventory
{
    public const int DefaultSlotCount = 27;
    public const int DefaultHotbarCount = 9;

    public int SlotCount => slots.Length;
    public int HotbarSlotCount => hotbarCount;

    public ItemStack[] slots;
    int hotbarCount = DefaultHotbarCount;

    // Selected hotbar index (0..hotbarCount-1)
    int selectedHotbarIndex = 0;
    public int SelectedHotbarIndex
    {
        get => selectedHotbarIndex;
        private set => selectedHotbarIndex = Mathf.Clamp(value, 0, Math.Max(0, hotbarCount - 1));
    }

    public Inventory(int totalSlots = DefaultSlotCount, int hotbarSlots = DefaultHotbarCount)
    {
        hotbarCount = Mathf.Clamp(hotbarSlots, 1, totalSlots);
        slots = new ItemStack[totalSlots];
    }

    // Adds up to 'amount' of the given item. Returns leftover amount that could not be stored.
    public int AddItem(Item item, int amount)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (amount <= 0) return 0;

        int remaining = amount;

        // First, try to fill existing stacks of the same item
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            var stack = slots[i];
            if (stack != null && stack.item.id == item.id && stack.count < item.maxStack)
            {
                int space = item.maxStack - stack.count;
                int take = Math.Min(space, remaining);
                stack.count += take;
                remaining -= take;
            }
        }

        // Then, create new stacks in empty slots
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i] == null)
            {
                int put = Math.Min(item.maxStack, remaining);
                slots[i] = new ItemStack(item, put);
                remaining -= put;
            }
        }

        return remaining;
    }

    // True if inventory cannot accept any additional items (all slots filled and all stacks at max)
    public bool IsFull()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == null) return false;
            if (s.count < s.item.maxStack) return false;
        }
        return true;
    }

    // Returns total count of the given item in the inventory
    public int GetTotalItems(Item item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        int total = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s != null && s.item.id == item.id)
                total += s.count;
        }
        return total;
    }

    // Hotbar helpers
    public ItemStack GetSelectedItem()
    {
        if (slots == null || slots.Length == 0) return null;
        int idx = Mathf.Clamp(SelectedHotbarIndex, 0, hotbarCount - 1);
        return slots[idx];
    }

    public void CycleHotbar(int direction)
    {
        if (hotbarCount <= 0) return;
        int idx = SelectedHotbarIndex + direction;
        if (idx < 0) idx = hotbarCount - 1;
        if (idx >= hotbarCount) idx = 0;
        SelectedHotbarIndex = idx;
    }

    public void SelectHotbarSlot(int index)
    {
        if (hotbarCount <= 0) return;
        SelectedHotbarIndex = Mathf.Clamp(index, 0, hotbarCount - 1);
    }

    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return null;
        return slots[index];
    }

    // --- Backwards compatibility helpers for block-based code ---
    // Map BlockType -> synthetic Item instances so old block code can reuse the item inventory.
    static readonly System.Collections.Generic.Dictionary<BlockType, Item> blockItemMap =
        new System.Collections.Generic.Dictionary<BlockType, Item>();

    Item BlockItemFor(BlockType type)
    {
        if (!blockItemMap.TryGetValue(type, out Item it))
        {
            it = new Item($"block_{type}", type.ToString(), 64);
            blockItemMap[type] = it;
        }
        return it;
    }

    // Add using BlockType (old API)
    public int Add(BlockType type, int amount)
    {
        return AddItem(BlockItemFor(type), amount);
    }

    // Try remove from slot index (old API)
    public bool TryRemoveFromSlot(int index, int amount)
    {
        if (index < 0 || index >= slots.Length) return false;
        var s = slots[index];
        if (s == null || s.count <= 0) return false;
        // Only allow removing if this stack represents a block
        if (!s.item.id.StartsWith("block_")) return false;
        if (amount <= 0) return true;
        if (s.count < amount) return false;
        s.count -= amount;
        if (s.count <= 0) slots[index] = null;
        return true;
    }

}
