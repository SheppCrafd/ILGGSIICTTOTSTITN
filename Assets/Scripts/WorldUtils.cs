using UnityEngine;

public static class WorldUtils
{
    public const int DirtDepth = 3;

    public static bool IsInBounds(int x, int z, int worldSize)
    {
        return x >= 0 && z >= 0 && x < worldSize && z < worldSize;
    }

    public static int ColumnHeight(BlockType[] col)
    {
        for (int y = col.Length - 1; y >= 0; y--)
        {
            if (col[y] != BlockType.Air)
                return y + 1;
        }

        return 0;
    }

    public static BlockType BlockTypeAtDepth(int columnHeight, int y)
    {
        int depthFromTop = columnHeight - 1 - y;

        if (depthFromTop == 0)
            return BlockType.Grass;

        if (depthFromTop <= DirtDepth)
            return BlockType.Dirt;

        return BlockType.Stone;
    }

    public static int ToChunkCoord(int worldCoord)
    {
        return Mathf.FloorToInt((float)worldCoord / Chunk.SIZE);
    }

    public static int ToChunkCoord(float worldCoord)
    {
        return Mathf.FloorToInt(worldCoord / Chunk.SIZE);
    }

    public static Material CreateFallbackMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError($"[WorldUtils] 'Standard' shader not found when creating fallback material '{name}'.");
            shader = Shader.Find("Hidden/InternalErrorShader");
        }
        Material material = new Material(shader);
        material.name = name;
        material.color = color;
        return material;
    }
}
