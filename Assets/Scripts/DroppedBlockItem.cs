using UnityEngine;

public class DroppedBlockItem : MonoBehaviour
{
    public BlockType type;
    public int amount = 1;
    public float pickupRadius = 1.25f;
    public float pickupDelay = 0.25f;

    float age;
    float baseY;

    public void Init(BlockType blockType, int itemAmount)
    {
        type = blockType;
        amount = itemAmount;
        baseY = transform.position.y;
        ApplyMaterial();
    }

    void Start()
    {
        baseY = transform.position.y;
        ApplyMaterial();
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
        Vector3 position = transform.position;
        position.y = baseY + Mathf.Sin(Time.time * 4f) * 0.08f;
        transform.position = position;

        if (age < pickupDelay)
            return;

        Player player = FindAnyObjectByType<Player>();

        if (player == null)
            return;

        if (Vector3.Distance(transform.position, player.transform.position) > pickupRadius)
            return;

        int remaining = player.TryCollectBlock(type, amount);

        if (remaining <= 0)
            Destroy(gameObject);
        else
            amount = remaining;
    }

    void ApplyMaterial()
    {
        var renderer = GetComponent<MeshRenderer>();

        if (renderer == null)
            return;

        renderer.sharedMaterial = WorldUtils.CreateFallbackMaterial($"{type} Drop", WorldUtils.BlockColor(type));
    }
}
