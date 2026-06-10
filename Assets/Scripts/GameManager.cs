using UnityEngine;

public class GameManager : MonoBehaviour
{
    public Player player;
    public World world;
    public ChunkManager chunkManager;

    void Awake()
    {
        int seed = Random.Range(0, 999999);

        world.Init(seed);

        player.world = world;
        player.SpawnAtWorldCenter();

        if (chunkManager != null)
            chunkManager.world = world;

#if UNITY_EDITOR
        Debug.Log("World seed: " + seed);
#endif
    }
}
