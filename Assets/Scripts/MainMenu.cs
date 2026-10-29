using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Bandera que sobrevive al cambio de escena: le dice al GameManager
// que al arrancar el juego cargue la partida guardada.
public static class GameBoot
{
    public static bool loadSaveOnStart;
}

// Portada del juego. Va en un objeto vacio de la escena MainMenu.
// Construye toda la interfaz por codigo y detecta el raton a mano,
// asi no necesita EventSystem ni botones de Unity UI.
public class MainMenu : MonoBehaviour
{
    [Header("Portada")]
    [Tooltip("La imagen de portada. Importala como Sprite (2D and UI).")]
    public Sprite cover;

    [Tooltip("Nombre exacto de la escena del juego. Tiene que estar en Build Settings.")]
    public string gameSceneName = "SampleScene";

    [Header("Aspecto")]
    public int optionSize = 44;
    public float optionSpacing = 62f;
    public float bottomMargin = 90f;
    public Color normalColor = new Color(1f, 1f, 1f, 0.92f);
    public Color hoverColor = new Color(1f, 0.78f, 0.2f);
    public Color disabledColor = new Color(1f, 1f, 1f, 0.3f);

    private class Option
    {
        public string label;
        public bool enabled;
        public System.Action action;
        public Text text;
        public RectTransform rect;
    }

    private readonly System.Collections.Generic.List<Option> options =
        new System.Collections.Generic.List<Option>();

    private Canvas canvas;
    private GameObject controlsPanel;
    private Text hint;
    private int selected;
    private bool hasSave;

    private void Awake()
    {
        // La escena del juego captura el cursor: aqui hay que soltarlo.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        hasSave = SaveSystem.HasSave();

        Build();
    }

    // ------------------------------------------------------------ Montaje

    private void Build()
    {
        GameObject canvasGO = new GameObject("Menu_Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildCover(canvasGO.transform);
        BuildOptions(canvasGO.transform);
        BuildControlsPanel(canvasGO.transform);
    }

    private void BuildCover(Transform parent)
    {
        Image bg = NewImage("Portada", parent, cover, cover != null ? Color.white : new Color(0.1f, 0.08f, 0.07f));
        Stretch(bg.rectTransform);

        // EnvelopeParent: la imagen cubre siempre la pantalla recortando,
        // en vez de deformarse o dejar bandas negras.
        if (cover != null)
        {
            bg.preserveAspect = false;
            AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = cover.rect.width / Mathf.Max(1f, cover.rect.height);
        }

        // Degradado oscuro en la parte baja para que el texto se lea.
        Image fade = NewImage("Degradado", parent, MakeVerticalFade(), Color.white);
        fade.rectTransform.anchorMin = new Vector2(0f, 0f);
        fade.rectTransform.anchorMax = new Vector2(1f, 0.45f);
        fade.rectTransform.offsetMin = Vector2.zero;
        fade.rectTransform.offsetMax = Vector2.zero;
    }

    private void BuildOptions(Transform parent)
    {
        AddOption("JUGAR", true, StartNewGame);
        AddOption("CARGAR PARTIDA", hasSave, LoadSavedGame);
        AddOption("CONTROLES", true, ToggleControls);
        AddOption("SALIR", true, QuitGame);

        for (int i = 0; i < options.Count; i++)
        {
            Option o = options[i];

            o.text = NewText("Opcion_" + o.label, parent, o.label, optionSize,
                             o.enabled ? normalColor : disabledColor);

            o.rect = o.text.rectTransform;
            o.rect.anchorMin = o.rect.anchorMax = new Vector2(0.5f, 0f);
            o.rect.pivot = new Vector2(0.5f, 0.5f);
            o.rect.sizeDelta = new Vector2(620f, optionSpacing - 8f);
            o.rect.anchoredPosition =
                new Vector2(0f, bottomMargin + (options.Count - 1 - i) * optionSpacing);
        }

        selected = 0;

        hint = NewText("Pista", parent,
                       hasSave ? "Raton o flechas + Enter" : "Raton o flechas + Enter    (no hay partida guardada)",
                       24, new Color(1f, 1f, 1f, 0.55f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hint.rectTransform.sizeDelta = new Vector2(900f, 40f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, 42f);
    }

    private void AddOption(string label, bool enabled, System.Action action)
    {
        options.Add(new Option { label = label, enabled = enabled, action = action });
    }

    private void BuildControlsPanel(Transform parent)
    {
        Image panel = NewImage("Controles", parent, null, new Color(0f, 0f, 0f, 0.88f));
        Stretch(panel.rectTransform);
        controlsPanel = panel.gameObject;

        Text title = NewText("Titulo", panel.transform, "CONTROLES", 72, new Color(1f, 0.78f, 0.2f));
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.rectTransform.sizeDelta = new Vector2(1200f, 120f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 250f);

        string body =
            "WASD / Flechas     Moverse\n" +
            "Shift              Correr\n" +
            "Raton              Apuntar\n" +
            "Clic izquierdo     Disparar ketchup\n" +
            "V                  Cambiar 1a / 3a persona\n\n" +
            "K                  Guardar partida\n" +
            "L                  Cargar partida\n" +
            "Esc / P            Pausa\n" +
            "R                  Reiniciar";

        Text text = NewText("Lista", panel.transform, body, 34, Color.white);
        text.alignment = TextAnchor.UpperCenter;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = new Vector2(1100f, 450f);
        text.rectTransform.anchoredPosition = new Vector2(0f, -40f);

        Text back = NewText("Volver", panel.transform, "Clic o Esc para volver", 28,
                            new Color(1f, 1f, 1f, 0.6f));
        back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        back.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        back.rectTransform.sizeDelta = new Vector2(900f, 50f);
        back.rectTransform.anchoredPosition = new Vector2(0f, 70f);

        controlsPanel.SetActive(false);
    }

    // ------------------------------------------------------------- Entrada

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (controlsPanel != null && controlsPanel.activeSelf)
        {
            bool close = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                      || (mouse != null && mouse.leftButton.wasPressedThisFrame);

            if (close) controlsPanel.SetActive(false);
            return;
        }

        HandleKeyboard(kb);
        HandleMouse(mouse);
        Repaint();
    }

    private void HandleKeyboard(Keyboard kb)
    {
        if (kb == null) return;

        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            Activate(selected);
    }

    // Salta las opciones deshabilitadas.
    private void Move(int step)
    {
        for (int i = 0; i < options.Count; i++)
        {
            selected = (selected + step + options.Count) % options.Count;
            if (options[selected].enabled) return;
        }
    }

    private void HandleMouse(Mouse mouse)
    {
        if (mouse == null) return;

        Vector2 position = mouse.position.ReadValue();

        for (int i = 0; i < options.Count; i++)
        {
            if (!options[i].enabled) continue;

            // Camara null porque el Canvas es ScreenSpaceOverlay.
            if (!RectTransformUtility.RectangleContainsScreenPoint(options[i].rect, position, null))
                continue;

            selected = i;
            if (mouse.leftButton.wasPressedThisFrame) Activate(i);
            return;
        }
    }

    private void Repaint()
    {
        for (int i = 0; i < options.Count; i++)
        {
            Option o = options[i];
            if (o.text == null) continue;

            o.text.color = !o.enabled ? disabledColor
                         : (i == selected ? hoverColor : normalColor);

            o.text.fontSize = i == selected && o.enabled
                ? Mathf.RoundToInt(optionSize * 1.12f)
                : optionSize;
        }
    }

    private void Activate(int index)
    {
        if (index < 0 || index >= options.Count) return;
        if (!options[index].enabled) return;

        options[index].action?.Invoke();
    }

    // ------------------------------------------------------------ Acciones

    private void StartNewGame()
    {
        GameBoot.loadSaveOnStart = false;
        SceneManager.LoadScene(gameSceneName);
    }

    private void LoadSavedGame()
    {
        GameBoot.loadSaveOnStart = true;
        SceneManager.LoadScene(gameSceneName);
    }

    private void ToggleControls()
    {
        if (controlsPanel != null) controlsPanel.SetActive(true);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------ Helpers

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Text NewText(string name, Transform parent, string content, int size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = content;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return f;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Degradado vertical: transparente arriba, oscuro abajo.
    private static Sprite MakeVerticalFade()
    {
        const int h = 128;
        Texture2D tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        {
            float t = 1f - (y / (float)(h - 1));      // 0 arriba, 1 abajo
            float a = Mathf.SmoothStep(0f, 0.88f, t);
            tex.SetPixel(0, y, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
