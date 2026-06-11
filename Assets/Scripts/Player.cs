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

    Transform marker;
    float verticalPosition;
    float verticalVelocity;
    bool hasVerticalPosition;
    bool isGrounded = true;
    bool isCrouching;
    Vector2Int facingDirection = Vector2Int.up;
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

        UpdateFacing(horizontalInput, verticalInput);
        isCrouching = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.C);

        float moveSpeed = MoveSpeed();
        y += verticalInput * moveSpeed * dt;
        x += horizontalInput * moveSpeed * dt;

        x = Mathf.Clamp(x, 0, world.size - 1);
        y = Mathf.Clamp(y, 0, world.size - 1);

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

    void UpdateFacing(int horizontalInput, int verticalInput)
    {
        if (horizontalInput == 0 && verticalInput == 0)
            return;

        if (Mathf.Abs(horizontalInput) > Mathf.Abs(verticalInput))
            facingDirection = new Vector2Int(horizontalInput > 0 ? 1 : -1, 0);
        else
            facingDirection = new Vector2Int(0, verticalInput > 0 ? 1 : -1);
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

        return Mathf.Max(1, WorldUtils.ColumnHeight(col));
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
        if (!TryGetTargetColumn(out int targetX, out int targetZ))
            return;

        if (!world.TryBreakTopBlock(targetX, targetZ, out BlockType blockType, out Vector3 dropPosition))
            return;

        RefreshColumn(targetX, targetZ);
        SpawnDroppedBlock(blockType, dropPosition);
    }

    void PlaceTargetBlock()
    {
        ItemStack selected = inventory.GetSlot(selectedHotbarIndex);

        if (selected == null || selected.IsEmpty)
            return;

        if (!TryGetTargetColumn(out int targetX, out int targetZ))
            return;

        if (GroundHeightAt(targetX, targetZ) > verticalPosition)
            return;

        if (!world.TryPlaceTopBlock(targetX, targetZ, selected.type))
            return;

        inventory.TryRemoveFromSlot(selectedHotbarIndex, 1);
        RefreshColumn(targetX, targetZ);
    }

    bool TryGetTargetColumn(out int targetX, out int targetZ)
    {
        targetX = Mathf.FloorToInt(x) + facingDirection.x;
        targetZ = Mathf.FloorToInt(y) + facingDirection.y;
        return WorldUtils.IsInBounds(targetX, targetZ, world.size);
    }

    float GroundHeightAt(int targetX, int targetZ)
    {
        return Mathf.Max(1, WorldUtils.ColumnHeight(world.Get(targetX, targetZ)));
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
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = $"{blockType} Drop";
        item.transform.position = dropPosition;
        item.transform.localScale = Vector3.one * 0.35f;

        var droppedItem = item.AddComponent<DroppedBlockItem>();
        droppedItem.pickupRadius = pickupRadius;
        droppedItem.Init(blockType, 1);
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