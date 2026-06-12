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

    bool hasBuilt = false;

    // Marks the chunk as needing a rebuild on next Build call.
    public void MarkDirty()
    {
        hasBuilt = false;
        Debug.Log($"[Chunk] MarkDirty called on chunk '{gameObject.name}'");
    }

    List<Vector3> vertices = new();
    List<Vector2> uvs = new();
    List<int> grassTopTriangles = new();
    List<int> grassSideTriangles = new();
    List<int> dirtTriangles = new();
    List<int> stoneTriangles = new();
    List<int> fadedGrassTopTriangles = new();
    List<int> fadedGrassSideTriangles = new();
    List<int> fadedDirtTriangles = new();
    List<int> fadedStoneTriangles = new();
    static readonly Dictionary<Material, Material> transparentMaterials = new();

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

        // Prevent unnecessary rebuilds - only build if this is the first time
        if (hasBuilt)
        {
            Debug.LogWarning($"[Chunk] Skipping rebuild for already-built chunk at ({cx}, {cy}). hasBuilt={hasBuilt}");
            return;
        }

        vertices.Clear();
        uvs.Clear();
        grassTopTriangles.Clear();
        grassSideTriangles.Clear();
        dirtTriangles.Clear();
        stoneTriangles.Clear();
        fadedGrassTopTriangles.Clear();
        fadedGrassSideTriangles.Clear();
        fadedDirtTriangles.Clear();
        fadedStoneTriangles.Clear();

        int startX = cx * SIZE;
        int startZ = cy * SIZE;

        BuildBlockMesh(world, startX, startZ);

        // Only rebuild mesh if there's actual data to prevent unnecessary allocations
        if (vertices.Count == 0)
        {
            mesh.Clear();
            hasBuilt = true;
            Debug.Log($"[Chunk] Built EMPTY mesh for chunk ({cx}, {cy}) — vertices=0. This may be due to visibility masking.");
            return;
        }

        ApplyBlockMaterials(blockDatabase);

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.subMeshCount = 8;
        mesh.SetTriangles(grassTopTriangles, 0);
        mesh.SetTriangles(grassSideTriangles, 1);
        mesh.SetTriangles(dirtTriangles, 2);
        mesh.SetTriangles(stoneTriangles, 3);
        mesh.SetTriangles(fadedGrassTopTriangles, 4);
        mesh.SetTriangles(fadedGrassSideTriangles, 5);
        mesh.SetTriangles(fadedDirtTriangles, 6);
        mesh.SetTriangles(fadedStoneTriangles, 7);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        hasBuilt = true;
        Debug.Log($"[Chunk] Built mesh for chunk ({cx}, {cy}) — vertices={vertices.Count}, subMeshCount={mesh.subMeshCount}");
    }

    void ApplyBlockMaterials(BlockDatabase blockDatabase)
    {
        var mr = GetComponent<MeshRenderer>();

        Material grassTop = WorldUtils.FindBlockMaterial(blockDatabase, BlockType.Grass, "Top");
        Material grassSide = WorldUtils.FindBlockMaterial(blockDatabase, BlockType.Grass, "North");
        Material dirt = WorldUtils.FindBlockMaterial(blockDatabase, BlockType.Dirt, null);
        Material stone = WorldUtils.FindBlockMaterial(blockDatabase, BlockType.Stone, null);

        grassTop = grassTop != null ? grassTop : WorldUtils.CreateFallbackMaterial("Grass Top", Color.green);
        grassSide = grassSide != null ? grassSide : WorldUtils.CreateFallbackMaterial("Grass Side", new Color(0.45f, 0.75f, 0.25f));
        dirt = dirt != null ? dirt : WorldUtils.CreateFallbackMaterial("Dirt", new Color(0.45f, 0.25f, 0.12f));
        stone = stone != null ? stone : WorldUtils.CreateFallbackMaterial("Stone", Color.gray);

        // Only create new material array if materials have changed to prevent memory churn
        Material[] newMaterials = new Material[]
        {
            grassTop,
            grassSide,
            dirt,
            stone,
            CreateTransparentMaterial(grassTop),
            CreateTransparentMaterial(grassSide),
            CreateTransparentMaterial(dirt),
            CreateTransparentMaterial(stone)
        };

        // Check if materials actually changed before reassigning
        Material[] currentMaterials = mr.sharedMaterials;
        if (currentMaterials == null || currentMaterials.Length != newMaterials.Length)
        {
            mr.sharedMaterials = newMaterials;
            return;
        }

        for (int i = 0; i < newMaterials.Length; i++)
        {
            if (currentMaterials[i] != newMaterials[i])
            {
                mr.sharedMaterials = newMaterials;
                return;
            }
        }
    }

    Material CreateTransparentMaterial(Material source)
    {
        if (transparentMaterials.TryGetValue(source, out Material cachedMaterial))
            return cachedMaterial;

        Material material = new Material(source);
        material.name = $"{source.name} 50%";
        Color color = material.color;
        color.a = 0.5f;
        material.color = color;
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        transparentMaterials[source] = material;
        return material;
    }

    void BuildBlockMesh(World world, int startX, int startZ)
    {
        for (int x = 0; x < SIZE; x++)
        {
            for (int z = 0; z < SIZE; z++)
            {
                int worldX = startX + x;
                int worldZ = startZ + z;

                if (!WorldUtils.IsInBounds(worldX, worldZ, world.size))
                    continue;

                BlockType[] col = world.Get(worldX, worldZ);

                for (int y = 0; y < world.height; y++)
                    AddVisibleBlock(world, startX, startZ, x, y, z, col[y]);
            }
        }
    }

    internal static int ColumnHeight(BlockType[] col) => WorldUtils.ColumnHeight(col);

    void AddVisibleBlock(World world, int startX, int startZ, int x, int y, int z, BlockType type)
    {
        if (type == BlockType.Air)
            return;

        int worldX = startX + x;
        int worldZ = startZ + z;
        float opacity = BlockVisibilityMask.Opacity(worldX, y, worldZ);

        if (opacity <= 0f)
            return;

        if (IsAirLike(world, worldX, y + 1, worldZ))
            AddTopFace(x, y, z, type, opacity);

        if (IsAirLike(world, worldX, y - 1, worldZ))
            AddBottomFace(x, y, z, type, opacity);

        if (IsAirLike(world, worldX + 1, y, worldZ))
            AddSideFace(x, y, z, 1, 0, type, opacity);

        if (IsAirLike(world, worldX - 1, y, worldZ))
            AddSideFace(x, y, z, -1, 0, type, opacity);

        if (IsAirLike(world, worldX, y, worldZ + 1))
            AddSideFace(x, y, z, 0, 1, type, opacity);

        if (IsAirLike(world, worldX, y, worldZ - 1))
            AddSideFace(x, y, z, 0, -1, type, opacity);
    }

    bool IsAirLike(World world, int worldX, int y, int worldZ)
    {
        return !world.IsInBlockBounds(worldX, y, worldZ) ||
            world.GetBlock(worldX, y, worldZ) == BlockType.Air ||
            BlockVisibilityMask.Opacity(worldX, y, worldZ) <= 0f;
    }

    internal static BlockType BlockTypeAtDepth(int columnHeight, int y) => WorldUtils.BlockTypeAtDepth(columnHeight, y);

    void AddTopFace(int x, int y, int z, BlockType type, float opacity)
    {
        AddQuad(
            new Vector3(x, y + 1, z),
            new Vector3(x, y + 1, z + 1),
            new Vector3(x + 1, y + 1, z + 1),
            new Vector3(x + 1, y + 1, z),
            MaterialTriangles(type, true, opacity),
            1f,
            1f
        );
    }

    void AddBottomFace(int x, int y, int z, BlockType type, float opacity)
    {
        AddQuad(
            new Vector3(x, y, z),
            new Vector3(x + 1, y, z),
            new Vector3(x + 1, y, z + 1),
            new Vector3(x, y, z + 1),
            MaterialTriangles(type, false, opacity),
            1f,
            1f,
            true
        );
    }

    void AddSideFace(int x, int y, int z, int dx, int dz, BlockType type, float opacity)
    {
        if (dx > 0)
        {
            AddQuad(
                new Vector3(x + 1, y, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y + 1, z + 1),
                new Vector3(x + 1, y, z + 1),
                MaterialTriangles(type, false, opacity),
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
                MaterialTriangles(type, false, opacity),
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
                MaterialTriangles(type, false, opacity),
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
                MaterialTriangles(type, false, opacity),
                1f,
                1f
            );
        }
    }

    List<int> MaterialTriangles(BlockType type, bool isTop, float opacity)
    {
        bool faded = opacity < 1f;

        if (type == BlockType.Grass)
            return faded ?
                (isTop ? fadedGrassTopTriangles : fadedGrassSideTriangles) :
                (isTop ? grassTopTriangles : grassSideTriangles);

        if (type == BlockType.Dirt)
            return faded ? fadedDirtTriangles : dirtTriangles;

        return faded ? fadedStoneTriangles : stoneTriangles;
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