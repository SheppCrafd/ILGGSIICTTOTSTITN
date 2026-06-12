using UnityEngine;

// World item entity that holds an Item and a count.
// Includes a simple pickup component that adds to the player's Inventory (InventorySystem.Inventory)
public class ItemEntity : MonoBehaviour
{
    // The core Item from InventorySystem (serializable)
    public Item item;
    public int count = 1;

    // Optional simple visual; will create a sphere if none present when spawnTestOnStart is true
    public bool spawnTestOnStart = false;

    void Start()
    {
        if (spawnTestOnStart && item == null)
        {
            item = new Item("test_item", "Test Item");
            count = 10;

            // Create a simple visual object so the item is visible in scene
            if (transform.childCount == 0)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.5f;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.sharedMaterial = WorldUtils.CreateFallbackMaterial("ItemEntity", Color.yellow);

                var col = go.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);
            }
        }
    }
}

// Component that handles player pickup interaction for an ItemEntity.
[RequireComponent(typeof(ItemEntity))]
public class ItemPickup : MonoBehaviour
{
    public float pickupRange = 2f;
    public KeyCode pickupKey = KeyCode.E;

    ItemEntity itemEntity;
    Player player;

    void Awake()
    {
        itemEntity = GetComponent<ItemEntity>();
    }

    void Start()
    {
        player = FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogWarning("[ItemPickup] No Player found in scene. Assign player manually if needed.");
        }
        else
        {
            Debug.Log($"[ItemPickup] Found player at {player.transform.position}, pickupRange={pickupRange}");
        }
    }

    bool sawPlayerInRange = false;

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
            if (player != null)
                Debug.Log($"[ItemPickup] Late-found player at {player.transform.position}");
            else
                return;
        }

        float dist = Vector3.Distance(player.transform.position, transform.position);
        if (dist > pickupRange)
            return;

        if (!sawPlayerInRange)
        {
            Debug.Log($"[ItemPickup] Player entered pickup range. PlayerPos={player.transform.position}, ItemPos={transform.position}, dist={dist}");
            sawPlayerInRange = true;
        }

        if (Input.GetKeyDown(pickupKey))
        {
            TryPickup();
        }
    }

    void TryPickup()
    {
        if (itemEntity == null || itemEntity.item == null || itemEntity.count <= 0)
            return;

        // Ensure player has an Inventory; if not, create one
        if (player.inventory == null)
            player.inventory = new Inventory();

        int leftover = player.inventory.AddItem(itemEntity.item, itemEntity.count);

        if (leftover <= 0)
        {
            Debug.Log($"[ItemPickup] Picked up {itemEntity.count} x {itemEntity.item.name}. PlayerPos={player.transform.position}, ItemPos={transform.position}, dist={Vector3.Distance(player.transform.position, transform.position)}, pickupRange={pickupRange}");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log($"[ItemPickup] Picked up {itemEntity.count - leftover} x {itemEntity.item.name}; {leftover} left in world. PlayerPos={player.transform.position}, ItemPos={transform.position}, dist={Vector3.Distance(player.transform.position, transform.position)}, pickupRange={pickupRange}");
            itemEntity.count = leftover;
        }
    }
}