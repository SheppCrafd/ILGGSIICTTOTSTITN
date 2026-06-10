using UnityEngine;

public class WorldGenerator
{
    const int BaseHeight = 12;
    const float HillHeight = 10f;
    const float DetailHeight = 2f;

    private int seed;

    public WorldGenerator(int seed)
    {
        this.seed = seed;
    }

    float Noise(float x, float y)
    {
        float nx = (x + seed) * 0.018f;
        float ny = (y + seed) * 0.018f;
        return Mathf.PerlinNoise(nx, ny) * 2f - 1f;
    }

    float Noise2(float x, float y)
    {
        float nx = (x + seed + 100) * 0.055f;
        float ny = (y + seed + 100) * 0.055f;
        return Mathf.PerlinNoise(nx, ny) * 2f - 1f;
    }

    public int Height(int x, int y)
    {
        float n = Noise(x, y);
        float n2 = Noise2(x, y);
        float height = BaseHeight + n * HillHeight + n2 * DetailHeight;

        return Mathf.RoundToInt(height);
    }

    public BlockType[] Column(int x, int y, int maxHeight)
    {
        int h = Mathf.Clamp(Height(x, y), 1, maxHeight);
        BlockType[] col = new BlockType[maxHeight];

        for (int z = 0; z < maxHeight; z++)
        {
            if (z < h)
                col[z] = WorldUtils.BlockTypeAtDepth(h, z);
            else
                col[z] = BlockType.Air;
        }

        return col;
    }
}