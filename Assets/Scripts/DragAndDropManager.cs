using UnityEngine;

// Central, static drag-and-drop state shared between IMGUI inventory and Canvas hotbar UI.
public static class DragAndDropManager
{
    public static bool IsDragging = false;
    public static ItemStack DragStack = null;
    public static int SourceIndex = -1; // index in inventory.slots
    public static bool SourceIsHotbar = false;

    // Visuals
    public static Texture2D DragTexture = null;
    public static Color DragColor = Color.white;

    // Start dragging: remove stack from source index and store a copy
    public static void StartDrag(Inventory inv, int sourceIndex, bool sourceIsHotbar, int amount, Texture2D texture = null, Color color = default)
    {
        if (inv == null) return;
        var s = inv.GetSlot(sourceIndex);
        if (s == null || amount <= 0) return;

        int take = Mathf.Clamp(amount, 1, s.count);
        // copy only 'take' amount
        DragStack = new ItemStack(s.item, take);
        // remove 'take' from source; if emptied, null out
        if (s.count > take)
        {
            s.count -= take;
            inv.slots[sourceIndex] = s;
        }
        else
        {
            inv.slots[sourceIndex] = null;
        }

        SourceIndex = sourceIndex;
        SourceIsHotbar = sourceIsHotbar;
        IsDragging = true;
        DragTexture = texture;
        DragColor = color == default ? Color.white : color;
    }

    // Cancel the drag: try restore to source index, else add back into inventory (best-effort)
    public static void CancelDrag(Inventory inv)
    {
        if (!IsDragging || inv == null || DragStack == null) return;

        var src = inv.GetSlot(SourceIndex);
        if (src == null)
        {
            inv.slots[SourceIndex] = DragStack;
        }
        else if (src.item != null && src.item.id == DragStack.item.id)
        {
            int space = src.item.maxStack - src.count;
            int move = Mathf.Min(space, DragStack.count);
            src.count += move;
            DragStack.count -= move;
            if (DragStack.count > 0)
            {
                int leftover = inv.AddItem(DragStack.item, DragStack.count);
                if (leftover > 0)
                    Debug.LogWarning("[DragDrop] CancelDrag: inventory full, dropped leftover items");
            }
        }
        else
        {
            int leftover = inv.AddItem(DragStack.item, DragStack.count);
            if (leftover > 0)
            {
                Debug.LogWarning("[DragDrop] CancelDrag: inventory full, dropped leftover items");
            }
        }

        Clear();
    }

    // Drop into a specific inventory slot index (full inventory indexing). Handles merge/swap behavior.
    public static void DropToSlot(Inventory inv, int targetIndex)
    {
        if (!IsDragging || inv == null || DragStack == null) return;

        var target = inv.GetSlot(targetIndex);

        // Empty target -> move
        if (target == null)
        {
            inv.slots[targetIndex] = DragStack;
            Clear();
            return;
        }

        // Same item id -> merge as much as possible, leftover goes back to source (or new stack at targetIndex if swap necessary)
        if (target.item != null && target.item.id == DragStack.item.id)
        {
            int space = target.item.maxStack - target.count;
            int move = Mathf.Min(space, DragStack.count);
            target.count += move;
            DragStack.count -= move;

            if (DragStack.count <= 0)
            {
                Clear();
                return;
            }
            else
            {
                // leftover: try put back to original source index if empty
                if (inv.GetSlot(SourceIndex) == null)
                {
                    inv.slots[SourceIndex] = DragStack;
                }
                else
                {
                    int leftover = inv.AddItem(DragStack.item, DragStack.count);
                    if (leftover > 0)
                        Debug.LogWarning("[DragDrop] DropToSlot: inventory couldn't accept leftover items");
                }
                Clear();
                return;
            }
        }

        // Different item -> swap
        inv.slots[targetIndex] = DragStack;
        // put target back where source was (if still available) otherwise attempt to add to inventory
        if (inv.GetSlot(SourceIndex) == null)
        {
            inv.slots[SourceIndex] = target;
        }
        else
        {
            int leftover = inv.AddItem(target.item, target.count);
            if (leftover > 0)
                Debug.LogWarning("[DragDrop] DropToSlot: couldn't add swapped item back to inventory (leftover)");
        }

        Clear();
    }

    static void Clear()
    {
        IsDragging = false;
        DragStack = null;
        SourceIndex = -1;
        SourceIsHotbar = false;
        DragTexture = null;
        DragColor = Color.white;
    }
}
