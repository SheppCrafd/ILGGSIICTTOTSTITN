using UnityEngine;

// Handles left click to break the block under the mouse and right click to place a block
// Placement is side-specific: clicking a side places on the adjacent column in that direction.
public class BlockInteractor : MonoBehaviour
{
    public Camera cam;
    public World world;
    public ChunkManager chunkManager;
    public BlockType placeType = BlockType.Dirt;

    // Raycast stepping options (raycast against heightmap by sampling along the ray)
    public float maxDistance = 100f;
    public float step = 0.1f;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        if (cam == null || world == null || chunkManager == null)
            return;

        if (Input.GetMouseButtonDown(0)) // left click -> break
            HandleBreak();

        if (Input.GetMouseButtonDown(1)) // right click -> place
            HandlePlace();
    }

    void HandleBreak()
    {
        if (TryRaycast(out Vector3 hitPoint, out Vector3 hitNormal, out int hx, out int hz, out int hy))
        {
            // break the topmost block of the column under the mouse
            if (world.RemoveTopBlock(hx, hz))
                chunkManager.RebuildChunkContaining(hx, hz);
        }
    }

    void HandlePlace()
    {
        if (TryRaycast(out Vector3 hitPoint, out Vector3 hitNormal, out int hx, out int hz, out int hy))
        {
            int targetX = hx;
            int targetZ = hz;

            // Choose target column based on which face was hit
            if (Mathf.Abs(hitNormal.x) > 0.5f)
                targetX = hx + (hitNormal.x > 0 ? 1 : -1);
            else if (Mathf.Abs(hitNormal.z) > 0.5f)
                targetZ = hz + (hitNormal.z > 0 ? 1 : -1);

            if (world.PlaceTopBlock(targetX, targetZ, placeType))
            {
                chunkManager.RebuildChunkContaining(targetX, targetZ);
            }
        }
    }

    // Raycast against the heightmap by sampling points along the ray. Returns the column hit and a heuristic normal.
    bool TryRaycast(out Vector3 hitPoint, out Vector3 hitNormal, out int hitX, out int hitZ, out int hitY)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        hitX = hitZ = hitY = 0;

        Ray r = cam.ScreenPointToRay(Input.mousePosition);

        for (float t = 0f; t <= maxDistance; t += step)
        {
            Vector3 p = r.GetPoint(t);
            int ix = Mathf.FloorToInt(p.x);
            int iz = Mathf.FloorToInt(p.z);

            if (!WorldUtils.IsInBounds(ix, iz, world.size))
                continue;

            BlockType[] col = world.Get(ix, iz);
            int colHeight = WorldUtils.ColumnHeight(col);

            if (colHeight <= 0)
                continue;

            // If the ray is at or below the column height, treat it as a hit
            if (p.y <= colHeight)
            {
                hitPoint = p;
                hitX = ix;
                hitZ = iz;
                hitY = Mathf.Clamp(Mathf.FloorToInt(p.y), 0, world.height - 1);

                float localX = p.x - ix;
                float localZ = p.z - iz;

                // Heuristic to decide whether the ray intersected a side face or the top
                if (localX < 0.15f) hitNormal = Vector3.left;
                else if (localX > 0.85f) hitNormal = Vector3.right;
                else if (localZ < 0.15f) hitNormal = Vector3.back;
                else if (localZ > 0.85f) hitNormal = Vector3.forward;
                else hitNormal = Vector3.up;

                return true;
            }
        }

        return false;
    }
}