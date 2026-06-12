using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Runtime-built hotbar UI. Reads the player's Inventory (InventorySystem.Inventory) and shows
// the first 9 slots. Highlights Inventory.SelectedHotbarIndex. Uses block textures from Assets/Textures when available.
[RequireComponent(typeof(Canvas))]
public class HotbarUI : MonoBehaviour
{
    public Player player;

    Canvas canvas;
    RectTransform hotbarRoot;

    const int slotCount = 9;
    // Match IMGUI hotbar sizing to avoid overlap: slotSize=58, gap=6
    const int slotSize = 58;
    const int padding = 6;

    class SlotUi
    {
        public Image background;
        public Image icon;
        public Text countText;
        public Image highlight;
    }

    SlotUi[] slots = new SlotUi[slotCount];

    Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    void Awake()
    {
        // Try to find player if not assigned
        if (player == null)
            player = FindAnyObjectByType<Player>();

        // Build Canvas if not present
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        // Use ConstantPixelSize so runtime UI matches IMGUI pixel sizing and avoids unintended scaling
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        var raycaster = gameObject.GetComponent<GraphicRaycaster>() ?? gameObject.AddComponent<GraphicRaycaster>();

        // Load sprites from BlockDatabase at runtime; fallback to colored squares.
        LoadSpritesFromBlockDatabase();

        // Create hotbar root
        var go = new GameObject("HotbarRoot", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        hotbarRoot = go.GetComponent<RectTransform>();
        hotbarRoot.anchorMin = new Vector2(0.5f, 0f);
        hotbarRoot.anchorMax = new Vector2(0.5f, 0f);
        hotbarRoot.pivot = new Vector2(0.5f, 0f);
        // Place hotbar at bottom-center matching IMGUI position
        hotbarRoot.anchoredPosition = new Vector2(0, 18);
        int totalWidth = slotCount * slotSize + (slotCount - 1) * padding;
        hotbarRoot.sizeDelta = new Vector2(totalWidth, slotSize + padding * 2);

        // Build slots
        for (int i = 0; i < slotCount; i++)
        {
            var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
            slotGo.transform.SetParent(hotbarRoot, false);
            var rt = slotGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(slotSize, slotSize);
            float x = (i - (slotCount - 1) / 2.0f) * (slotSize + padding);
            rt.anchoredPosition = new Vector2(x, 0);

            // Add drag handler to this slot so it can start drags
            var dragHandler = slotGo.AddComponent<UIDragHandler>();
            dragHandler.parent = this;
            dragHandler.slotIndex = i;

            // background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(slotGo.transform, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.15f); // reduce darkening of block textures
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

            // icon
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slotGo.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.color = new Color(1f, 1f, 1f, 0.0f); // invisible until set
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.1f); iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
            // Make sure icon participates in raycasts for pointer events
            icon.raycastTarget = true;

            // count text
            var txtGo = new GameObject("Count", typeof(RectTransform));
            txtGo.transform.SetParent(slotGo.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.alignment = TextAnchor.LowerRight;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 18;
            txt.color = Color.white;
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.offsetMin = new Vector2(6, 6); txtRt.offsetMax = new Vector2(-6, -6);

            // highlight overlay
            var hlGo = new GameObject("Highlight", typeof(RectTransform));
            hlGo.transform.SetParent(slotGo.transform, false);
            var hl = hlGo.AddComponent<Image>();
            hl.color = new Color(1f, 1f, 0f, 0f); // transparent by default
            var hlRt = hlGo.GetComponent<RectTransform>();
            hlRt.anchorMin = Vector2.zero; hlRt.anchorMax = Vector2.one; hlRt.offsetMin = Vector2.zero; hlRt.offsetMax = Vector2.zero;

            slots[i] = new SlotUi { background = bg, icon = icon, countText = txt, highlight = hl };
        }

        // Create drag image used to follow the mouse during drag-and-drop
        var dragGo = new GameObject("HotbarDragImage", typeof(RectTransform));
        dragGo.transform.SetParent(transform, false);
        var dragImg = dragGo.AddComponent<Image>();
        dragImg.raycastTarget = false;
        dragImg.enabled = false;
        var dragRt = dragGo.GetComponent<RectTransform>();
        dragRt.sizeDelta = new Vector2(slotSize, slotSize);
        dragImage = dragImg; // store for runtime use
    }

    Image dragImage;

    void Update()
    {
        if (player == null || player.inventory == null)
            return;

        var inv = player.inventory;

        // Update drag-image position if dragging
        if (DragAndDropManager.IsDragging && dragImage != null)
        {
            Vector2 mp = Input.mousePosition;
            var rt = dragImage.rectTransform;
            rt.position = mp;
            dragImage.enabled = true;
            if (DragAndDropManager.DragTexture != null)
            {
                // create sprite from texture for display
                dragImage.sprite = Sprite.Create(DragAndDropManager.DragTexture, new Rect(0,0,DragAndDropManager.DragTexture.width, DragAndDropManager.DragTexture.height), new Vector2(0.5f,0.5f));
                dragImage.color = DragAndDropManager.DragColor;
            }
            else
            {
                dragImage.sprite = null; // will show color-only box if no sprite
                dragImage.color = DragAndDropManager.DragColor;
            }
        }
        else if (dragImage != null)
        {
            dragImage.enabled = false;
        }

        for (int i = 0; i < slotCount; i++)
        {
            var s = slots[i];
            if (s == null)
                continue;

            var slot = inv.GetSlot(i);

            if (slot != null && slot.item != null && slot.count > 0)
            {
                // If slot represents a block, try to show its texture-based sprite when available
                if (slot.item.id.StartsWith("block_"))
                {
                    string rest = slot.item.id.Substring("block_".Length);
                    Sprite texSprite = null;
                    // Map known block types to texture names: Grass -> Grass_Side, Dirt -> Dirt, Stone -> Stone
                    switch (rest)
                    {
                        case "Grass":
                            spriteCache.TryGetValue("Grass_Side", out texSprite);
                            break;
                        case "Dirt":
                            spriteCache.TryGetValue("Dirt", out texSprite);
                            break;
                        case "Stone":
                            spriteCache.TryGetValue("Stone", out texSprite);
                            break;
                        default:
                            spriteCache.TryGetValue(rest, out texSprite);
                            break;
                    }

                    if (texSprite != null)
                    {
                        s.icon.sprite = texSprite;
                        s.icon.color = Color.white;
                    }
                    else
                    {
                        // fallback: colored square based on id hash
                        s.icon.sprite = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
                        s.icon.color = IdToColor(slot.item.id);
                    }
                    // Ensure the slot's icon is raycastable for drag handlers
                    s.icon.raycastTarget = true;

                    s.countText.text = slot.count > 1 ? slot.count.ToString() : string.Empty;
                }
                else
                {
                    // Non-block items: fallback to colored square
                    s.icon.sprite = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
                    s.icon.color = IdToColor(slot.item.id);
                    s.countText.text = slot.count > 1 ? slot.count.ToString() : string.Empty;
                }
            }
            else
            {
                if (s.icon != null)
                    s.icon.color = new Color(1f, 1f, 1f, 0f);
                if (s.icon != null)
                    s.icon.sprite = null;
                if (s.countText != null)
                    s.countText.text = string.Empty;
            }

            // highlight selected
            if (inv.SelectedHotbarIndex == i)
            {
                if (s.highlight != null)
                    s.highlight.color = new Color(1f, 1f, 0f, 0.25f);
            }
            else
            {
                if (s.highlight != null)
                    s.highlight.color = new Color(1f, 1f, 0f, 0f);
            }
        }
    }

    // Called by UIDragHandler when user starts dragging a hotbar slot
    public void BeginDragFromSlot(int slotIndex)
    {
        if (player == null || player.inventory == null) return;
        var s = player.inventory.GetSlot(slotIndex);
        if (s == null) return;

        // prepare a texture for the drag image if available
        Texture2D tex = null;
        Color col = Color.white;
        if (slots[slotIndex] != null && slots[slotIndex].icon != null && slots[slotIndex].icon.sprite != null)
        {
            tex = slots[slotIndex].icon.sprite.texture;
            col = slots[slotIndex].icon.color;
        }

        DragAndDropManager.StartDrag(player.inventory, slotIndex, true, tex, col);
    }

    // Update drag visual position (screen space)
    public void UpdateDrag(Vector2 screenPos)
    {
        if (dragImage == null) return;
        var rt = dragImage.rectTransform;
        rt.position = screenPos;
    }

    // End drag: try drop onto hotbar slot under mouse. If not dropped on hotbar, leave DragAndDropManager.IsDragging true
    // so IMGUI inventory code can accept the drop when open.
    public void EndDrag(Vector2 screenPos)
    {
        if (!DragAndDropManager.IsDragging || player == null || player.inventory == null) return;

        // Try find a hotbar slot under the pointer
        for (int i = 0; i < slotCount; i++)
        {
            var slotRt = slots[i].icon.rectTransform;
            if (RectTransformUtility.RectangleContainsScreenPoint(slotRt, screenPos, null))
            {
                // drop to this hotbar slot (inventory index is i)
                DragAndDropManager.DropToSlot(player.inventory, i);
                return;
            }
        }

        // Not dropped on a hotbar slot: leave drag active so IMGUI inventory can pick it up on mouse up over inventory.
    }

    Color IdToColor(string id)
    {
        if (string.IsNullOrEmpty(id)) return Color.gray;
        int hash = id.GetHashCode();
        float r = ((hash >> 16) & 0xFF) / 255f;
        float g = ((hash >> 8) & 0xFF) / 255f;
        float b = (hash & 0xFF) / 255f;        return new Color(r, g, b, 1f);
    }


    void LoadSpritesFromBlockDatabase()
    {
        try
        {
            BlockDatabase db = null;
            if (player != null) db = player.GetBlockDatabase();
            if (db == null) db = FindAnyObjectByType<BlockDatabase>();
            if (db == null) return;
            var map = new System.Collections.Generic.Dictionary<BlockType, string> {
                { BlockType.Grass, "Grass_Side" },
                { BlockType.Dirt, "Dirt" },
                { BlockType.Stone, "Stone" }
            };
            foreach (var kv in map)
            {
                Material mat = WorldUtils.FindBlockMaterial(db, kv.Key, null);
                if (mat != null && mat.mainTexture is Texture2D tex)
                {
                    var sprite = Sprite.Create((Texture2D)tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    spriteCache[kv.Value] = sprite;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HotbarUI] Failed to load sprites from BlockDatabase: {ex}");
        }
    }
}
