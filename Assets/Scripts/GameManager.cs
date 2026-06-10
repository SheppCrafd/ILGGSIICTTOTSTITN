using UnityEngine;

public class GameManager : MonoBehaviour
{
    public Player player;
    public World world;
    public ChunkManager chunkManager;

    void Awake()
    {
        if (world == null)
        {
            Debug.LogError("[GameManager] World reference is not assigned.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[GameManager] Player reference is not assigned.");
            return;
        }

        int seed = Random.Range(0, 999999);

        world.Init(seed);

        player.world = world;
        player.SpawnAtWorldCenter();

        if (chunkManager != null)
            chunkManager.world = world;
        else
            Debug.LogWarning("[GameManager] ChunkManager is not assigned; terrain will not stream.");

#if UNITY_EDITOR
        Debug.Log("[GameManager] World seed: " + seed);
#endif
    }
}
