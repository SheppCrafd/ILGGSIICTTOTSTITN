using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Runtime-built inventory UI with drag & drop, split (half/right-click) and pick-1 (shift-click).
public class InventoryUI : MonoBehaviour
{
    public Player Player { get; private set; }
    Canvas canvas;
    GameObject panel;
    GameObject dragIcon;
    Text dragCountText;

    ItemStack dragStack = null;
    bool visible = false;

    public InventoryUI() { }

    void Start()
    {
        Player = FindAnyObjectByType<Player>();
        BuildUI();
        Hide();
    }

    void BuildUI()
    {
        // Ensure EventSystem exists
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        canvas = new GameObject("InventoryCanvas", typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.gameObject.AddComponent<CanvasScaler>();
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("InventoryPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0.12f,0.12f,0.12f,0.95f);
        var rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(720, 320);
        rt.anchorMin = new Vector2(0.5f,0.5f); rt.anchorMax = new Vector2(0.5f,0.5f); rt.anchoredPosition = Vector2.zero;

        var inv = Player != null ? Player.inventory : new Inventory();
        int columns = 9;
        int rows = Mathf.CeilToInt((float)inv.SlotCount / columns);

        float slotSize = 64f;
        float padding = 8f;

        GameObject grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(panel.transform, false);
        var grt = grid.GetComponent<RectTransform>();
        grt.sizeDelta = new Vector2(columns * (slotSize + padding), rows * (slotSize + padding));
        grt.anchoredPosition = Vector2.zero;

        for (int i = 0; i < inv.SlotCount; i++)
        {
            int col = i % columns;
            int row = i / columns;
            float x = (col - (columns-1)/2f) * (slotSize + padding);
            float y = ((rows-1)/2f - row) * (slotSize + padding);

            var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
            slotGo.transform.SetParent(grid.transform, false);
            var srt = slotGo.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(slotSize, slotSize);
            srt.anchoredPosition = new Vector2(x, y);

            var bg = slotGo.AddComponent<Image>();
            bg.color = new Color(0.2f,0.2f,0.2f,1f);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slotGo.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.rectTransform.anchorMin = new Vector2(0.1f,0.1f); icon.rectTransform.anchorMax = new Vector2(0.9f,0.9f);
            icon.color = new Color(1,1,1,0);

            var countGo = new GameObject("Count", typeof(RectTransform));
            countGo.transform.SetParent(slotGo.transform, false);
            var txt = countGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 16; txt.alignment = TextAnchor.LowerRight; txt.color = Color.white;
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one; txt.rectTransform.offsetMin = new Vector2(6,6); txt.rectTransform.offsetMax = new Vector2(-6,-6);

            var slotComp = slotGo.AddComponent<InventorySlotUI>();
            slotComp.slotIndex = i;
            slotComp.parentUI = this;
        }

        // Drag icon
        dragIcon = new GameObject("DragIcon", typeof(RectTransform));
        dragIcon.transform.SetParent(canvas.transform, false);
        var dimg = dragIcon.AddComponent<Image>();
        dimg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), Vector2.zero);
        dimg.color = new Color(1,1,1,0);
        var drt = dragIcon.GetComponent<RectTransform>(); drt.sizeDelta = new Vector2(48,48);

        var dcountGo = new GameObject("DragCount", typeof(RectTransform)); dcountGo.transform.SetParent(dragIcon.transform, false);
        dragCountText = dcountGo.AddComponent<Text>(); dragCountText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); dragCountText.fontSize = 14; dragCountText.alignment = TextAnchor.LowerRight; dragCountText.color = Color.white;
        dragCountText.rectTransform.anchorMin = Vector2.zero; dragCountText.rectTransform.anchorMax = Vector2.one; dragCountText.rectTransform.offsetMin = new Vector2(4,4); dragCountText.rectTransform.offsetMax = new Vector2(-4,-4);

        // Keybinds top-left
        var kbGo = new GameObject("Keybinds", typeof(RectTransform)); kbGo.transform.SetParent(canvas.transform, false);
        var kbTxt = kbGo.AddComponent<Text>(); kbTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); kbTxt.fontSize = 14; kbTxt.alignment = TextAnchor.UpperLeft; kbTxt.color = Color.white;
        kbTxt.rectTransform.anchorMin = new Vector2(0f,1f); kbTxt.rectTransform.anchorMax = new Vector2(0f,1f); kbTxt.rectTransform.pivot = new Vector2(0f,1f); kbTxt.rectTransform.anchoredPosition = new Vector2(8,-8);
        kbTxt.text = "Keys:\nWASD - Move\nSpace - Jump\nI - Inventory\nEsc - Pause\nLMB - Break\nRMB - Place";

        // Update visuals initially
        RefreshAll();
    }

    public Color IdToColor(string id)
    {
        if (string.IsNullOrEmpty(id)) return Color.gray;
        int hash = id.GetHashCode();
        float r = ((hash >> 16) & 0xFF) / 255f;
        float g = ((hash >> 8) & 0xFF) / 255f;
        float b = (hash & 0xFF) / 255f;
        return new Color(r,g,b,1f);
    }

    void Update()
    {
        if (!visible) return;

        // drag icon follow mouse
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, Input.mousePosition, null, out pos);
        dragIcon.GetComponent<RectTransform>().anchoredPosition = pos;

        if (dragStack != null)
        {
            var img = dragIcon.GetComponent<Image>();
            img.color = new Color(1,1,1,1);
            dragCountText.text = dragStack.count > 1 ? dragStack.count.ToString() : string.Empty;
            img.color = IdToColor(dragStack.item.id);
        }
        else
        {
            var img = dragIcon.GetComponent<Image>();
            img.color = new Color(1,1,1,0);
            dragCountText.text = string.Empty;
        }
    }

    public void Toggle()
    {
        if (visible) Hide(); else Show();
    }

    public void Show()
    {
        if (visible) return;
        visible = true;
        if (canvas != null) canvas.gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Hide()
    {
        if (!visible) return;
        visible = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
        Time.timeScale = 1f;

        // If holding a dragStack when closing, try to place it back into inventory
        if (dragStack != null && Player != null)
        {
            Player.inventory.AddItem(dragStack.item, dragStack.count);
            dragStack = null;
            RefreshAll();
        }
    }

    public void RefreshAll()
    {
        var slots = panel.GetComponentsInChildren<InventorySlotUI>(true);
        foreach (var s in slots) s.Refresh();
    }

    // Slot interactions delegated from InventorySlotUI
    public void OnSlotPointerDown(InventorySlotUI slotUI, PointerEventData eventData)
    {
        var inv = Player.inventory;
        int idx = slotUI.slotIndex;
        var slot = inv.GetSlot(idx);

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // If not dragging: pick up
        if (dragStack == null)
        {
            if (slot == null) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (shift)
                {
                    // pick 1
                    dragStack = new ItemStack(slot.item, 1);
                    slot.count -= 1;
                    if (slot.count <= 0) inv.slots[idx] = null;
                }
                else
                {
                    // pick full
                    dragStack = slot;
                    inv.slots[idx] = null;
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                // pick half (rounded up)
                int take = (slot.count + 1) / 2;
                dragStack = new ItemStack(slot.item, take);
                slot.count -= take;
                if (slot.count <= 0) inv.slots[idx] = null;
            }

            RefreshAll();
            return;
        }

        // If dragging and clicked a slot: place/swap
        if (dragStack != null)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // try merge
                if (slot == null)
                {
                    inv.slots[idx] = dragStack;
                    dragStack = null;
                }
                else if (slot.item.id == dragStack.item.id)
                {
                    int space = slot.item.maxStack - slot.count;
                    int move = Math.Min(space, dragStack.count);
                    slot.count += move;
                    dragStack.count -= move;
                    if (dragStack.count <= 0) dragStack = null;
                }
                else
                {
                    // swap
                    inv.slots[idx] = dragStack;
                    dragStack = slot;
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                // place single item into slot
                if (slot == null)
                {
                    inv.slots[idx] = new ItemStack(dragStack.item, 1);
                    dragStack.count -= 1;
                    if (dragStack.count <= 0) dragStack = null;
                }
                else if (slot.item.id == dragStack.item.id && slot.count < slot.item.maxStack)
                {
                    slot.count += 1;
                    dragStack.count -= 1;
                    if (dragStack.count <= 0) dragStack = null;
                }
            }

            RefreshAll();
            return;
        }
    }

    public void OnBeginDrag(InventorySlotUI slotUI, PointerEventData eventData)
    {
        // begin drag same as pointer down (will pick up)
        OnSlotPointerDown(slotUI, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // drag handled by Update dragging image
    }

    public void OnEndDrag(InventorySlotUI slotUI, PointerEventData eventData)
    {
        // If ended drag and not over a slot, return item to inventory
        if (dragStack != null && eventData.pointerEnter == null)
        {
            Player.inventory.AddItem(dragStack.item, dragStack.count);
            dragStack = null;
            RefreshAll();
        }
    }

    public void OnDrop(InventorySlotUI slotUI, PointerEventData eventData)
    {
        // delegate to pointer down logic
        OnSlotPointerDown(slotUI, eventData);
    }
}
