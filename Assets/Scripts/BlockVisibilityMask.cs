using System.Collections.Generic;
using UnityEngine;

public static class BlockVisibilityMask
{
    const float FadedOpacity = 0.5f;
    const float TransparentOpacity = 0f;
    const float OpaqueOpacity = 1f;
    const float RebuildPositionStep = 5.0f;
    const float RayStep = 0.35f;

    static readonly HashSet<Vector3Int> transparentBlocks = new();
    static readonly HashSet<Vector3Int> fadedBlocks = new();
    static readonly List<Vector3Int> prismBlocks = new();

    static Vector3 lastPlayerPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);
    static Vector3 lastCameraPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);
    static float lastPrismWidth = -1f;

    public static int Version { get; private set; }

    public static void Invalidate()
    {
        lastPrismWidth = -1f;
    }

    public static bool Update(World world, Vector3 playerPosition, Vector3 cameraPosition, float prismWidth)
    {
        if (world == null)
            return false;

        if (!ShouldRebuild(playerPosition, cameraPosition, prismWidth))
            return false;

        transparentBlocks.Clear();
        fadedBlocks.Clear();
        prismBlocks.Clear();

        Vector3 playerEye = playerPosition + Vector3.up * 0.5f;
        BuildPrism(world, playerEye, cameraPosition, prismWidth);
        BuildRayHits(world, playerEye);

        lastPlayerPosition = Quantize(playerPosition);
        lastCameraPosition = Quantize(cameraPosition);
        lastPrismWidth = prismWidth;
        Version++;
        return true;
    }

    public static float Opacity(int x, int y, int z)
    {
        var block = new Vector3Int(x, y, z);

        if (fadedBlocks.Contains(block))
            return FadedOpacity;

        if (transparentBlocks.Contains(block))
            return TransparentOpacity;

        return OpaqueOpacity;
    }

    static bool ShouldRebuild(Vector3 playerPosition, Vector3 cameraPosition, float prismWidth)
    {
        return Quantize(playerPosition) != lastPlayerPosition ||
            Quantize(cameraPosition) != lastCameraPosition ||
            !Mathf.Approximately(prismWidth, lastPrismWidth);
    }

    static Vector3 Quantize(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / RebuildPositionStep) * RebuildPositionStep,
            Mathf.Round(position.y / RebuildPositionStep) * RebuildPositionStep,
            Mathf.Round(position.z / RebuildPositionStep) * RebuildPositionStep
        );
    }

    static void BuildPrism(World world, Vector3 start, Vector3 end, float prismWidth)
    {
        Vector3 axis = end - start;
        float length = axis.magnitude;

        if (length <= 0.01f)
            return;

        axis /= length;
        float radius = prismWidth * 0.5f;

        Vector3 min = Vector3.Min(start, end) - Vector3.one * radius;
        Vector3 max = Vector3.Max(start, end) + Vector3.one * radius;

        int minX = Mathf.Max(0, Mathf.FloorToInt(min.x));
        int maxX = Mathf.Min(world.size - 1, Mathf.CeilToInt(max.x));
        int minY = Mathf.Max(0, Mathf.FloorToInt(min.y));
        int maxY = Mathf.Min(world.height - 1, Mathf.CeilToInt(max.y));
        int minZ = Mathf.Max(0, Mathf.FloorToInt(min.z));
        int maxZ = Mathf.Min(world.size - 1, Mathf.CeilToInt(max.z));

        // Limit the number of blocks processed to prevent memory explosion
        int maxBlocksToProcess = 5000;
        int blocksProcessed = 0;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (blocksProcessed >= maxBlocksToProcess)
                        return;

                    if (world.GetBlock(x, y, z) == BlockType.Air)
                        continue;

                    Vector3 center = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);

                    if (!IsInsideHexPrism(center, start, axis, length, radius))
                        continue;

                    var block = new Vector3Int(x, y, z);
                    transparentBlocks.Add(block);
                    prismBlocks.Add(block);
                    blocksProcessed++;
                }
            }
        }

        prismBlocks.Sort((a, b) =>
            (a - Vector3Int.FloorToInt(start)).sqrMagnitude.CompareTo(
                (b - Vector3Int.FloorToInt(start)).sqrMagnitude
            )
        );
    }

    static bool IsInsideHexPrism(Vector3 point, Vector3 start, Vector3 axis, float length, float radius)
    {
        Vector3 relative = point - start;
        float along = Vector3.Dot(relative, axis);

        if (along < 0f || along > length)
            return false;

        Vector3 crossSection = relative - axis * along;
        Vector3 right = Vector3.Cross(axis, Vector3.up);

        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(axis, Vector3.forward);

        right.Normalize();
        Vector3 up = Vector3.Cross(right, axis).normalized;
        float u = Vector3.Dot(crossSection, right);
        float v = Vector3.Dot(crossSection, up);
        float sqrt3 = Mathf.Sqrt(3f);

        return Mathf.Abs(u) <= radius &&
            Mathf.Abs(sqrt3 * v + u) <= radius * 2f &&
            Mathf.Abs(sqrt3 * v - u) <= radius * 2f;
    }

    static void BuildRayHits(World world, Vector3 playerEye)
    {
        for (int i = 0; i < prismBlocks.Count; i++)
        {
            Vector3Int block = prismBlocks[i];

            if (IsFirstSolidOnRay(world, playerEye, block))
                fadedBlocks.Add(block);
        }
    }

    static bool IsFirstSolidOnRay(World world, Vector3 start, Vector3Int target)
    {
        Vector3 targetCenter = new Vector3(target.x + 0.5f, target.y + 0.5f, target.z + 0.5f);
        Vector3 ray = targetCenter - start;
        float length = ray.magnitude;

        if (length <= 0.01f)
            return true;

        Vector3 direction = ray / length;

        for (float distance = RayStep; distance < length; distance += RayStep)
        {
            Vector3 point = start + direction * distance;
            int x = Mathf.FloorToInt(point.x);
            int y = Mathf.FloorToInt(point.y);
            int z = Mathf.FloorToInt(point.z);

            if (x == target.x && y == target.y && z == target.z)
                continue;

            if (!world.IsInBlockBounds(x, y, z))
                continue;

            if (world.GetBlock(x, y, z) != BlockType.Air)
                return false;
        }

        return true;
    }
}
