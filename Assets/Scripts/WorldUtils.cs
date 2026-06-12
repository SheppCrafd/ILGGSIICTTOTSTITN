using System.Collections.Generic;
using UnityEngine;

public static class WorldUtils
{
    public const int DirtDepth = 3;

    private static readonly Dictionary<string, Material> fallbackMaterialCache = new Dictionary<string, Material>();

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

    public static Color BlockColor(BlockType type)
    {
        switch (type)
        {
            case BlockType.Grass:
                return Color.green;

            case BlockType.Dirt:
                return new Color(0.45f, 0.25f, 0.12f);

            case BlockType.Stone:
                return Color.gray;

            default:
                return Color.white;
        }
    }

    public static Material FindBlockMaterial(BlockDatabase blockDatabase, BlockType type, string childName)
    {
        if (blockDatabase == null)
            return null;

        GameObject prefab = blockDatabase.Get(type);

        if (prefab == null)
            return null;

        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);

        if (!string.IsNullOrEmpty(childName))
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].gameObject.name == childName && renderers[i].sharedMaterial != null)
                    return renderers[i].sharedMaterial;
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].sharedMaterial != null)
                return renderers[i].sharedMaterial;
        }

        return null;
    }

    // Return a Texture2D suitable for UI icons for the given block type by inspecting its prefab renderers.
    public static Texture2D FindBlockTexture(BlockDatabase blockDatabase, BlockType type)
    {
        if (blockDatabase == null)
            return null;

        GameObject prefab = blockDatabase.Get(type);
        if (prefab == null)
            return null;

        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        Texture2D chosen = null;

        // Prefer a renderer or texture with 'side' in the name for side-facing icons (e.g., Grass_Side)
        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            var tex = r.sharedMaterial.mainTexture as Texture2D;
            if (tex == null) continue;
            if (r.gameObject.name.ToLower().Contains("side") || tex.name.ToLower().Contains("side"))
            {
                chosen = tex;
                break;
            }
        }

        // Fallback to any available renderer texture
        if (chosen == null)
        {
            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                var tex = r.sharedMaterial.mainTexture as Texture2D;
                if (tex != null)
                {
                    chosen = tex;
                    break;
                }
            }
        }

        return chosen;
    }

    public static Material CreateFallbackMaterial(string name, Color color)
    {
        string cacheKey = $"{name}_{color.r}_{color.g}_{color.b}";

        if (fallbackMaterialCache.TryGetValue(cacheKey, out Material cachedMaterial))
            return cachedMaterial;

        // Try multiple shader candidates to support URP/HDRP and Built-in
        string[] shaderCandidates = new string[]
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
            "Standard",
            "Hidden/InternalErrorShader"
        };

        Shader shader = null;
        foreach (var s in shaderCandidates)
        {
            shader = Shader.Find(s);
            if (shader != null)
                break;
        }

        if (shader == null)
        {
            Debug.LogError($"[WorldUtils] No suitable shader found when creating fallback material '{name}'.");
            shader = Shader.Find("Hidden/InternalErrorShader");
        }

        Material material = new Material(shader);
        material.name = name;

        // Attempt to set a color where supported.
        try
        {
            material.color = color;
        }
        catch { }

        fallbackMaterialCache[cacheKey] = material;
        return material;
    }
}