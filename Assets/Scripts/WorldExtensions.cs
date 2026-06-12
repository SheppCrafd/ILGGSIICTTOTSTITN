using UnityEngine;

// Extension methods for World to provide block-level access without modifying original World.cs
public static class WorldExtensions
{
    public static bool IsInBlockBounds(this World world, int x, int y, int z)
    {
        if (world == null) return false;
        if (x < 0 || z < 0 || x >= world.size || z >= world.size) return false;
        if (y < 0 || y >= world.height) return false;
        return true;
    }

    public static BlockType GetBlock(this World world, int x, int y, int z)
    {
        if (world == null) return BlockType.Air;
        if (!world.IsInBlockBounds(x, y, z)) return BlockType.Air;
        var col = world.Get(x, z);
        if (col == null || y < 0 || y >= col.Length) return BlockType.Air;
        return col[y];
    }

    public static bool SetBlock(this World world, int x, int y, int z, BlockType type)
    {
        if (world == null) return false;
        if (!world.IsInBlockBounds(x, y, z)) return false;
        var col = world.Get(x, z);
        if (col == null || y < 0 || y >= col.Length) return false;
        col[y] = type;
        return true;
    }

    public static bool TryBreakBlock(this World world, int x, int y, int z, out BlockType broken, out Vector3 dropPosition)
    {
        broken = BlockType.Air;
        dropPosition = Vector3.zero;

        if (world == null) return false;
        if (!world.IsInBlockBounds(x, y, z)) return false;

        var current = world.GetBlock(x, y, z);
        if (current == BlockType.Air) return false;

        // set to air
        world.SetBlock(x, y, z, BlockType.Air);

        broken = current;
        dropPosition = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
        return true;
    }

    public static bool TryPlaceBlock(this World world, int x, int y, int z, BlockType type)
    {
        if (world == null) return false;
        if (!world.IsInBlockBounds(x, y, z)) return false;
        if (world.GetBlock(x, y, z) != BlockType.Air) return false;
        world.SetBlock(x, y, z, type);
        return true;
    }
}
