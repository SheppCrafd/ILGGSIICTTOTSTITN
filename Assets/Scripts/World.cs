using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int size = 128;
    public int height = 40;

    public WorldGenerator gen;

    private Dictionary<Vector2Int, BlockType[]> cache =
        new Dictionary<Vector2Int, BlockType[]>();

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
            var col = gen.Column(x, y, height);
            cache[key] = col;

        }

        return cache[key];
    }
}
