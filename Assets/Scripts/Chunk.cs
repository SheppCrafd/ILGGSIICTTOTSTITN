using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("GameTests.Editor")]

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    public const int SIZE = 16;

    Mesh mesh;

    List<Vector3> vertices = new();
    List<Vector2> uvs = new();
    List<int> grassTopTriangles = new();
    List<int> grassSideTriangles = new();
    List<int> dirtTriangles = new();
    List<int> stoneTriangles = new();

    void Awake()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        if (mr.material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[Chunk] 'Standard' shader not found. Ensure it is included in Always Included Shaders.");
                return;
            }
            mr.material = new Material(shader);
        }
    }

    public void Build(World world, int cx, int cy)
    {
        Build(world, cx, cy, null);
    }

    public void Build(World world, int cx, int cy, BlockDatabase blockDatabase)
    {
        if (world == null)
        {
            Debug.LogError($"[Chunk] Build called with null world at ({cx}, {cy}).");
            return;
        }

        vertices.Clear();
        uvs.Clear();
        grassTopTriangles.Clear();
        grassSideTriangles.Clear();
        dirtTriangles.Clear();
        stoneTriangles.Clear();

        ApplyBlockMaterials(blockDatabase);

        int startX = cx * SIZE;
        int startZ = cy * SIZE;

        BuildHeightMapMesh(world, startX, startZ);

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.subMeshCount = 4;
        mesh.SetTriangles(grassTopTriangles, 0);
        mesh.SetTriangles(grassSideTriangles, 1);
        mesh.SetTriangles(dirtTriangles, 2);
        mesh.SetTriangles(stoneTriangles, 3);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void ApplyBlockMaterials(BlockDatabase blockDatabase)
    {
        var mr = GetComponent<MeshRenderer>();

        Material grassTop = FindMaterial(blockDatabase, BlockType.Grass, "Top");
        Material grassSide = FindMaterial(blockDatabase, BlockType.Grass, "North");
        Material dirt = FindMaterial(blockDatabase, BlockType.Dirt, null);
        Material stone = FindMaterial(blockDatabase, BlockType.Stone, null);

        mr.sharedMaterials = new Material[]
        {
            grassTop != null ? grassTop : WorldUtils.CreateFallbackMaterial("Grass Top", Color.green),
            grassSide != null ? grassSide : WorldUtils.CreateFallbackMaterial("Grass Side", new Color(0.45f, 0.75f, 0.25f)),
            dirt != null ? dirt : WorldUtils.CreateFallbackMaterial("Dirt", new Color(0.45f, 0.25f, 0.12f)),
            stone != null ? stone : WorldUtils.CreateFallbackMaterial("Stone", Color.gray)
        };
    }

    Material FindMaterial(BlockDatabase blockDatabase, BlockType type, string childName)
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

    Material FallbackMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError($"[Chunk] 'Standard' shader not found when creating fallback material '{name}'.");
            shader = Shader.Find("Hidden/InternalErrorShader");
        }
        Material material = new Material(shader);
        material.name = name;
        material.color = color;
        return material;
    }

    void BuildHeightMapMesh(World world, int startX, int startZ)
    {
        int[,] heights = new int[SIZE, SIZE];

        for (int x = 0; x < SIZE; x++)
        {
            for (int z = 0; z < SIZE; z++)
            {
                int worldX = startX + x;
                int worldZ = startZ + z;

                if (!WorldUtils.IsInBounds(worldX, worldZ, world.size))
                    continue;

                heights[x, z] = WorldUtils.ColumnHeight(world.Get(worldX, worldZ));
            }
        }

        for (int x = 0; x < SIZE; x++)
        {
            for (int z = 0; z < SIZE; z++)
            {
                int h = heights[x, z];

                if (h <= 0)
                    continue;

                AddTopFace(x, h, z);
                AddSideFaces(world, startX, startZ, x, z, h, 1, 0);
                AddSideFaces(world, startX, startZ, x, z, h, -1, 0);
                AddSideFaces(world, startX, startZ, x, z, h, 0, 1);
                AddSideFaces(world, startX, startZ, x, z, h, 0, -1);
            }
        }
    }

    internal static int ColumnHeight(BlockType[] col) => WorldUtils.ColumnHeight(col);

    void AddSideFaces(World world, int startX, int startZ, int x, int z, int height, int dx, int dz)
    {
        int neighborHeight = GetColumnHeight(world, startX + x + dx, startZ + z + dz);

        for (int y = neighborHeight; y < height; y++)
            AddSideFace(x, y, z, dx, dz, WorldUtils.BlockTypeAtDepth(height, y));
    }

    int GetColumnHeight(World world, int worldX, int worldZ)
    {
        if (!WorldUtils.IsInBounds(worldX, worldZ, world.size))
            return 0;

        return WorldUtils.ColumnHeight(world.Get(worldX, worldZ));
    }

    internal static BlockType BlockTypeAtDepth(int columnHeight, int y) => WorldUtils.BlockTypeAtDepth(columnHeight, y);

    void AddTopFace(int x, int height, int z)
    {
        AddQuad(
            new Vector3(x, height, z),
            new Vector3(x, height, z + 1),
            new Vector3(x + 1, height, z + 1),
            new Vector3(x + 1, height, z),
            grassTopTriangles,
            1f,
            1f
        );
    }

    void AddSideFace(int x, int y, int z, int dx, int dz, BlockType type)
    {
        if (dx > 0)
        {
            AddQuad(
                new Vector3(x + 1, y, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y + 1, z + 1),
                new Vector3(x + 1, y, z + 1),
                MaterialTriangles(type, false),
                1f,
                1f
            );
        }
        else if (dx < 0)
        {
            AddQuad(
                new Vector3(x, y, z),
                new Vector3(x, y, z + 1),
                new Vector3(x, y + 1, z + 1),
                new Vector3(x, y + 1, z),
                MaterialTriangles(type, false),
                1f,
                1f,
                true
            );
        }
        else if (dz > 0)
        {
            AddQuad(
                new Vector3(x, y, z + 1),
                new Vector3(x + 1, y, z + 1),
                new Vector3(x + 1, y + 1, z + 1),
                new Vector3(x, y + 1, z + 1),
                MaterialTriangles(type, false),
                1f,
                1f
            );
        }
        else
        {
            AddQuad(
                new Vector3(x, y, z),
                new Vector3(x, y + 1, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y, z),
                MaterialTriangles(type, false),
                1f,
                1f
            );
        }
    }

    List<int> MaterialTriangles(BlockType type, bool isTop)
    {
        if (type == BlockType.Grass)
            return isTop ? grassTopTriangles : grassSideTriangles;

        if (type == BlockType.Dirt)
            return dirtTriangles;

        return stoneTriangles;
    }

    void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> targetTriangles, float uvWidth, float uvHeight, bool rotateUvsCounterClockwise = false)
    {
        int v = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        if (rotateUvsCounterClockwise)
        {
            uvs.Add(new Vector2(uvWidth, 0));
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, uvHeight));
            uvs.Add(new Vector2(uvWidth, uvHeight));
        }
        else
        {
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, uvHeight));
            uvs.Add(new Vector2(uvWidth, uvHeight));
            uvs.Add(new Vector2(uvWidth, 0));
        }

        targetTriangles.Add(v + 0);
        targetTriangles.Add(v + 1);
        targetTriangles.Add(v + 2);

        targetTriangles.Add(v + 0);
        targetTriangles.Add(v + 2);
        targetTriangles.Add(v + 3);
    }

}