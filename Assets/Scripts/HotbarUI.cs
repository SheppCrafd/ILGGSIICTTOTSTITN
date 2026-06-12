using UnityEngine;
using UnityEngine.UI;

// Runtime-built hotbar UI. Reads the player's Inventory (InventorySystem.Inventory) and shows
// the first 9 slots. Highlights Inventory.SelectedHotbarIndex. Uses placeholder colored icons.
[RequireComponent(typeof(Canvas))]
public class HotbarUI : MonoBehaviour
{
    public Player player;

    Canvas canvas;
    RectTransform hotbarRoot;

    const int slotCount = 9;
    const int slotSize = 64;
    const int padding = 6;

    class SlotUi
    {
        public Image background;
        public Image icon;
        public Text countText;
        public Image highlight;
    }

    SlotUi[] slots = new SlotUi[slotCount];

    void Awake()
    {
        // Try to find player if not assigned
        if (player == null)
            player = FindAnyObjectByType<Player>();

        // Build Canvas if not present
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var raycaster = gameObject.GetComponent<GraphicRaycaster>() ?? gameObject.AddComponent<GraphicRaycaster>();

        // Create hotbar root
        var go = new GameObject("HotbarRoot", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        hotbarRoot = go.GetComponent<RectTransform>();
        hotbarRoot.anchorMin = new Vector2(0.5f, 0f);
        hotbarRoot.anchorMax = new Vector2(0.5f, 0f);
        hotbarRoot.pivot = new Vector2(0.5f, 0f);
        hotbarRoot.anchoredPosition = new Vector2(0, 20);
        hotbarRoot.sizeDelta = new Vector2(slotCount * (slotSize + padding), slotSize + padding * 2);

        // Build slots
        for (int i = 0; i < slotCount; i++)
        {
            var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
            slotGo.transform.SetParent(hotbarRoot, false);
            var rt = slotGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(slotSize, slotSize);
            float x = (i - (slotCount - 1) / 2.0f) * (slotSize + padding);
            rt.anchoredPosition = new Vector2(x, 0);

            // background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(slotGo.transform, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
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
    }

    void Update()
    {
        if (player == null || player.inventory == null)
            return;

        var inv = player.inventory;

        for (int i = 0; i < slotCount; i++)
        {
            var s = slots[i];
            if (s == null)
                continue;

            var slot = inv.GetSlot(i);

            // icon: placeholder colored square based on item id
            if (slot != null && slot.item != null && slot.count > 0)
            {
                s.icon.color = Color.white;
                // tint based on id hash
                s.icon.sprite = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
                s.icon.color = IdToColor(slot.item.id);
                s.countText.text = slot.count > 1 ? slot.count.ToString() : string.Empty;
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

    Color IdToColor(string id)
    {
        if (string.IsNullOrEmpty(id)) return Color.gray;
        int hash = id.GetHashCode();
        float r = ((hash >> 16) & 0xFF) / 255f;
        float g = ((hash >> 8) & 0xFF) / 255f;
        float b = (hash & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }
}