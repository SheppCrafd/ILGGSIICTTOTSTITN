public class PlayerInventory
{
    public const int MaxStackSize = 64;

    readonly ItemStack[] slots;

    public int HotbarSlotCount { get; }
    public int SlotCount => slots.Length;

    public PlayerInventory(int hotbarSlotCount, int inventorySlotCount)
    {
        HotbarSlotCount = hotbarSlotCount;
        slots = new ItemStack[hotbarSlotCount + inventorySlotCount];

        for (int i = 0; i < slots.Length; i++)
            slots[i] = new ItemStack();
    }

    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    public int Add(BlockType type, int amount)
    {
        if (type == BlockType.Air || amount <= 0)
            return amount;

        amount = FillExistingStacks(type, amount, 0, HotbarSlotCount);
        amount = FillEmptySlots(type, amount, 0, HotbarSlotCount);
        amount = FillExistingStacks(type, amount, HotbarSlotCount, slots.Length);
        amount = FillEmptySlots(type, amount, HotbarSlotCount, slots.Length);
        return amount;
    }

    public bool TryRemoveFromSlot(int index, int amount)
    {
        ItemStack stack = GetSlot(index);

        if (stack == null || stack.IsEmpty || stack.amount < amount)
            return false;

        stack.amount -= amount;

        if (stack.amount <= 0)
            stack.Clear();

        return true;
    }

    int FillExistingStacks(BlockType type, int amount, int start, int end)
    {
        for (int i = start; i < end && amount > 0; i++)
        {
            ItemStack stack = slots[i];

            if (stack.IsEmpty || stack.type != type || stack.amount >= MaxStackSize)
                continue;

            int added = System.Math.Min(MaxStackSize - stack.amount, amount);
            stack.amount += added;
            amount -= added;
        }

        return amount;
    }

    int FillEmptySlots(BlockType type, int amount, int start, int end)
    {
        for (int i = start; i < end && amount > 0; i++)
        {
            ItemStack stack = slots[i];

            if (!stack.IsEmpty)
                continue;

            int added = System.Math.Min(MaxStackSize, amount);
            stack.type = type;
            stack.amount = added;
            amount -= added;
        }

        return amount;
    }
}
