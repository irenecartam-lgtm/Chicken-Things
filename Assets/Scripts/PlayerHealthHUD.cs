using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// HUD de vida estilo Fortnite: foto del personaje dentro de un circulo,
// rodeado por un anillo que se vacia al recibir daño. Se crea entero por codigo.
public class PlayerHealthHUD : MonoBehaviour
{
    [Header("Aspecto")]
    public float diameter = 160f;
    public Vector2 margin = new Vector2(90f, 90f);
    [Range(0.05f, 0.3f)] public float ringThickness = 0.12f;
    public Color fullColor = new Color(0.25f, 0.9f, 0.35f);
    public Color midColor = new Color(1f, 0.85f, 0.2f);
    public Color lowColor = new Color(0.95f, 0.15f, 0.15f);
    public Color trailColor = new Color(1f, 1f, 1f, 0.85f);
    public Color portraitFallbackColor = new Color(0.95f, 0.7f, 0.25f);

    [Header("Animacion")]
    public float fillSpeed = 10f;
    public float trailDelay = 0.35f;
    public float trailSpeed = 0.6f;

    private Image ring, trail, damageFlash;
    private Text lifeLabel;
    private GameObject gameOverPanel;

    private float targetRatio = 1f, shownRatio = 1f, trailRatio = 1f;
    private float lastHitTime = -99f;
    private bool gameOver;

    // ---------------------------------------------------------------- API
    public void Build(Sprite portrait, int life, int maxLife)
    {
        GameObject canvasGO = new GameObject("HealthHUD_Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Destello rojo a pantalla completa al recibir daño.
        damageFlash = NewImage("DamageFlash", canvasGO.transform, null, new Color(1f, 0f, 0f, 0f));
        Stretch(damageFlash.rectTransform, 0f);

        // Contenedor del HUD (abajo a la izquierda).
        RectTransform root = (RectTransform)new GameObject("PlayerHealth", typeof(RectTransform)).transform;
        root.SetParent(canvasGO.transform, false);
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.sizeDelta = new Vector2(diameter, diameter);
        root.anchoredPosition = margin;

        Sprite disc = MakeRingSprite(0f);
        Sprite annulus = MakeRingSprite(1f - ringThickness);

        Image bg = NewImage("Fondo", root, disc, new Color(0f, 0f, 0f, 0.55f));
        Stretch(bg.rectTransform, 0f);

        Image track = NewImage("Pista", root, annulus, new Color(0.1f, 0.1f, 0.1f, 0.85f));
        Stretch(track.rectTransform, 0f);

        trail = NewImage("Estela", root, annulus, trailColor);
        Stretch(trail.rectTransform, 0f);
        MakeRadial(trail);

        ring = NewImage("Vida", root, annulus, fullColor);
        Stretch(ring.rectTransform, 0f);
        MakeRadial(ring);

        // Foto del personaje recortada en circulo.
        float inset = diameter * 0.5f * ringThickness + 5f;
        Image maskImg = NewImage("MascaraFoto", root, disc, Color.white);
        Stretch(maskImg.rectTransform, inset);
        Mask mask = maskImg.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        Image photo = NewImage("Foto", maskImg.transform, portrait, portrait != null ? Color.white : portraitFallbackColor);
        if (portrait != null)
        {
            photo.rectTransform.anchorMin = photo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            AspectRatioFitter fit = photo.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = portrait.rect.width / portrait.rect.height;
        }
        else
        {
            Stretch(photo.rectTransform, 0f);
        }

        // Numero de vida en una pastilla sobre el borde inferior del anillo.
        Image pill = NewImage("Pastilla", root, null, new Color(0f, 0f, 0f, 0.75f));
        pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        pill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        pill.rectTransform.sizeDelta = new Vector2(diameter * 0.55f, 30f);
        pill.rectTransform.anchoredPosition = Vector2.zero;
        lifeLabel = NewText("Numero", pill.transform, "", 22, Color.white);
        Stretch(lifeLabel.rectTransform, 0f);

        BuildGameOverPanel(canvasGO.transform);

        targetRatio = shownRatio = trailRatio = Ratio(life, maxLife);
        SetLife(life, maxLife, false);
        Apply();
    }

    public void SetLife(int life, int maxLife, bool tookDamage)
    {
        targetRatio = Ratio(life, maxLife);
        if (lifeLabel != null) lifeLabel.text = life + " / " + maxLife;

        if (tookDamage)
        {
            lastHitTime = Time.unscaledTime;
            if (damageFlash != null) damageFlash.color = new Color(1f, 0f, 0f, 0.35f);
        }
        else if (targetRatio > trailRatio)
        {
            trailRatio = targetRatio;
        }
    }

    public void ShowGameOver()
    {
        gameOver = true;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // ------------------------------------------------------------- Update
    private void Update()
    {
        if (ring == null) return;

        float dt = Time.unscaledDeltaTime;

        shownRatio = Mathf.Lerp(shownRatio, targetRatio, 1f - Mathf.Exp(-fillSpeed * dt));
        if (Mathf.Abs(shownRatio - targetRatio) < 0.001f) shownRatio = targetRatio;

        if (Time.unscaledTime - lastHitTime > trailDelay)
            trailRatio = Mathf.MoveTowards(trailRatio, targetRatio, trailSpeed * dt);

        Apply();

        if (damageFlash != null && damageFlash.color.a > 0f)
        {
            Color c = damageFlash.color;
            c.a = Mathf.MoveTowards(c.a, 0f, 1.2f * dt);
            damageFlash.color = c;
        }

        if (gameOver && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void Apply()
    {
        ring.fillAmount = shownRatio;
        ring.color = ColorFor(shownRatio);
        trail.fillAmount = trailRatio;
    }

    private Color ColorFor(float r)
    {
        return r > 0.5f
            ? Color.Lerp(midColor, fullColor, (r - 0.5f) * 2f)
            : Color.Lerp(lowColor, midColor, r * 2f);
    }

    private static float Ratio(int life, int max) => max > 0 ? Mathf.Clamp01((float)life / max) : 0f;

    // ------------------------------------------------------ Game Over UI
    private void BuildGameOverPanel(Transform parent)
    {
        Image panel = NewImage("GameOver", parent, null, new Color(0f, 0f, 0f, 0.65f));
        Stretch(panel.rectTransform, 0f);
        gameOverPanel = panel.gameObject;

        Text title = NewText("Titulo", panel.transform, "GAME OVER", 110, new Color(1f, 0.25f, 0.25f));
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.rectTransform.sizeDelta = new Vector2(1200f, 200f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 60f);

        Text hint = NewText("Reiniciar", panel.transform, "Pulsa R para reiniciar", 40, Color.white);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hint.rectTransform.sizeDelta = new Vector2(1000f, 80f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -70f);

        gameOverPanel.SetActive(false);
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
        return t;
    }

    private static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return f;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void MakeRadial(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.fillClockwise = true;
        img.fillAmount = 1f;
    }

    // Crea un sprite redondo (innerRatio = 0) o un anillo (innerRatio > 0) con borde suave.
    private static Sprite MakeRingSprite(float innerRatio)
    {
        const int res = 256;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float r = res * 0.5f;
        Color32[] px = new Color32[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float outerA = Mathf.Clamp01(r - d);
                float innerA = innerRatio > 0f ? Mathf.Clamp01(d - innerRatio * r) : 1f;
                byte a = (byte)(Mathf.Min(outerA, innerA) * 255f);
                px[y * res + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}