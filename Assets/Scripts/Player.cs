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
    public int inventorySlots = 36;
    public float pickupRadius = 1.25f;
    public float visibilityPrismWidth = 10f;
    public float mouseInteractionRange = 80f;
    public Camera viewCamera;
    public bool debugPositionLogging = true;

    Transform marker;
    float verticalPosition;
    float verticalVelocity;
    bool hasVerticalPosition;
    bool isGrounded = true;
    bool isCrouching;
    // Single canonical inventory (item-based, includes hotbar)
    public Inventory inventory;
    bool showInventory;

    void Awake()
    {
        // Use a single canonical inventory: 36 slots total, first 9 are hotbar (3 rows of 9 in the inventory panel).
        inventory = new Inventory(36, 9);

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
        // Use GetKey so holding space will re-trigger jumps when landing
        ApplyGravity(dt, Input.GetKey(KeyCode.Space));
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

        // Compute ground height at the (already-updated) x,y position
        float newGround = GroundHeight();

        // If crouching, prevent walking off edges (don't allow dropping to a lower ground level)
        // Small epsilon to avoid jitter from floating point noise
        if (isCrouching && newGround < verticalPosition - 0.05f)
        {
            x = previousX;
            y = previousY;
            return;
        }

        // Prevent moving into higher solid ground that would intersect the player (original behavior)
        if (newGround > verticalPosition + 0.001f)
        {
            x = previousX;
            y = previousY;
        }
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

                // If player is holding jump (space), immediately jump again
                if (jumpPressed && jumpHeight > 0f && gravity < 0f)
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    isGrounded = false;
                }
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

        if (debugPositionLogging)
        {
            int px = Mathf.FloorToInt(x);
            int py = Mathf.FloorToInt(y);
            int floorY = Mathf.FloorToInt(verticalPosition);
            string colInfo = "N/A";
            try
            {
                if (world != null && world.IsInBlockBounds(px, floorY, py))
                {
                    var col = world.Get(px, py);
                    int height = col != null ? col.Length : -1;
                    int sampleBelow = Mathf.Clamp(floorY - 1, 0, Mathf.Max(0, height - 1));
                    BlockType blockBelow = BlockType.Air;
                    if (col != null && sampleBelow >= 0 && sampleBelow < col.Length)
                        blockBelow = col[sampleBelow];
                    colInfo = $"colHeight={height}, blockBelow={blockBelow}, floorY={floorY}";
                }
            }
            catch (System.Exception ex)
            {
                colInfo = "error reading column: " + ex.Message;
            }

            Debug.Log($"[PlayerDebug] transform={transform.position}, player.x={x}, player.y={y}, z={z}, verticalPosition={verticalPosition}, floorY={floorY}, px={px},py={py}, pickupRadius={pickupRadius}, {colInfo}");
        }

        if (marker != null)
        {
            // Keep marker positioned at player's world position even if the marker is not parented to the player
            marker.position = transform.position;
            float markerHeight = isCrouching ? markerSize * 0.6f : markerSize;
            marker.localScale = new Vector3(markerSize, markerHeight, markerSize);
        }
    }

    // hotbarOffset: which inventory index maps to the hotbar's leftmost slot (0..SlotCount - HotbarSlotCount)
    public int hotbarOffset = 0;

    void HandleInventoryInput()
    {
        // Number keys 1-9 select hotbar slots (within the visible bottom row)
        for (int i = 0; i < inventory.HotbarSlotCount && i < 9; i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                inventory.SelectHotbarSlot(i);
        }

        // Mouse wheel scrolls the bottom row window (hotbarOffset)
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f)
            AdjustHotbarOffset(-1);
        else if (scroll < 0f)
            AdjustHotbarOffset(1);

        // Toggle inventory visibility with E (top 3 rows visible). Bottom row (hotbar) is always visible.
        if (Input.GetKeyDown(KeyCode.E))
            showInventory = !showInventory;
    }

    void AdjustHotbarOffset(int dir)
    {
        if (inventory == null) return;
        int maxOffset = Mathf.Max(0, inventory.SlotCount - inventory.HotbarSlotCount);
        hotbarOffset = (hotbarOffset + dir) % (maxOffset + 1);
        if (hotbarOffset < 0) hotbarOffset += maxOffset + 1;
    }

    void HandleBlockInteraction()
    {
        // Block interactions while IMGUI inventory panel is visible
        if (showInventory)
            return;

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

        // Determine selected tool/item (for future tool logic; no durability yet)
        var selectedItem = inventory.GetSlot(Mathf.Clamp(hotbarOffset + inventory.SelectedHotbarIndex, 0, inventory.SlotCount - 1));
        string toolId = selectedItem != null && selectedItem.item != null ? selectedItem.item.id : "none";

        Debug.Log($"[Break] Attempting to break block at {targetBlock}");
        if (!world.TryBreakBlock(targetBlock.x, targetBlock.y, targetBlock.z, out BlockType blockType, out Vector3 dropPosition))
        {
            Debug.Log($"[Break] Failed to break block at {targetBlock}");
            return;
        }

        Debug.Log($"[Break] Broke block {blockType} at {targetBlock} -> dropPos={dropPosition}");

        // (Optional) could modify drops based on toolId in future

        BlockVisibilityMask.Invalidate();
        UpdateVisibilityMask();
        RefreshColumn(targetBlock.x, targetBlock.z);
        SpawnDroppedBlock(blockType, dropPosition);
    }

    void PlaceTargetBlock()
    {
        // Get the item stack in the selected hotbar slot
        var selectedItem = inventory.GetSlot(Mathf.Clamp(hotbarOffset + inventory.SelectedHotbarIndex, 0, inventory.SlotCount - 1));
        if (selectedItem == null || selectedItem.item == null || selectedItem.count <= 0)
            return;

        // Ensure it represents a block (id starts with block_)
        if (!selectedItem.item.id.StartsWith("block_"))
            return;

        string rest = selectedItem.item.id.Substring("block_".Length);
        if (!System.Enum.TryParse<BlockType>(rest, out BlockType blockType))
            return;

        if (!TryGetMouseBlockTarget(out _, out Vector3Int placeBlock))
            return;

        if (placeBlock.y <= Mathf.FloorToInt(verticalPosition) && placeBlock.x == Mathf.FloorToInt(x) && placeBlock.z == Mathf.FloorToInt(y))
            return;

        // Only allow placing on blocks with 50% opacity or more
        float opacity = BlockVisibilityMask.Opacity(placeBlock.x, placeBlock.y, placeBlock.z);
        if (opacity < 0.5f)
            return;

        if (!world.TryPlaceBlock(placeBlock.x, placeBlock.y, placeBlock.z, blockType))
            return;

        inventory.TryRemoveFromSlot(Mathf.Clamp(hotbarOffset + inventory.SelectedHotbarIndex, 0, inventory.SlotCount - 1), 1);
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
        // Spawn an ItemEntity representing the block drop so the ItemPickup system can handle collection.
        GameObject go = new GameObject($"{blockType} Drop (ItemEntity)");
        go.transform.position = dropPosition;
        go.transform.localScale = Vector3.one * 0.35f;

        // Visual: simple sphere child
        var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vis.transform.SetParent(go.transform, false);
        vis.transform.localScale = Vector3.one;
        var mr = vis.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.sharedMaterial = WorldUtils.CreateFallbackMaterial($"{blockType} Drop", WorldUtils.BlockColor(blockType));
        var col = vis.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var itemEntity = go.AddComponent<ItemEntity>();
        itemEntity.item = new Item($"block_{blockType}", blockType.ToString());
        itemEntity.count = 1;

        var pickup = go.AddComponent<ItemPickup>();
        pickup.pickupRange = pickupRadius;

        // Debug: log spawn positions to help diagnose alignment
        try
        {
            Debug.Log($"[SpawnDrop] Spawned {blockType} Drop at {go.transform.position}. PlayerWorldPos={CurrentWorldPosition()}, player.transform.position={transform.position}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SpawnDrop] Error logging spawn info: {ex}");
        }
    }

    public BlockDatabase GetBlockDatabase()
    {
        if (chunkManager != null && chunkManager.blockDatabase != null)
            return chunkManager.blockDatabase;

        return FindAnyObjectByType<BlockDatabase>();
    }

    public int TryCollectBlock(BlockType blockType, int amount)
    {
        if (inventory == null)
            inventory = new Inventory();

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

        // Always draw the bottom-row hotbar (now part of the inventory UI)
        for (int i = 0; i < inventory.HotbarSlotCount; i++)
        {
            Rect slotRect = new Rect(startX + i * (slotSize + gap), yPosition, slotSize, slotSize);
            // Enlarge selected slot slightly for emphasis
            if (i == inventory.SelectedHotbarIndex)
            {
                slotRect = new Rect(slotRect.x - 3, slotRect.y - 3, slotRect.width + 6, slotRect.height + 6);
            }
            DrawHotbarSlot(slotRect, i);
        }

        if (showInventory)
            DrawInventoryPanel(startX, yPosition, slotSize, gap);

        // Draw drag preview for IMGUI when dragging (HotbarUI also shows a UI drag image for canvas-driven drags)
        if (DragAndDropManager.IsDragging)
        {
            Event e = Event.current;
            Vector2 mp = e.mousePosition;
            Rect pr = new Rect(mp.x - 16, mp.y - 16, 32, 32);
            if (DragAndDropManager.DragTexture != null)
            {
                GUI.DrawTexture(pr, DragAndDropManager.DragTexture);
            }
            else
            {
                Color prev = GUI.color;
                GUI.color = DragAndDropManager.DragColor;
                GUI.Box(pr, string.Empty);
                GUI.color = prev;
            }

            // If mouse released outside any slot, cancel the drag to avoid stuck state
            if (e.type == EventType.MouseUp)
            {
                bool overAny = false;
                // check hotbar
                for (int i = 0; i < inventory.HotbarSlotCount; i++)
                {
                    Rect slotRect = new Rect(startX + i * (slotSize + gap), yPosition, slotSize, slotSize);
                    if (slotRect.Contains(e.mousePosition)) { overAny = true; break; }
                }
                // check inventory grid if visible
                if (!overAny && showInventory)
                {
                    const int columns = 9;
                    int rows = Mathf.CeilToInt((float)(inventory.SlotCount - inventory.HotbarSlotCount) / columns);
                    int panelWidth = columns * slotSize + (columns - 1) * gap;
                    int panelStartX = Mathf.Clamp(startX, 8, Mathf.Max(8, Screen.width - panelWidth - 8));
                    int panelStartY = Mathf.Max(8, yPosition - rows * (slotSize + gap) - 18);
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
                        if (slotRect.Contains(e.mousePosition)) { overAny = true; break; }
                    }
                }

                if (!overAny)
                {
                    DragAndDropManager.CancelDrag(inventory);
                    e.Use();
                }
            }
        }
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

        if (inventory != null && index == inventory.SelectedHotbarIndex)
            GUI.color = Color.yellow;

        GUI.Box(slotRect, string.Empty);
        GUI.color = previousColor;

        int absIndex = Mathf.Clamp(hotbarOffset + index, 0, inventory.SlotCount - 1);
        var slot = inventory.GetSlot(absIndex);
        string label = $"{index + 1}";

        Event e = Event.current;
        // Mouse interactions for hotbar slots (start drag / drop)
        if (e.type == EventType.MouseDown && slotRect.Contains(e.mousePosition))
        {
            // If dragging, drop into this hotbar slot
            if (DragAndDropManager.IsDragging)
            {
                DragAndDropManager.DropToSlot(inventory, absIndex);
                e.Use();
                return;
            }
            else
            {
                // start drag from this hotbar slot
                if (slot != null && slot.item != null && slot.count > 0)
                {
                    // Prepare texture and color
                    Texture2D tex = null;
                    Color col = Color.white;
                    if (slot.item.id.StartsWith("block_"))
                    {
                        string rest = slot.item.id.Substring("block_".Length);
                        if (System.Enum.TryParse<BlockType>(rest, out BlockType bt))
                        {
                            var db = GetBlockDatabase();
                            var t = WorldUtils.FindBlockTexture(db, bt);
                            if (t != null) tex = t;
                        }
                    }

                    int amount = slot.count;
                    // Shift -> pick single, Right mouse -> split half
                    if (e.shift)
                        amount = 1;
                    else if (e.button == 1 && slot.count > 1)
                        amount = Mathf.CeilToInt(slot.count / 2f);

                    DragAndDropManager.StartDrag(inventory, absIndex, true, amount, tex, col);
                    e.Use();
                    return;
                }
            }
        }

        // Mouse up while dragging -> drop into this hotbar slot
        if (e.type == EventType.MouseUp && DragAndDropManager.IsDragging && slotRect.Contains(e.mousePosition))
        {
            DragAndDropManager.DropToSlot(inventory, absIndex);
            e.Use();
            return;
        }

        if (slot != null && slot.item != null && slot.count > 0 && slot.item.id.StartsWith("block_"))
        {
            string rest = slot.item.id.Substring("block_".Length);
            if (System.Enum.TryParse<BlockType>(rest, out BlockType bt))
            {
                // Draw icon from BlockDatabase if available
                var db = GetBlockDatabase();
                var tex = WorldUtils.FindBlockTexture(db, bt);
                Rect iconRect = new Rect(slotRect.x + 4, slotRect.y + 4, slotRect.width - 8, slotRect.height - 8);
                if (tex != null)
                {
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(slotRect.x, slotRect.y, slotRect.width, 16), $"{BlockLabel(bt)}");
                    GUI.Label(new Rect(slotRect.x, slotRect.y + slotRect.height - 18, slotRect.width, 18), $"{slot.count}");
                }
                else
                {
                    label = $"{index + 1}\n{BlockLabel(bt)}\n{slot.count}";
                    GUI.Label(slotRect, label);
                }
            }
        }
        else
        {
            GUI.Label(slotRect, label);
        }
    }

    void DrawInventorySlot(Rect slotRect, int index)
    {
        GUI.Box(slotRect, string.Empty);
        var slot = inventory.GetSlot(index);

        // Handle mouse interaction for dragging/dropping
        Event e = Event.current;
        if (e.type == EventType.MouseDown && slotRect.Contains(e.mousePosition))
        {
            // If we're currently dragging, drop into this slot
            if (DragAndDropManager.IsDragging)
            {
                DragAndDropManager.DropToSlot(inventory, index);
                e.Use();
                return;
            }
            else
            {
                // start drag from this inventory slot
                if (slot != null && slot.item != null && slot.count > 0)
                {
                    // Try to prepare a texture for the drag visual (if this is a block)
                    Texture2D tex = null;
                    Color col = Color.white;
                    if (slot.item.id.StartsWith("block_"))
                    {
                        string rest = slot.item.id.Substring("block_".Length);
                        if (System.Enum.TryParse<BlockType>(rest, out BlockType bt))
                        {
                            var db = GetBlockDatabase();
                            var mat = WorldUtils.FindBlockMaterial(db, bt, null);
                            if (mat != null && mat.mainTexture is Texture2D t)
                                tex = t;
                        }
                    }

                    int amount = slot.count;
                    // Shift -> pick single, Right mouse button -> pick half (split)
                    if (e.shift)
                        amount = 1;
                    else if (e.button == 1 && slot.count > 1)
                        amount = Mathf.CeilToInt(slot.count / 2f);
                    DragAndDropManager.StartDrag(inventory, index, false, amount, tex, col);
                    e.Use();
                    return;
                }
            }
        }

        // Mouse up while dragging (covers cases where mouse down wasn't on a slot)
        if (e.type == EventType.MouseUp && DragAndDropManager.IsDragging && slotRect.Contains(e.mousePosition))
        {
            // Try drop onto hotbar first if hotbar exists
            var hotbar = FindAnyObjectByType<HotbarUI>();

            if (hotbar == null || !hotbar.gameObject.activeInHierarchy)
            {
                DragAndDropManager.DropToSlot(inventory, index);
                e.Use();
                return;
            }

        if (hotbar == null || !hotbar.gameObject.activeInHierarchy)
        {
            DragAndDropManager.DropToSlot(inventory, index);
            e.Use();
            return;
        }

            DragAndDropManager.DropToSlot(inventory, index);
            e.Use();
            return;
        }

        if (slot == null || slot.item == null || slot.count <= 0)
            return;

        if (slot.item.id.StartsWith("block_"))
        {
            string rest = slot.item.id.Substring("block_".Length);
            if (System.Enum.TryParse<BlockType>(rest, out BlockType bt))
            {
                // Draw icon using BlockDatabase textures (prefer side texture for grass)
                var db = GetBlockDatabase();
                var tex = WorldUtils.FindBlockTexture(db, bt);
                Rect iconRect = new Rect(slotRect.x + 4, slotRect.y + 4, slotRect.width - 8, slotRect.height - 8);
                if (tex != null)
                {
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(slotRect.x, slotRect.y, slotRect.width, 16), $"{BlockLabel(bt)}");
                    GUI.Label(new Rect(slotRect.x, slotRect.y + slotRect.height - 18, slotRect.width, 18), $"{slot.count}");
                }
                else
                {
                    GUI.Label(slotRect, $"{BlockLabel(bt)}\n{slot.count}");
                }
            }
        }
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
        // Place marker in world root so UI parenting or canvas won't move it unexpectedly.
        dot.transform.SetParent(null);
        dot.transform.position = transform.position;
        dot.transform.localScale = Vector3.one * markerSize;

        var renderer = dot.GetComponent<MeshRenderer>();
        // Use robust fallback material (supports URP/HDRP/Built-in). Do NOT override with Standard shader.
        var mat = WorldUtils.CreateFallbackMaterial("Player", Color.red);
        if (mat != null)
            renderer.sharedMaterial = mat;
        renderer.enabled = true;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var collider = dot.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        marker = dot.transform;

        // Ensure marker is on the Default layer so main camera renders it by default
        try
        {
            dot.layer = LayerMask.NameToLayer("Default");
        }
        catch { dot.layer = 0; }

        // Ensure renderer is enabled
        var rend = dot.GetComponent<MeshRenderer>();
        if (rend != null)
            rend.enabled = true;

        // Debug: log marker and renderer info to help diagnose visibility
        try
        {
            Debug.Log($"[Player] Created marker '{dot.name}' at {dot.transform.position}. Renderer enabled={rend != null && rend.enabled}, shader={(rend != null && rend.sharedMaterial && rend.sharedMaterial.shader != null ? rend.sharedMaterial.shader.name : "null")}, color={(rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.color.ToString() : "null")} ");
            Camera cam = GetViewCamera();
            if (cam != null)
                Debug.Log($"[Player] Camera: {cam.name}, cullingMask={cam.cullingMask}, position={cam.transform.position}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Player] Error logging marker info: {ex}");
        }
    }
}