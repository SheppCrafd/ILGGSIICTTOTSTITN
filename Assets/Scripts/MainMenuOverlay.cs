using UnityEngine;
using UnityEngine.UI;

// Simple main menu overlay created at runtime on scene load.
public class MainMenuOverlay : MonoBehaviour
{
    Canvas canvas;
    GameObject panel;

    void Start()
    {
        BuildMenu();
        // Pause the game while the menu is visible
        Time.timeScale = 0f;
    }

    void BuildMenu()
    {
        // Ensure EventSystem exists
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        canvas = new GameObject("MainMenuCanvas", typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.gameObject.AddComponent<CanvasScaler>();
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("MainMenuPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panel.transform, false);
        var title = titleGO.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 48; title.alignment = TextAnchor.UpperCenter; title.color = Color.white;
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 0.8f); trt.anchorMax = new Vector2(0.5f, 0.8f); trt.anchoredPosition = Vector2.zero; trt.sizeDelta = new Vector2(600, 80);
        title.text = "Not M C";

        // Buttons container
        var btnContainer = new GameObject("Buttons", typeof(RectTransform));
        btnContainer.transform.SetParent(panel.transform, false);
        var brt = btnContainer.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f); brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(400, 200);

        // New World button
        CreateButton(btnContainer.transform, "NewWorldButton", new Vector2(0, 40), "New World", OnNewWorldClicked);
        // Load World button
        CreateButton(btnContainer.transform, "LoadWorldButton", new Vector2(0, -40), "Load World", OnLoadWorldClicked);
    }

    Button CreateButton(Transform parent, string name, Vector2 anchored, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 60);
        rt.anchoredPosition = anchored;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 24; txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white;
        txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one; txt.rectTransform.offsetMin = Vector2.zero; txt.rectTransform.offsetMax = Vector2.zero;
        txt.text = label;

        if (onClick != null)
            btn.onClick.AddListener(onClick);

        return btn;
    }

    void OnNewWorldClicked()
    {
        Debug.Log("[MainMenu] New World clicked (no-op)");
        // No-op per instruction
    }

    void OnLoadWorldClicked()
    {
        Debug.Log("[MainMenu] Load World clicked (no-op)");
        // No-op per instruction
    }

    // Create overlay on scene load
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExists()
    {
        var existing = FindObjectOfType<MainMenuOverlay>();
        if (existing == null)
        {
            var go = new GameObject("MainMenuOverlay");
            go.AddComponent<MainMenuOverlay>();
        }
    }
}