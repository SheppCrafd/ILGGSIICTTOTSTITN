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
    Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

    void Update()
    {
        if (world == null || player == null)
        {
            Debug.LogWarning("[ChunkManager] Missing world or player");
            return;
        }

        if (blockDatabase == null)
            blockDatabase = FindAnyObjectByType<BlockDatabase>();

        int pcx = Mathf.FloorToInt(player.x / Chunk.SIZE);
        int pcy = Mathf.FloorToInt(player.y / Chunk.SIZE);
        Vector2Int playerChunk = new Vector2Int(pcx, pcy);

        if (playerChunk != lastPlayerChunk)
        {
            lastPlayerChunk = playerChunk;
            pendingChunks.Clear();
            queuedChunks.Clear();
            QueueVisibleChunks(playerChunk);
        }

        SpawnQueuedChunks();
    }

    void QueueVisibleChunks(Vector2Int playerChunk)
    {
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
                }
            }
        }
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

    bool IsInsideWorld(Vector2Int coord)
    {
        return coord.x >= 0 &&
            coord.y >= 0 &&
            coord.x * Chunk.SIZE < world.size &&
            coord.y * Chunk.SIZE < world.size;
    }

    void SpawnChunk(Vector2Int coord)
    {
        Chunk c = Instantiate(
            chunkPrefab,
            new Vector3(coord.x * Chunk.SIZE, 0, coord.y * Chunk.SIZE),
            Quaternion.identity,
            transform
        );

        c.Build(world, coord.x, coord.y, blockDatabase);

        chunks.Add(coord, c);
    }
}
