using UnityEngine;

// Temporary no-op visibility mask: all blocks are fully opaque.
// Hex prism logic removed per request to simplify debugging.
public static class BlockVisibilityMask
{
    public static int Version { get; private set; }

    public static void Invalidate()
    {
        Version++;
    }

    // No-op update; returns false to indicate nothing changed.
    public static bool Update(World world, Vector3 playerPosition, Vector3 cameraPosition, float prismWidth)
    {
        return false;
    }

    // Always opaque
    public static float Opacity(int x, int y, int z)
    {
        return 1f;
    }
}