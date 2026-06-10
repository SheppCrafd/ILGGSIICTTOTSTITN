using NUnit.Framework;
using UnityEngine;

public class WorldTests
{
    World CreateWorld(int seed = 42, int size = 128, int height = 40)
    {
        var go = new GameObject("TestWorld");
        var world = go.AddComponent<World>();
        world.size = size;
        world.height = height;
        world.Init(seed);
        return world;
    }

    void DestroyWorld(World world)
    {
        Object.DestroyImmediate(world.gameObject);
    }

    [Test]
    public void Init_SetsGenerator()
    {
        var world = CreateWorld(123);
        Assert.IsNotNull(world.gen);
        DestroyWorld(world);
    }

    [Test]
    public void Get_ReturnsSameColumnOnRepeatedCalls()
    {
        var world = CreateWorld(42);

        BlockType[] col1 = world.Get(10, 10);
        BlockType[] col2 = world.Get(10, 10);

        Assert.AreSame(col1, col2, "Get should return cached column");
        DestroyWorld(world);
    }

    [Test]
    public void Get_ReturnsColumnOfCorrectHeight()
    {
        var world = CreateWorld(42, 64, 20);

        BlockType[] col = world.Get(5, 5);

        Assert.AreEqual(20, col.Length);
        DestroyWorld(world);
    }

    [Test]
    public void CompactChunk_RemovesColumnsFromCache()
    {
        var world = CreateWorld(42);

        // Access columns in chunk (0,0) to populate cache
        world.Get(0, 0);
        world.Get(1, 1);
        world.Get(15, 15);

        // Compact chunk (0,0)
        world.CompactChunk(0, 0);

        // After compaction, getting the same columns should still work
        // (loaded from compacted cache)
        BlockType[] col = world.Get(0, 0);
        Assert.IsNotNull(col);
        Assert.AreEqual(world.height, col.Length);

        DestroyWorld(world);
    }

    [Test]
    public void CompactChunk_PreservesColumnData()
    {
        var world = CreateWorld(42);

        // Get column data before compaction
        BlockType[] original = world.Get(5, 5);
        BlockType[] originalCopy = new BlockType[original.Length];
        System.Array.Copy(original, originalCopy, original.Length);

        // Compact the chunk containing (5,5) which is chunk (0,0)
        world.CompactChunk(0, 0);

        // Get column again (from compacted cache)
        BlockType[] restored = world.Get(5, 5);

        Assert.AreEqual(originalCopy.Length, restored.Length);
        for (int i = 0; i < originalCopy.Length; i++)
        {
            Assert.AreEqual(originalCopy[i], restored[i],
                $"Mismatch at y={i} after compaction");
        }

        DestroyWorld(world);
    }

    [Test]
    public void EmptyColumn_ReturnsAllAir()
    {
        var world = CreateWorld(42, 128, 20);

        BlockType[] col = world.EmptyColumn();

        Assert.AreEqual(20, col.Length);
        for (int i = 0; i < col.Length; i++)
        {
            Assert.AreEqual(BlockType.Air, col[i]);
        }

        DestroyWorld(world);
    }

    [Test]
    public void Get_DifferentPositionsReturnDifferentColumns()
    {
        var world = CreateWorld(42);

        BlockType[] col1 = world.Get(0, 0);
        BlockType[] col2 = world.Get(50, 50);

        Assert.AreNotSame(col1, col2);
        DestroyWorld(world);
    }

    [Test]
    public void Init_ClearsCaches()
    {
        var world = CreateWorld(42);

        // Populate cache
        world.Get(5, 5);

        // Re-init with different seed
        world.Init(9999);

        // Get should generate fresh data
        BlockType[] col = world.Get(5, 5);
        Assert.IsNotNull(col);
        Assert.AreEqual(world.height, col.Length);

        DestroyWorld(world);
    }

    [Test]
    public void CompactChunk_ThenGet_WorksForAllColumnsInChunk()
    {
        var world = CreateWorld(42, 128, 10);

        // Pre-populate some columns
        for (int x = 0; x < Chunk.SIZE; x++)
            world.Get(x, 0);

        // Compact
        world.CompactChunk(0, 0);

        // Verify all columns in that chunk are accessible
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                BlockType[] col = world.Get(x, z);
                Assert.IsNotNull(col);
                Assert.AreEqual(10, col.Length);
            }
        }

        DestroyWorld(world);
    }
}
