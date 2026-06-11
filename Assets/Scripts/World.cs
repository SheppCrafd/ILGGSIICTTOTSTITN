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
#if UNITY_EDITOR
    private HashSet<Vector2Int> loggedCompactedChunkLoads =
        new HashSet<Vector2Int>();
#endif

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
#if UNITY_EDITOR
        loggedCompactedChunkLoads.Clear();
#endif

        if (size <= 0)
        {
            Debug.LogError($"[World] Invalid world size ({size}). Defaulting to 128.");
            size = 128;
        }

        if (height <= 0)
        {
            Debug.LogError($"[World] Invalid world height ({height}). Defaulting to 40.");
            height = 40;
        }

        LogCacheEvent($"[World] INIT seed={seed}, size={size}, height={height}");
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

    public bool TryBreakTopBlock(int x, int z, out BlockType blockType, out Vector3 dropPosition)
    {
        blockType = BlockType.Air;
        dropPosition = Vector3.zero;

        if (!WorldUtils.IsInBounds(x, z, size))
            return false;

        BlockType[] col = Get(x, z);
        int columnHeight = WorldUtils.ColumnHeight(col);

        if (columnHeight <= 0)
            return false;

        int blockY = columnHeight - 1;
        blockType = col[blockY];

        if (blockType == BlockType.Air)
            return false;

        SetBlock(x, z, blockY, BlockType.Air);
        dropPosition = new Vector3(x + 0.5f, columnHeight + 0.4f, z + 0.5f);
        return true;
    }

    public bool TryBreakBlock(int x, int y, int z, out BlockType blockType, out Vector3 dropPosition)
    {
        blockType = BlockType.Air;
        dropPosition = Vector3.zero;

        if (!IsInBlockBounds(x, y, z))
            return false;

        blockType = GetBlock(x, y, z);

        if (blockType == BlockType.Air)
            return false;

        SetBlock(x, z, y, BlockType.Air);
        dropPosition = new Vector3(x + 0.5f, y + 0.7f, z + 0.5f);
        return true;
    }

    public bool TryPlaceTopBlock(int x, int z, BlockType blockType)
    {
        if (blockType == BlockType.Air || !WorldUtils.IsInBounds(x, z, size))
            return false;

        BlockType[] col = Get(x, z);
        int columnHeight = WorldUtils.ColumnHeight(col);

        if (columnHeight >= height)
            return false;

        SetBlock(x, z, columnHeight, blockType);
        return true;
    }

    public bool TryPlaceBlock(int x, int y, int z, BlockType blockType)
    {
        if (!IsInBlockBounds(x, y, z) || blockType == BlockType.Air)
            return false;

        if (GetBlock(x, y, z) != BlockType.Air && BlockVisibilityMask.Opacity(x, y, z) > 0f)
            return false;

        SetBlock(x, z, y, blockType);
        return true;
    }

    public BlockType GetBlock(int x, int y, int z)
    {
        if (!IsInBlockBounds(x, y, z))
            return BlockType.Air;

        return Get(x, z)[y];
    }

    public bool IsInBlockBounds(int x, int y, int z)
    {
        return WorldUtils.IsInBounds(x, z, size) && y >= 0 && y < height;
    }

    public bool SetBlock(int x, int z, int y, BlockType blockType)
    {
        if (!WorldUtils.IsInBounds(x, z, size) || y < 0 || y >= height)
            return false;

        BlockType[] col = Get(x, z);
        col[y] = blockType;
        UpdateCompactedColumn(x, z, y, blockType);
        return true;
    }

    public void CompactChunk(int cx, int cy)
    {
        Vector2Int chunkKey = new Vector2Int(cx, cy);
        LogCacheEvent($"[World] Compacting chunk ({cx}, {cy}) into cache.");

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

                if (!WorldUtils.IsInBounds(worldX, worldZ, size))
                    col = EmptyColumn();
                else if (!cache.TryGetValue(columnKey, out col))
                {
                    col = TryLoadCompactedColumn(worldX, worldZ);

                    if (col == null)
                        col = gen.Column(worldX, worldZ, height);
                }

                for (int y = 0; y < height; y++)
                    compacted[index++] = (byte)col[y];

                cache.Remove(columnKey);
            }
        }

        compactedChunkCache[chunkKey] = compacted;
#if UNITY_EDITOR
        loggedCompactedChunkLoads.Remove(chunkKey);
#endif

        LogCacheEvent($"[World] Cached chunk ({cx}, {cy}). Bytes={compacted.Length}, columnCache={cache.Count}, compactedChunks={compactedChunkCache.Count}.");
    }

    BlockType[] TryLoadCompactedColumn(int x, int z)
    {
        Vector2Int chunkKey = new Vector2Int(
            WorldUtils.ToChunkCoord(x),
            WorldUtils.ToChunkCoord(z)
        );

        if (!compactedChunkCache.TryGetValue(chunkKey, out byte[] compacted))
            return null;

#if UNITY_EDITOR
        if (loggedCompactedChunkLoads.Add(chunkKey))
            LogCacheEvent($"[World] Loading chunk ({chunkKey.x}, {chunkKey.y}) from compacted cache.");
#endif

        int localX = x - chunkKey.x * Chunk.SIZE;
        int localZ = z - chunkKey.y * Chunk.SIZE;
        int index = ((localX * Chunk.SIZE) + localZ) * height;

        if (index < 0 || index + height > compacted.Length)
        {
            Debug.LogError($"[World] Compacted column index out of range at ({x}, {z}), chunk ({chunkKey.x}, {chunkKey.y}), index={index}, len={compacted.Length}.");
            return null;
        }

        BlockType[] col = new BlockType[height];

        for (int y = 0; y < height; y++)
            col[y] = (BlockType)compacted[index + y];

        return col;
    }

    void UpdateCompactedColumn(int x, int z, int y, BlockType blockType)
    {
        Vector2Int chunkKey = new Vector2Int(
            WorldUtils.ToChunkCoord(x),
            WorldUtils.ToChunkCoord(z)
        );

        if (!compactedChunkCache.TryGetValue(chunkKey, out byte[] compacted))
            return;

        int localX = x - chunkKey.x * Chunk.SIZE;
        int localZ = z - chunkKey.y * Chunk.SIZE;
        int index = ((localX * Chunk.SIZE) + localZ) * height + y;

        if (index < 0 || index >= compacted.Length)
        {
            Debug.LogError($"[World] Compacted block index out of range at ({x}, {y}, {z}), chunk ({chunkKey.x}, {chunkKey.y}), index={index}, len={compacted.Length}.");
            return;
        }

        compacted[index] = (byte)blockType;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void LogCacheEvent(string message)
    {
        Debug.Log(message);
    }

    internal BlockType[] EmptyColumn()
    {
        BlockType[] col = new BlockType[height];

        for (int y = 0; y < height; y++)
            col[y] = BlockType.Air;

        return col;
    }
}