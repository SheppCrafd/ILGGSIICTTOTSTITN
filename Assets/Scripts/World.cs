using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int size = 64;
    public int height = 40;

    public WorldGenerator gen;

    private const int MaxCacheSize = 64;

    private Dictionary<Vector2Int, BlockType[]> cache =
        new Dictionary<Vector2Int, BlockType[]>();

    private LinkedList<Vector2Int> cacheAccessOrder =
        new LinkedList<Vector2Int>();

    private Dictionary<Vector2Int, byte[]> compactedChunkCache =
        new Dictionary<Vector2Int, byte[]>();

#if UNITY_EDITOR
    private HashSet<Vector2Int> loggedCompactedChunkLoads =
        new HashSet<Vector2Int>();
#endif

    void Awake()
    {
        cache.Clear();
        cacheAccessOrder.Clear();

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
            Debug.LogError($"[World] Invalid size ({size}). Defaulting to 128.");
            size = 128;
        }

        if (height <= 0)
        {
            Debug.LogError($"[World] Invalid height ({height}). Defaulting to 40.");
            height = 40;
        }

        LogCacheEvent($"[World] INIT seed={seed}, size={size}, height={height}");
    }

    // =========================================================
    // COLUMN GET (LRU CACHE)
    // =========================================================

    public BlockType[] Get(int x, int z)
    {
        if (gen == null)
        {
            int seed = Random.Range(0, 999999);
            Init(seed);
        }

        Vector2Int key = new Vector2Int(x, z);

        if (!cache.ContainsKey(key))
        {
            var col = TryLoadCompactedColumn(x, z);

            if (col == null)
                col = gen.Column(x, z, height);

            cache[key] = col;
            cacheAccessOrder.AddFirst(key);

            if (cache.Count > MaxCacheSize)
            {
                Vector2Int lru = cacheAccessOrder.Last.Value;
                cacheAccessOrder.RemoveLast();
                cache.Remove(lru);
            }
        }
        else
        {
            cacheAccessOrder.Remove(key);
            cacheAccessOrder.AddFirst(key);
        }

        return cache[key];
    }

    // =========================================================
    // CORE BLOCK SET
    // =========================================================

    public bool SetBlock(int x, int z, int y, BlockType blockType)
    {
        if (!WorldUtils.IsInBounds(x, z, size) || y < 0 || y >= height)
            return false;

        BlockType[] col = Get(x, z);
        col[y] = blockType;

        Vector2Int key = new Vector2Int(x, z);
        cache[key] = col;

        Vector2Int chunkKey = new Vector2Int(
            WorldUtils.ToChunkCoord(x),
            WorldUtils.ToChunkCoord(z)
        );

        if (compactedChunkCache.ContainsKey(chunkKey))
            compactedChunkCache.Remove(chunkKey);

        return true;
    }

    // =========================================================
    // BREAK SYSTEM
    // =========================================================

    public bool TryBreakTopBlock(int x, int z, out BlockType blockType, out Vector3 dropPosition)
    {
        blockType = BlockType.Air;
        dropPosition = Vector3.zero;

        if (!WorldUtils.IsInBounds(x, z, size))
            return false;

        BlockType[] col = Get(x, z);
        int h = WorldUtils.ColumnHeight(col);

        if (h <= 0)
            return false;

        int y = h - 1;
        blockType = col[y];

        if (blockType == BlockType.Air)
            return false;

        SetBlock(x, z, y, BlockType.Air);

        dropPosition = new Vector3(x + 0.5f, h + 0.4f, z + 0.5f);
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

    // =========================================================
    // PLACE SYSTEM
    // =========================================================

    public bool TryPlaceTopBlock(int x, int z, BlockType blockType)
    {
        if (blockType == BlockType.Air || !WorldUtils.IsInBounds(x, z, size))
            return false;

        BlockType[] col = Get(x, z);
        int h = WorldUtils.ColumnHeight(col);

        if (h >= height)
            return false;

        return SetBlock(x, z, h, blockType);
    }

    public bool TryPlaceBlock(int x, int y, int z, BlockType blockType)
    {
        if (!IsInBlockBounds(x, y, z) || blockType == BlockType.Air)
            return false;

        if (GetBlock(x, y, z) != BlockType.Air &&
            BlockVisibilityMask.Opacity(x, y, z) > 0f)
            return false;

        return SetBlock(x, z, y, blockType);
    }

    // =========================================================
    // BLOCK GET
    // =========================================================

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

    // =========================================================
    // COMPRESSED SYSTEM (DISABLED)
    // =========================================================

    public void CompactChunk(int cx, int cy)
    {
        return; // disabled for debugging
    }

    BlockType[] TryLoadCompactedColumn(int x, int z)
    {
        return null; // disabled
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
            Debug.LogError($"[World] Compacted index out of range {index}");
            return;
        }

        compacted[index] = (byte)blockType;
    }

    // =========================================================
    // UTIL
    // =========================================================

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