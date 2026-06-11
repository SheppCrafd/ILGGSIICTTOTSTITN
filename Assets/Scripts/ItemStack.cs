public class ItemStack
{
    public BlockType type = BlockType.Air;
    public int amount;

    public bool IsEmpty => amount <= 0 || type == BlockType.Air;

    public void Clear()
    {
        type = BlockType.Air;
        amount = 0;
    }
}
