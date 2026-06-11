using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public World world;
    public Player player;
    public Chunk chunkPrefab;
    public BlockDatabase blockDatabase;

    public int renderDistance = 4;
    public int chunksPerFrame = 1;

    Dictionary<Vector2Int, Chunk> chunks = new();
    Queue<Vector2Int> pendingChunks = new();
    HashSet<Vector2Int> queuedChunks = new();
    List<Vector2Int> despawnBuffer = new();
    Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    int lastVisibilityVersion = -1;

    void Update()
    {
        if (world == null || player == null)
        {
            Debug.LogWarning("[ChunkManager] Missing world or player");
            return;
        }

        if (blockDatabase == null)
        {
            blockDatabase = FindAnyObjectByType<BlockDatabase>();
            if (blockDatabase == null)
            {
                Debug.LogError("[ChunkManager] No BlockDatabase found in scene. Chunks will use fallback materials.");
            }
        }

        int pcx = WorldUtils.ToChunkCoord(player.x);
        int pcy = WorldUtils.ToChunkCoord(player.y);
        Vector2Int playerChunk = new Vector2Int(pcx, pcy);

        if (playerChunk != lastPlayerChunk)
        {
            LogChunkEvent($"[ChunkManager] Player entered chunk ({playerChunk.x}, {playerChunk.y}); refreshing visible chunks.");
            lastPlayerChunk = playerChunk;
            pendingChunks.Clear();
            queuedChunks.Clear();
            DespawnDistantChunks(playerChunk);
            QueueVisibleChunks(playerChunk);
        }

        UpdateVisibilityMask();

        if (lastVisibilityVersion != BlockVisibilityMask.Version)
        {
            lastVisibilityVersion = BlockVisibilityMask.Version;
            RefreshActiveChunks();
        }

        SpawnQueuedChunks();
    }

    void QueueVisibleChunks(Vector2Int playerChunk)
    {
        int queuedCount = 0;

        for (int radius = 0; radius <= renderDistance; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        continue;

                    Vector2Int coord = new Vector2Int(playerChunk.x + x, playerChunk.y + y);

                    if (!IsInsideWorld(coord) || chunks.ContainsKey(coord) || queuedChunks.Contains(coord))
                        continue;

                    pendingChunks.Enqueue(coord);
                    queuedChunks.Add(coord);
                    queuedCount++;
                    LogChunkEvent($"[ChunkManager] Queued chunk ({coord.x}, {coord.y}) for loading.");
                }
            }
        }

        LogChunkEvent($"[ChunkManager] Queued {queuedCount} chunk(s) around player chunk ({playerChunk.x}, {playerChunk.y}). Pending={pendingChunks.Count}, active={chunks.Count}.");
    }

    void SpawnQueuedChunks()
    {
        int count = Mathf.Max(1, chunksPerFrame);

        for (int i = 0; i < count && pendingChunks.Count > 0; i++)
        {
            Vector2Int coord = pendingChunks.Dequeue();
            queuedChunks.Remove(coord);

            if (!chunks.ContainsKey(coord))
                SpawnChunk(coord);
        }
    }

    internal static bool IsInsideWorld(Vector2Int coord, int worldSize)
    {
        return coord.x >= 0 &&
            coord.y >= 0 &&
            coord.x * Chunk.SIZE < worldSize &&
            coord.y * Chunk.SIZE < worldSize;
    }

    bool IsInsideWorld(Vector2Int coord)
    {
        return IsInsideWorld(coord, world.size);
    }

    internal static bool IsInsideRenderDistance(Vector2Int coord, Vector2Int playerChunk, int renderDist)
    {
        return Mathf.Max(
            Mathf.Abs(coord.x - playerChunk.x),
            Mathf.Abs(coord.y - playerChunk.y)
        ) <= renderDist;
    }

    bool IsInsideRenderDistance(Vector2Int coord, Vector2Int playerChunk)
    {
        return IsInsideRenderDistance(coord, playerChunk, renderDistance);
    }

    public void RefreshColumn(int worldX, int worldZ)
    {
        RefreshChunk(WorldUtils.ToChunkCoord(worldX), WorldUtils.ToChunkCoord(worldZ));
        RefreshChunk(WorldUtils.ToChunkCoord(worldX - 1), WorldUtils.ToChunkCoord(worldZ));
        RefreshChunk(WorldUtils.ToChunkCoord(worldX + 1), WorldUtils.ToChunkCoord(worldZ));
        RefreshChunk(WorldUtils.ToChunkCoord(worldX), WorldUtils.ToChunkCoord(worldZ - 1));
        RefreshChunk(WorldUtils.ToChunkCoord(worldX), WorldUtils.ToChunkCoord(worldZ + 1));
    }

    void UpdateVisibilityMask()
    {
        Camera camera = Camera.main;

        if (camera == null)
            camera = FindAnyObjectByType<Camera>();

        if (camera == null)
            return;

        BlockVisibilityMask.Update(world, player.CurrentWorldPosition(), camera.transform.position, player.visibilityPrismWidth);
    }

    void RefreshActiveChunks()
    {
        foreach (var chunk in chunks)
            chunk.Value.Build(world, chunk.Key.x, chunk.Key.y, blockDatabase);
    }

    void RefreshChunk(int cx, int cy)
    {
        Vector2Int coord = new Vector2Int(cx, cy);

        if (!chunks.TryGetValue(coord, out Chunk chunk))
            return;

        chunk.Build(world, coord.x, coord.y, blockDatabase);
    }

    void DespawnDistantChunks(Vector2Int playerChunk)
    {
        despawnBuffer.Clear();

        foreach (var chunk in chunks)
        {
            if (!IsInsideRenderDistance(chunk.Key, playerChunk))
                despawnBuffer.Add(chunk.Key);
        }

        for (int i = 0; i < despawnBuffer.Count; i++)
        {
            Vector2Int coord = despawnBuffer[i];

            LogChunkEvent($"[ChunkManager] Unloading chunk ({coord.x}, {coord.y}); compacting into cache.");

            if (world != null)
                world.CompactChunk(coord.x, coord.y);

            Destroy(chunks[coord].gameObject);
            chunks.Remove(coord);

            LogChunkEvent($"[ChunkManager] Unloaded chunk ({coord.x}, {coord.y}). Active={chunks.Count}.");
        }
    }

    void SpawnChunk(Vector2Int coord)
    {
        if (chunkPrefab == null)
        {
            Debug.LogError("[ChunkManager] chunkPrefab is not assigned. Cannot spawn chunks.");
            return;
        }

        LogChunkEvent($"[ChunkManager] Loading chunk ({coord.x}, {coord.y}). Pending={pendingChunks.Count}, active={chunks.Count}.");

        Chunk c = Instantiate(
            chunkPrefab,
            new Vector3(coord.x * Chunk.SIZE, 0, coord.y * Chunk.SIZE),
            Quaternion.identity,
            transform
        );

        if (c == null)
        {
            Debug.LogError($"[ChunkManager] Failed to instantiate chunk at ({coord.x}, {coord.y}).");
            return;
        }

        c.Build(world, coord.x, coord.y, blockDatabase);

        chunks.Add(coord, c);

        LogChunkEvent($"[ChunkManager] Loaded chunk ({coord.x}, {coord.y}). Active={chunks.Count}.");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void LogChunkEvent(string message)
    {
        Debug.Log(message);
    }
}