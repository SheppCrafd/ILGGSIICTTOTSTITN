using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int size = 128;
    public int height = 40;

    public WorldGenerator gen;

    private Dictionary<Vector2Int, BlockType[]> cache =
        new Dictionary<Vector2Int, BlockType[]>();
    private Dictionary<Vector2Int, byte[]> compactedChunkCache =
        new Dictionary<Vector2Int, byte[]>();

    void Awake()
    {
        // SAFETY: never allow null generator
        if (gen == null)
        {
            int seed = Random.Range(0, 999999);
            Init(seed);
        }
    }

    public void Init(int seed)
    {
        gen = new WorldGenerator(seed);
        cache.Clear();
        compactedChunkCache.Clear();

        Debug.Log($"[World] INIT seed={seed}");
    }

    public BlockType[] Get(int x, int y)
    {
        if (gen == null)
        {
            int seed = Random.Range(0, 999999);
            Init(seed);
        }

        Vector2Int key = new Vector2Int(x, y);

        if (!cache.ContainsKey(key))
        {
            var col = TryLoadCompactedColumn(x, y);

            if (col == null)
                col = gen.Column(x, y, height);

            cache[key] = col;

        }

        return cache[key];
    }

    public void CompactChunk(int cx, int cy)
    {
        Vector2Int chunkKey = new Vector2Int(cx, cy);
        byte[] compacted = new byte[Chunk.SIZE * Chunk.SIZE * height];
        int index = 0;

        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                int worldX = cx * Chunk.SIZE + x;
                int worldZ = cy * Chunk.SIZE + z;
                Vector2Int columnKey = new Vector2Int(worldX, worldZ);
                BlockType[] col;

                if (worldX < 0 || worldZ < 0 || worldX >= size || worldZ >= size)
                    col = EmptyColumn();
                else if (!cache.TryGetValue(columnKey, out col))
                    col = gen.Column(worldX, worldZ, height);

                for (int y = 0; y < height; y++)
                    compacted[index++] = (byte)col[y];

                cache.Remove(columnKey);
            }
        }

        compactedChunkCache[chunkKey] = compacted;
    }

    BlockType[] TryLoadCompactedColumn(int x, int z)
    {
        Vector2Int chunkKey = new Vector2Int(
            Mathf.FloorToInt((float)x / Chunk.SIZE),
            Mathf.FloorToInt((float)z / Chunk.SIZE)
        );

        if (!compactedChunkCache.TryGetValue(chunkKey, out byte[] compacted))
            return null;

        int localX = x - chunkKey.x * Chunk.SIZE;
        int localZ = z - chunkKey.y * Chunk.SIZE;
        int index = ((localX * Chunk.SIZE) + localZ) * height;
        BlockType[] col = new BlockType[height];

        for (int y = 0; y < height; y++)
            col[y] = (BlockType)compacted[index + y];

        return col;
    }

    BlockType[] EmptyColumn()
    {
        BlockType[] col = new BlockType[height];

        for (int y = 0; y < height; y++)
            col[y] = BlockType.Air;

        return col;
    }
}
