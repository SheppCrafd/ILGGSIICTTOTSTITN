using UnityEngine;
using UnityEngine.UI;

public class KeybindsUI : MonoBehaviour
{
    Canvas canvas;
    Text kbText;

    void Awake()
    {
        canvas = new GameObject("KeybindsCanvas", typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.gameObject.AddComponent<CanvasScaler>();
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        var go = new GameObject("KeybindsText", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        kbText = go.AddComponent<Text>();
        kbText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        kbText.fontSize = 14;
        kbText.alignment = TextAnchor.UpperLeft;
        kbText.color = Color.white;
        kbText.rectTransform.anchorMin = new Vector2(0f,1f);
        kbText.rectTransform.anchorMax = new Vector2(0f,1f);
        kbText.rectTransform.pivot = new Vector2(0f,1f);
        kbText.rectTransform.anchoredPosition = new Vector2(8,-8);

        kbText.text = "Keys:\nWASD - Move\nSpace - Jump\nI - Inventory\nEsc - Pause\nLMB - Break\nRMB - Place";
    }
}