using NUnit.Framework;

public class WorldGeneratorTests
{
    [Test]
    public void Column_ReturnsArrayOfSpecifiedMaxHeight()
    {
        var gen = new WorldGenerator(42);
        BlockType[] col = gen.Column(10, 10, 40);

        Assert.AreEqual(40, col.Length);
    }

    [Test]
    public void Column_TopBlockIsGrass()
    {
        var gen = new WorldGenerator(42);
        BlockType[] col = gen.Column(5, 5, 40);

        int topSolid = -1;
        for (int i = col.Length - 1; i >= 0; i--)
        {
            if (col[i] != BlockType.Air)
            {
                topSolid = i;
                break;
            }
        }

        Assert.GreaterOrEqual(topSolid, 0, "Column should have at least one solid block");
        Assert.AreEqual(BlockType.Grass, col[topSolid]);
    }

    [Test]
    public void Column_DirtLayerBelowGrass()
    {
        var gen = new WorldGenerator(42);
        BlockType[] col = gen.Column(5, 5, 40);

        int topSolid = -1;
        for (int i = col.Length - 1; i >= 0; i--)
        {
            if (col[i] != BlockType.Air)
            {
                topSolid = i;
                break;
            }
        }

        // Dirt should be the 3 blocks below grass (DirtDepth = 3)
        if (topSolid >= 4)
        {
            Assert.AreEqual(BlockType.Dirt, col[topSolid - 1]);
            Assert.AreEqual(BlockType.Dirt, col[topSolid - 2]);
            Assert.AreEqual(BlockType.Dirt, col[topSolid - 3]);
        }
    }

    [Test]
    public void Column_StoneAtBottom()
    {
        var gen = new WorldGenerator(42);
        BlockType[] col = gen.Column(5, 5, 40);

        int topSolid = -1;
        for (int i = col.Length - 1; i >= 0; i--)
        {
            if (col[i] != BlockType.Air)
            {
                topSolid = i;
                break;
            }
        }

        // Below dirt (4+ blocks from top) should be stone
        if (topSolid >= 5)
        {
            Assert.AreEqual(BlockType.Stone, col[topSolid - 4]);
            Assert.AreEqual(BlockType.Stone, col[0]);
        }
    }

    [Test]
    public void Column_AboveSurfaceIsAir()
    {
        var gen = new WorldGenerator(42);
        BlockType[] col = gen.Column(5, 5, 40);

        int topSolid = -1;
        for (int i = col.Length - 1; i >= 0; i--)
        {
            if (col[i] != BlockType.Air)
            {
                topSolid = i;
                break;
            }
        }

        for (int i = topSolid + 1; i < col.Length; i++)
        {
            Assert.AreEqual(BlockType.Air, col[i],
                $"Block at y={i} should be Air above surface at y={topSolid}");
        }
    }

    [Test]
    public void Height_ReturnsDeterministicValue()
    {
        var gen = new WorldGenerator(123);
        int h1 = gen.Height(10, 10);
        int h2 = gen.Height(10, 10);

        Assert.AreEqual(h1, h2);
    }

    [Test]
    public void Height_DifferentSeedsProduceDifferentTerrain()
    {
        var gen1 = new WorldGenerator(1);
        var gen2 = new WorldGenerator(99999);

        // Check several positions - at least some should differ
        bool anyDifferent = false;
        for (int x = 0; x < 20; x++)
        {
            for (int y = 0; y < 20; y++)
            {
                if (gen1.Height(x, y) != gen2.Height(x, y))
                {
                    anyDifferent = true;
                    break;
                }
            }
            if (anyDifferent) break;
        }

        Assert.IsTrue(anyDifferent, "Different seeds should produce different terrain");
    }

    [Test]
    public void Height_IsReasonablyBounded()
    {
        var gen = new WorldGenerator(42);

        for (int x = 0; x < 50; x++)
        {
            for (int y = 0; y < 50; y++)
            {
                int h = gen.Height(x, y);
                // BaseHeight=12, HillHeight=10, DetailHeight=2
                // Range should be roughly 0..24
                Assert.GreaterOrEqual(h, 0, $"Height at ({x},{y}) should be non-negative");
                Assert.LessOrEqual(h, 40, $"Height at ({x},{y}) should not exceed max");
            }
        }
    }

    [Test]
    public void Column_ClampedToMinHeightOfOne()
    {
        var gen = new WorldGenerator(42);

        // Even with extreme coordinates, column should have at least height=1
        BlockType[] col = gen.Column(0, 0, 40);

        bool hasSolid = false;
        for (int i = 0; i < col.Length; i++)
        {
            if (col[i] != BlockType.Air)
            {
                hasSolid = true;
                break;
            }
        }

        Assert.IsTrue(hasSolid, "Column should have at least one solid block (height clamped to 1)");
    }

    [Test]
    public void Column_MaxHeightOneProducesGrassOnly()
    {
        var gen = new WorldGenerator(42);
        BlockType[] col = gen.Column(5, 5, 1);

        Assert.AreEqual(1, col.Length);
        Assert.AreEqual(BlockType.Grass, col[0]);
    }

    [Test]
    public void SameSeed_ProducesIdenticalColumns()
    {
        var gen1 = new WorldGenerator(7777);
        var gen2 = new WorldGenerator(7777);

        BlockType[] col1 = gen1.Column(25, 30, 40);
        BlockType[] col2 = gen2.Column(25, 30, 40);

        Assert.AreEqual(col1.Length, col2.Length);
        for (int i = 0; i < col1.Length; i++)
        {
            Assert.AreEqual(col1[i], col2[i], $"Mismatch at index {i}");
        }
    }
}
