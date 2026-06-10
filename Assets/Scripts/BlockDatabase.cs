using UnityEngine;

public class BlockDatabase : MonoBehaviour
{
    public GameObject grassPrefab;
    public GameObject dirtPrefab;
    public GameObject stonePrefab;

    public GameObject Get(BlockType type)
    {
        switch (type)
        {
            case BlockType.Grass:
                return grassPrefab;

            case BlockType.Dirt:
                return dirtPrefab;

            case BlockType.Stone:
                return stonePrefab;

            default:
                return null;
        }
    }
}
