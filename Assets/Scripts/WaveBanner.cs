using UnityEngine;
using UnityEngine.UI;

// Cartel grande en el centro-alto de la pantalla para avisar de las oleadas.
// No hay que arrastrarlo a ningun objeto: se crea solo al llamar a WaveBanner.Show().
public class WaveBanner : MonoBehaviour
{
    public static WaveBanner Instance { get; private set; }

    [Header("Tiempos")]
    public float riseTime = 0.25f;
    public float holdTime = 1.8f;
    public float fadeTime = 0.7f;

    [Header("Aspecto")]
    public int titleSize = 90;
    public int subtitleSize = 36;
    public Color titleColor = new Color(1f, 0.78f, 0.2f);
    public Color subtitleColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("Altura en pantalla: 0 = abajo, 1 = arriba.")]
    public float verticalPosition = 0.7f;

    private CanvasGroup group;
    private RectTransform root;
    private Text title;
    private Text subtitle;
    private float timer = -1f;

    public static void Show(string titleText, string subtitleText = "")
    {
        if (Instance == null)
            Instance = new GameObject("WaveBanner").AddComponent<WaveBanner>();

        Instance.Display(titleText, subtitleText);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
    }

    private void Build()
    {
        GameObject canvasGO = new GameObject("WaveBanner_Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;   // por debajo del HUD (100), para que pausa y game over lo tapen

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootGO = new GameObject("Contenido", typeof(RectTransform), typeof(CanvasGroup));
        rootGO.transform.SetParent(canvasGO.transform, false);

        root = (RectTransform)rootGO.transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, verticalPosition);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(1400f, 220f);
        root.anchoredPosition = Vector2.zero;

        group = rootGO.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        title = NewText("Titulo", root, "", titleSize, titleColor);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.rectTransform.sizeDelta = new Vector2(1400f, 120f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 30f);

        subtitle = NewText("Subtitulo", root, "", subtitleSize, subtitleColor);
        subtitle.rectTransform.anchorMin = subtitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        subtitle.rectTransform.sizeDelta = new Vector2(1400f, 60f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, -55f);
    }

    private void Display(string titleText, string subtitleText)
    {
        if (title == null) return;

        title.text = titleText;
        subtitle.text = subtitleText;
        timer = 0f;
    }

    private void Update()
    {
        if (timer < 0f || group == null) return;

        // unscaledDeltaTime: el cartel sigue animandose aunque el juego este en pausa.
        timer += Time.unscaledDeltaTime;

        float alpha;
        float scale;

        if (timer < riseTime)
        {
            float t = timer / riseTime;
            alpha = t;
            scale = Mathf.Lerp(1.3f, 1f, t * t);        // entra de golpe y se asienta
        }
        else if (timer < riseTime + holdTime)
        {
            alpha = 1f;
            scale = 1f;
        }
        else if (timer < riseTime + holdTime + fadeTime)
        {
            float t = (timer - riseTime - holdTime) / fadeTime;
            alpha = 1f - t;
            scale = Mathf.Lerp(1f, 1.08f, t);           // se va creciendo un poco
        }
        else
        {
            group.alpha = 0f;
            timer = -1f;
            return;
        }

        group.alpha = alpha;
        root.localScale = Vector3.one * scale;
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

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(3f, -3f);
        return t;
    }

    private static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return f;
    }
}