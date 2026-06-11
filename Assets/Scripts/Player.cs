using UnityEngine;

public class Player : MonoBehaviour
{
    public World world;
    public ChunkManager chunkManager;
    public float markerSize = 0.35f;

    public float x = 16;
    public float y = 16;

    public int z;

    public float speed = 5f;
    public float sprintMultiplier = 1.7f;
    public float crouchMultiplier = 0.5f;
    public float jumpHeight = 2f;
    public float gravity = -20f;
    public float terminalVelocity = -50f;
    public int hotbarSlots = 9;
    public int inventorySlots = 27;
    public float pickupRadius = 1.25f;
    public float visibilityPrismWidth = 10f;
    public float mouseInteractionRange = 80f;
    public Camera viewCamera;

    Transform marker;
    float verticalPosition;
    float verticalVelocity;
    bool hasVerticalPosition;
    bool isGrounded = true;
    bool isCrouching;
    PlayerInventory inventory;
    int selectedHotbarIndex;
    bool showInventory;

    void Awake()
    {
        inventory = new PlayerInventory(hotbarSlots, inventorySlots);
        CreateMarker();
    }

    public void SpawnAtWorldCenter()
    {
        if (world == null)
        {
            Debug.LogError("[Player] Cannot spawn at world center: world is null.");
            return;
        }

        x = world.size * 0.5f;
        y = world.size * 0.5f;
        hasVerticalPosition = false;
        verticalVelocity = 0f;
        isGrounded = true;
        UpdatePosition();
    }

    void Update()
    {
        if (world == null)
        {
            Debug.LogWarning("[Player] world missing");
            return;
        }

        float dt = Time.deltaTime;
        float previousX = x;
        float previousY = y;
        int horizontalInput = 0;
        int verticalInput = 0;

        if (Input.GetKey(KeyCode.W)) verticalInput++;
        if (Input.GetKey(KeyCode.S)) verticalInput--;
        if (Input.GetKey(KeyCode.D)) horizontalInput++;
        if (Input.GetKey(KeyCode.A)) horizontalInput--;

        isCrouching = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.C);

        float moveSpeed = MoveSpeed();
        y += verticalInput * moveSpeed * dt;
        x += horizontalInput * moveSpeed * dt;

        x = Mathf.Clamp(x, 0, world.size - 1);
        y = Mathf.Clamp(y, 0, world.size - 1);

        // Throttle visibility mask updates to every 5 frames to prevent memory leak
        if (Time.frameCount % 5 == 0)
            UpdateVisibilityMask();

        ResolveBlockedHorizontalMovement(previousX, previousY);
        ApplyGravity(dt, Input.GetKeyDown(KeyCode.Space));
        HandleInventoryInput();
        HandleBlockInteraction();
        UpdatePosition();
    }

    float MoveSpeed()
    {
        if (isCrouching)
            return speed * crouchMultiplier;

        if (Input.GetKey(KeyCode.LeftControl))
            return speed * sprintMultiplier;

        return speed;
    }

    void ResolveBlockedHorizontalMovement(float previousX, float previousY)
    {
        if (!hasVerticalPosition)
            return;

        if (GroundHeight() <= verticalPosition)
            return;

        x = previousX;
        y = previousY;
    }

    void ApplyGravity(float dt, bool jumpPressed)
    {
        float groundHeight = GroundHeight();

        if (!hasVerticalPosition)
        {
            verticalPosition = groundHeight;
            verticalVelocity = 0f;
            hasVerticalPosition = true;
        }

        if (isGrounded && verticalPosition > groundHeight)
            isGrounded = false;

        if (isGrounded)
        {
            verticalVelocity = 0f;

            if (jumpPressed && jumpHeight > 0f && gravity < 0f)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                isGrounded = false;
            }
        }

        if (!isGrounded)
        {
            verticalVelocity = Mathf.Max(terminalVelocity, verticalVelocity + gravity * dt);
            verticalPosition += verticalVelocity * dt;

            if (verticalPosition <= groundHeight)
            {
                verticalPosition = groundHeight;
                verticalVelocity = 0f;
                isGrounded = true;
            }
        }

        z = Mathf.FloorToInt(verticalPosition);
    }

    float GroundHeight()
    {
        int ix = Mathf.FloorToInt(x);
        int iy = Mathf.FloorToInt(y);

        var col = world.Get(ix, iy);

        for (int blockY = col.Length - 1; blockY >= 0; blockY--)
        {
            if (col[blockY] != BlockType.Air && BlockVisibilityMask.Opacity(ix, blockY, iy) > 0f)
                return Mathf.Max(1, blockY + 1);
        }

        return 1;
    }

    void UpdatePosition()
    {
        CreateMarker();

        if (!hasVerticalPosition)
        {
            verticalPosition = GroundHeight();
            z = Mathf.FloorToInt(verticalPosition);
            hasVerticalPosition = true;
        }

        transform.position = new Vector3(x, verticalPosition + 0.15f, y);

        if (marker != null)
        {
            float markerHeight = isCrouching ? markerSize * 0.6f : markerSize;
            marker.localScale = new Vector3(markerSize, markerHeight, markerSize);
        }
    }

    void HandleInventoryInput()
    {
        for (int i = 0; i < inventory.HotbarSlotCount && i < 9; i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                selectedHotbarIndex = i;
        }

        if (Input.GetKeyDown(KeyCode.I))
            showInventory = !showInventory;
    }

    void HandleBlockInteraction()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F))
            BreakTargetBlock();

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.R))
            PlaceTargetBlock();
    }

    void BreakTargetBlock()
    {
        if (!TryGetMouseBlockTarget(out Vector3Int targetBlock, out _))
            return;

        // Only allow breaking blocks with 50% opacity or more
        float opacity = BlockVisibilityMask.Opacity(targetBlock.x, targetBlock.y, targetBlock.z);
        if (opacity < 0.5f)
            return;

        if (!world.TryBreakBlock(targetBlock.x, targetBlock.y, targetBlock.z, out BlockType blockType, out Vector3 dropPosition))
            return;

        BlockVisibilityMask.Invalidate();
        UpdateVisibilityMask();
        RefreshColumn(targetBlock.x, targetBlock.z);
        SpawnDroppedBlock(blockType, dropPosition);
    }

    void PlaceTargetBlock()
    {
        ItemStack selected = inventory.GetSlot(selectedHotbarIndex);

        if (selected == null || selected.IsEmpty)
            return;

        if (!TryGetMouseBlockTarget(out _, out Vector3Int placeBlock))
            return;

        if (placeBlock.y <= Mathf.FloorToInt(verticalPosition) && placeBlock.x == Mathf.FloorToInt(x) && placeBlock.z == Mathf.FloorToInt(y))
            return;

        // Only allow placing on blocks with 50% opacity or more
        float opacity = BlockVisibilityMask.Opacity(placeBlock.x, placeBlock.y, placeBlock.z);
        if (opacity < 0.5f)
            return;

        if (!world.TryPlaceBlock(placeBlock.x, placeBlock.y, placeBlock.z, selected.type))
            return;

        inventory.TryRemoveFromSlot(selectedHotbarIndex, 1);
        BlockVisibilityMask.Invalidate();
        UpdateVisibilityMask();
        RefreshColumn(placeBlock.x, placeBlock.z);
    }

    bool TryGetMouseBlockTarget(out Vector3Int targetBlock, out Vector3Int placeBlock)
    {
        targetBlock = Vector3Int.zero;
        placeBlock = Vector3Int.zero;

        Camera camera = GetViewCamera();

        if (camera == null)
            return false;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        bool hasPlaceBlock = false;
        Vector3Int lastAirLikeBlock = Vector3Int.zero;

        for (float distance = 0f; distance <= mouseInteractionRange; distance += 0.1f)
        {
            Vector3 point = ray.origin + ray.direction * distance;
            int blockX = Mathf.FloorToInt(point.x);
            int blockY = Mathf.FloorToInt(point.y);
            int blockZ = Mathf.FloorToInt(point.z);

            if (!world.IsInBlockBounds(blockX, blockY, blockZ))
                continue;

            var block = new Vector3Int(blockX, blockY, blockZ);
            BlockType type = world.GetBlock(blockX, blockY, blockZ);
            float opacity = BlockVisibilityMask.Opacity(blockX, blockY, blockZ);

            if (type == BlockType.Air || opacity <= 0f)
            {
                if (!hasPlaceBlock || block != lastAirLikeBlock)
                {
                    lastAirLikeBlock = block;
                    hasPlaceBlock = true;
                }

                continue;
            }

            targetBlock = block;
            placeBlock = hasPlaceBlock ? lastAirLikeBlock : block + Vector3Int.up;
            return true;
        }

        return false;
    }

    Camera GetViewCamera()
    {
        if (viewCamera != null)
            return viewCamera;

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
            return mainCamera;

        return FindAnyObjectByType<Camera>();
    }

    void UpdateVisibilityMask()
    {
        Camera camera = GetViewCamera();

        if (camera != null)
            BlockVisibilityMask.Update(world, CurrentWorldPosition(), camera.transform.position, visibilityPrismWidth);
    }

    public Vector3 CurrentWorldPosition()
    {
        if (!hasVerticalPosition)
            return transform.position;

        return new Vector3(x, verticalPosition + 0.15f, y);
    }

    void RefreshColumn(int targetX, int targetZ)
    {
        if (chunkManager == null)
            chunkManager = FindAnyObjectByType<ChunkManager>();

        if (chunkManager != null)
            chunkManager.RefreshColumn(targetX, targetZ);
    }

    void SpawnDroppedBlock(BlockType blockType, Vector3 dropPosition)
    {
        BlockDatabase blockDatabase = GetBlockDatabase();
        GameObject prefab = blockDatabase != null ? blockDatabase.Get(blockType) : null;
        GameObject item = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);

        item.name = $"{blockType} Drop";
        item.transform.position = dropPosition;
        item.transform.localScale = Vector3.one * 0.35f;

        var droppedItem = item.AddComponent<DroppedBlockItem>();
        droppedItem.pickupRadius = pickupRadius;
        droppedItem.blockDatabase = blockDatabase;
        droppedItem.Init(blockType, 1);
    }

    BlockDatabase GetBlockDatabase()
    {
        if (chunkManager != null && chunkManager.blockDatabase != null)
            return chunkManager.blockDatabase;

        return FindAnyObjectByType<BlockDatabase>();
    }

    public int TryCollectBlock(BlockType blockType, int amount)
    {
        if (inventory == null)
            inventory = new PlayerInventory(hotbarSlots, inventorySlots);

        return inventory.Add(blockType, amount);
    }

    void OnGUI()
    {
        if (inventory == null)
            return;

        const int slotSize = 58;
        const int gap = 6;
        int totalWidth = inventory.HotbarSlotCount * slotSize + (inventory.HotbarSlotCount - 1) * gap;
        int startX = (Screen.width - totalWidth) / 2;
        int yPosition = Screen.height - slotSize - 18;

        for (int i = 0; i < inventory.HotbarSlotCount; i++)
        {
            Rect slotRect = new Rect(startX + i * (slotSize + gap), yPosition, slotSize, slotSize);
            DrawHotbarSlot(slotRect, i);
        }

        if (showInventory)
            DrawInventoryPanel(startX, yPosition, slotSize, gap);
    }

    void DrawInventoryPanel(int startX, int hotbarY, int slotSize, int gap)
    {
        const int columns = 9;
        int rows = Mathf.CeilToInt((float)(inventory.SlotCount - inventory.HotbarSlotCount) / columns);
        int panelWidth = columns * slotSize + (columns - 1) * gap;
        int panelStartX = Mathf.Clamp(startX, 8, Mathf.Max(8, Screen.width - panelWidth - 8));
        int panelStartY = Mathf.Max(8, hotbarY - rows * (slotSize + gap) - 18);

        for (int i = inventory.HotbarSlotCount; i < inventory.SlotCount; i++)
        {
            int gridIndex = i - inventory.HotbarSlotCount;
            int row = gridIndex / columns;
            int col = gridIndex % columns;
            Rect slotRect = new Rect(
                panelStartX + col * (slotSize + gap),
                panelStartY + row * (slotSize + gap),
                slotSize,
                slotSize
            );

            DrawInventorySlot(slotRect, i);
        }
    }

    void DrawHotbarSlot(Rect slotRect, int index)
    {
        Color previousColor = GUI.color;

        if (index == selectedHotbarIndex)
            GUI.color = Color.yellow;

        GUI.Box(slotRect, string.Empty);
        GUI.color = previousColor;

        ItemStack stack = inventory.GetSlot(index);
        string label = $"{index + 1}";

        if (stack != null && !stack.IsEmpty)
            label = $"{index + 1}\n{BlockLabel(stack.type)}\n{stack.amount}";

        GUI.Label(slotRect, label);
    }

    void DrawInventorySlot(Rect slotRect, int index)
    {
        GUI.Box(slotRect, string.Empty);
        ItemStack stack = inventory.GetSlot(index);

        if (stack == null || stack.IsEmpty)
            return;

        GUI.Label(slotRect, $"{BlockLabel(stack.type)}\n{stack.amount}");
    }

    string BlockLabel(BlockType blockType)
    {
        switch (blockType)
        {
            case BlockType.Grass:
                return "Grass";

            case BlockType.Dirt:
                return "Dirt";

            case BlockType.Stone:
                return "Stone";

            default:
                return string.Empty;
        }
    }

    void CreateMarker()
    {
        if (marker != null)
            return;

        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "Player Dot";
        dot.transform.SetParent(transform, false);
        dot.transform.localPosition = Vector3.zero;
        dot.transform.localScale = Vector3.one * markerSize;

        var renderer = dot.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = WorldUtils.CreateFallbackMaterial("Player", Color.red);
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("[Player] 'Standard' shader not found. Ensure it is included in Always Included Shaders.");
            return;
        }
        renderer.sharedMaterial = new Material(shader);
        renderer.sharedMaterial.color = Color.red;

        var collider = dot.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        marker = dot.transform;
    }
}