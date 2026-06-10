using NUnit.Framework;

public class ChunkTests
{
    [Test]
    public void ColumnHeight_AllAir_ReturnsZero()
    {
        BlockType[] col = new BlockType[] { BlockType.Air, BlockType.Air, BlockType.Air };
        Assert.AreEqual(0, Chunk.ColumnHeight(col));
    }

    [Test]
    public void ColumnHeight_SingleBlock_ReturnsOne()
    {
        BlockType[] col = new BlockType[] { BlockType.Stone, BlockType.Air, BlockType.Air };
        Assert.AreEqual(1, Chunk.ColumnHeight(col));
    }

    [Test]
    public void ColumnHeight_FullColumn_ReturnsLength()
    {
        BlockType[] col = new BlockType[] { BlockType.Stone, BlockType.Dirt, BlockType.Grass };
        Assert.AreEqual(3, Chunk.ColumnHeight(col));
    }

    [Test]
    public void ColumnHeight_TopBlockIsSolid_ReturnsCorrectHeight()
    {
        BlockType[] col = new BlockType[]
        {
            BlockType.Stone,
            BlockType.Stone,
            BlockType.Dirt,
            BlockType.Dirt,
            BlockType.Grass,
            BlockType.Air,
            BlockType.Air
        };
        Assert.AreEqual(5, Chunk.ColumnHeight(col));
    }

    [Test]
    public void ColumnHeight_EmptyArray_ReturnsZero()
    {
        BlockType[] col = new BlockType[0];
        Assert.AreEqual(0, Chunk.ColumnHeight(col));
    }

    [Test]
    public void BlockTypeAtDepth_TopBlock_ReturnsGrass()
    {
        // columnHeight=10, y=9 => depthFromTop = 10-1-9 = 0 => Grass
        Assert.AreEqual(BlockType.Grass, Chunk.BlockTypeAtDepth(10, 9));
    }

    [Test]
    public void BlockTypeAtDepth_OneBelow_ReturnsDirt()
    {
        // columnHeight=10, y=8 => depthFromTop = 10-1-8 = 1 => Dirt
        Assert.AreEqual(BlockType.Dirt, Chunk.BlockTypeAtDepth(10, 8));
    }

    [Test]
    public void BlockTypeAtDepth_ThreeBelow_ReturnsDirt()
    {
        // columnHeight=10, y=6 => depthFromTop = 10-1-6 = 3 => Dirt
        Assert.AreEqual(BlockType.Dirt, Chunk.BlockTypeAtDepth(10, 6));
    }

    [Test]
    public void BlockTypeAtDepth_FourBelow_ReturnsStone()
    {
        // columnHeight=10, y=5 => depthFromTop = 10-1-5 = 4 => Stone
        Assert.AreEqual(BlockType.Stone, Chunk.BlockTypeAtDepth(10, 5));
    }

    [Test]
    public void BlockTypeAtDepth_DeepUnderground_ReturnsStone()
    {
        // columnHeight=20, y=0 => depthFromTop = 20-1-0 = 19 => Stone
        Assert.AreEqual(BlockType.Stone, Chunk.BlockTypeAtDepth(20, 0));
    }

    [Test]
    public void BlockTypeAtDepth_Height1_TopIsGrass()
    {
        // columnHeight=1, y=0 => depthFromTop = 1-1-0 = 0 => Grass
        Assert.AreEqual(BlockType.Grass, Chunk.BlockTypeAtDepth(1, 0));
    }

    [Test]
    public void ColumnHeight_GapInMiddle_ReturnsHighestSolid()
    {
        // Air gap in the middle - top solid block determines height
        BlockType[] col = new BlockType[]
        {
            BlockType.Stone,
            BlockType.Air,
            BlockType.Air,
            BlockType.Grass,
            BlockType.Air
        };
        // Highest non-air is index 3
        Assert.AreEqual(4, Chunk.ColumnHeight(col));
    }

    [Test]
    public void ChunkSize_IsSixteen()
    {
        Assert.AreEqual(16, Chunk.SIZE);
    }
}
