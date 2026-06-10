using NUnit.Framework;
using UnityEngine;

public class ChunkManagerTests
{
    [Test]
    public void IsInsideWorld_OriginChunk_ReturnsTrue()
    {
        Assert.IsTrue(ChunkManager.IsInsideWorld(new Vector2Int(0, 0), 128));
    }

    [Test]
    public void IsInsideWorld_NegativeCoord_ReturnsFalse()
    {
        Assert.IsFalse(ChunkManager.IsInsideWorld(new Vector2Int(-1, 0), 128));
        Assert.IsFalse(ChunkManager.IsInsideWorld(new Vector2Int(0, -1), 128));
    }

    [Test]
    public void IsInsideWorld_ExceedsWorldSize_ReturnsFalse()
    {
        // worldSize=128, Chunk.SIZE=16 => max chunk coord is 7 (7*16=112 < 128)
        // coord 8 => 8*16=128 which is NOT < 128
        Assert.IsFalse(ChunkManager.IsInsideWorld(new Vector2Int(8, 0), 128));
        Assert.IsFalse(ChunkManager.IsInsideWorld(new Vector2Int(0, 8), 128));
    }

    [Test]
    public void IsInsideWorld_MaxValidChunk_ReturnsTrue()
    {
        // worldSize=128, Chunk.SIZE=16 => coord 7: 7*16=112 < 128
        Assert.IsTrue(ChunkManager.IsInsideWorld(new Vector2Int(7, 7), 128));
    }

    [Test]
    public void IsInsideWorld_SmallWorld_CorrectBoundary()
    {
        // worldSize=16, only chunk (0,0) is valid
        Assert.IsTrue(ChunkManager.IsInsideWorld(new Vector2Int(0, 0), 16));
        Assert.IsFalse(ChunkManager.IsInsideWorld(new Vector2Int(1, 0), 16));
    }

    [Test]
    public void IsInsideRenderDistance_SameChunk_ReturnsTrue()
    {
        var player = new Vector2Int(4, 4);
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(4, 4), player, 4));
    }

    [Test]
    public void IsInsideRenderDistance_WithinRange_ReturnsTrue()
    {
        var player = new Vector2Int(4, 4);
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(5, 5), player, 4));
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(8, 4), player, 4));
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(4, 8), player, 4));
    }

    [Test]
    public void IsInsideRenderDistance_AtBoundary_ReturnsTrue()
    {
        var player = new Vector2Int(4, 4);
        // Chebyshev distance = 4, renderDistance = 4
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(0, 0), player, 4));
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(8, 8), player, 4));
    }

    [Test]
    public void IsInsideRenderDistance_OutsideRange_ReturnsFalse()
    {
        var player = new Vector2Int(4, 4);
        // Chebyshev distance = 5 > renderDistance 4
        Assert.IsFalse(ChunkManager.IsInsideRenderDistance(new Vector2Int(9, 4), player, 4));
        Assert.IsFalse(ChunkManager.IsInsideRenderDistance(new Vector2Int(4, 9), player, 4));
    }

    [Test]
    public void IsInsideRenderDistance_RenderDistanceZero_OnlySameChunk()
    {
        var player = new Vector2Int(3, 3);
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(3, 3), player, 0));
        Assert.IsFalse(ChunkManager.IsInsideRenderDistance(new Vector2Int(4, 3), player, 0));
    }

    [Test]
    public void IsInsideRenderDistance_LargeRenderDistance()
    {
        var player = new Vector2Int(0, 0);
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(10, 10), player, 10));
        Assert.IsFalse(ChunkManager.IsInsideRenderDistance(new Vector2Int(11, 0), player, 10));
    }

    [Test]
    public void IsInsideWorld_ZeroWorldSize_AlwaysFalse()
    {
        Assert.IsFalse(ChunkManager.IsInsideWorld(new Vector2Int(0, 0), 0));
    }

    [Test]
    public void IsInsideRenderDistance_NegativePlayerChunk()
    {
        // Player at negative coords (shouldn't happen in-game but tests the math)
        var player = new Vector2Int(-2, -2);
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(-2, -2), player, 4));
        Assert.IsTrue(ChunkManager.IsInsideRenderDistance(new Vector2Int(2, 2), player, 4));
        Assert.IsFalse(ChunkManager.IsInsideRenderDistance(new Vector2Int(3, 3), player, 4));
    }
}
