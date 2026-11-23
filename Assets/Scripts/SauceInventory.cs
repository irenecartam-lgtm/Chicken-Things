using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Cuenta las bolitas que recoge el jugador y gestiona las salsas (superpoderes).
// Primera salsa: BUFFALO. Con "buffaloCost" bolitas se activa con la tecla 1 y durante
// "buffaloDuration" segundos cada disparo que acierte prende fuego al enemigo.
// Se crea solo al dar a Play en la escena del juego (no hay que ponerlo en ningun objeto).
public class SauceInventory : MonoBehaviour
{
    public static SauceInventory Instance { get; private set; }

    // La lee Bullet: mientras sea true, las balas queman.
    public static bool BuffaloActive { get; private set; }

    [Header("Recoger bolitas")]
    [Tooltip("A esta distancia del jugador la bolita se recoge.")]
    public float collectRadius = 1f;
    [Tooltip("Dentro de este radio las bolitas vuelan hacia el jugador.")]
    public float magnetRadius = 4f;
    public float magnetMinSpeed = 4f;
    public float magnetMaxSpeed = 16f;
    [Tooltip("Segundos tras morir el enemigo hasta poder coger sus bolitas.")]
    public float pickupDelay = 0.6f;
    [Tooltip("Altura sobre los pies del jugador a la que vuelan las bolitas al ser recogidas.")]
    public float pickupHeight = 0.6f;

    [Header("Salsa Buffalo")]
    public Key buffaloKey = Key.Digit1;
    [Tooltip("Bolitas que cuesta activar la salsa.")]
    public int buffaloCost = 30;
    [Tooltip("Segundos que dura el poder.")]
    public float buffaloDuration = 30f;
    [Tooltip("Segundos que arde un enemigo despues de un disparo.")]
    public float burnDuration = 5f;
    [Tooltip("Dano que hace el fuego en cada golpe de quemadura.")]
    public int burnDamagePerTick = 1;
    [Tooltip("Segundos entre un golpe de quemadura y el siguiente.")]
    public float burnTickInterval = 0.5f;

    [Header("Aspecto del HUD")]
    public Color idleColor = new Color(1f, 1f, 1f, 0.9f);
    public Color barColor = new Color(1f, 0.55f, 0.1f);
    public Color readyColor = new Color(1f, 0.85f, 0.2f);
    public Color activeColor = new Color(1f, 0.3f, 0.1f);

    [Header("Estado (solo lectura)")]
    public int bolitas;

    public Transform PlayerTransform => player;

    private Transform player;
    private float remaining;

    // HUD
    private Text statusText, infoText;
    private Image fill;
    private enum State { Idle, Ready, Active }
    private State lastState = (State)(-1);
    private int lastBolitas = -1;
    private int lastSecs = -1;
    private bool lastShowingMessage;
    private bool messageDirty;
    private string message = "";
    private float messageUntil;

    // AfterSceneLoad solo se dispara con la PRIMERA escena (el menu). Por eso ademas
    // se escucha sceneLoaded: asi tambien se crea al pasar del menu a la escena del juego.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoCreate();   // la escena que ya esta cargada
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AutoCreate();

    private static void AutoCreate()
    {
        if (FindFirstObjectByType<SauceInventory>() != null) return;

        // Escena de juego = la que tiene GameManager o WaveSpawner (el menu no tiene ninguno).
        if (FindFirstObjectByType<GameManager>() == null &&
            FindFirstObjectByType<WaveSpawner>() == null) return;

        new GameObject("SauceInventory").AddComponent<SauceInventory>();
    }

    // La llama Bullet al acertar a un enemigo. Solo hace algo si la salsa Buffalo esta activa.
    public static void TryIgnite(EnemyHealth target)
    {
        if (!BuffaloActive || Instance == null || target == null) return;
        if (target.CurrentHealth <= 0) return;   // ya ha muerto del disparo: no hay nada que quemar

        BurningEffect.Apply(target, Instance.burnDuration, Instance.burnDamagePerTick, Instance.burnTickInterval);
    }

    // La llama DebrisPickup al recoger una bolita.
    public void Collect(int amount)
    {
        bolitas += amount;
    }

    // ------------------------------------------------------------- Ciclo
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuffaloActive = false;

        BuildHud();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            BuffaloActive = false;
        }
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // En pausa y en Game Over el tiempo esta a 0: ni corre la salsa ni se lee la tecla.
        if (Time.timeScale > 0f)
        {
            if (BuffaloActive)
            {
                remaining -= Time.deltaTime;
                if (remaining <= 0f) EndBuffalo();
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb[buffaloKey].wasPressedThisFrame) TryActivateBuffalo();
        }

        UpdateHud();
    }

    private void TryActivateBuffalo()
    {
        if (BuffaloActive)
        {
            ShowMessage("La salsa Buffalo ya esta activa");
            return;
        }

        if (bolitas < buffaloCost)
        {
            ShowMessage("Te faltan " + (buffaloCost - bolitas) + " bolitas");
            return;
        }

        bolitas -= buffaloCost;
        remaining = buffaloDuration;
        BuffaloActive = true;

        WaveBanner.Show("SALSA BUFFALO", "Tus disparos queman durante " + Mathf.RoundToInt(buffaloDuration) + " s");
    }

    private void EndBuffalo()
    {
        BuffaloActive = false;
        remaining = 0f;
        ShowMessage("Se acabo la salsa Buffalo");
    }

    private void ShowMessage(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 1.8f;
        messageDirty = true;
    }

    // --------------------------------------------------------------- HUD
    private void BuildHud()
    {
        GameObject canvasGO = new GameObject("SauceHUD_Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 85;   // por debajo del cartel (90) y del HUD de vida/pausa (100)

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Panel abajo en el centro.
        Image panel = NewImage("Panel", canvasGO.transform, new Color(0f, 0f, 0f, 0.45f));
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.sizeDelta = new Vector2(560f, 120f);
        prt.anchoredPosition = new Vector2(0f, 40f);

        statusText = NewText("Estado", prt, 32, idleColor, -8f, 40f);
        infoText = NewText("Info", prt, 26, idleColor, -86f, 34f);

        // Barra: fondo oscuro + relleno que se estira de izquierda a derecha.
        Image barBg = NewImage("BarraFondo", prt, new Color(0.1f, 0.1f, 0.1f, 0.85f));
        RectTransform brt = barBg.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.sizeDelta = new Vector2(520f, 24f);
        brt.anchoredPosition = new Vector2(0f, -54f);

        fill = NewImage("BarraRelleno", brt, barColor);
        RectTransform frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0f, 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
    }

    private void UpdateHud()
    {
        if (statusText == null) return;

        State state = BuffaloActive ? State.Active : (bolitas >= buffaloCost ? State.Ready : State.Idle);
        int secs = BuffaloActive ? Mathf.CeilToInt(remaining) : 0;
        bool showMessage = Time.unscaledTime < messageUntil;

        // Los textos solo se reescriben cuando algo cambia (evita basura en cada fotograma).
        if (state != lastState || bolitas != lastBolitas || secs != lastSecs ||
            showMessage != lastShowingMessage || messageDirty)
        {
            lastState = state;
            lastBolitas = bolitas;
            lastSecs = secs;
            lastShowingMessage = showMessage;
            messageDirty = false;

            switch (state)
            {
                case State.Active:
                    statusText.text = "SALSA BUFFALO  " + secs + " s";
                    statusText.color = activeColor;
                    infoText.text = "Tus disparos queman a los enemigos";
                    break;
                case State.Ready:
                    statusText.text = "[" + buffaloKey.ToString().Replace("Digit", "") + "] SALSA BUFFALO LISTA";
                    statusText.color = readyColor;
                    infoText.text = "Bolitas: " + bolitas + " / " + buffaloCost;
                    break;
                default:
                    statusText.text = "SALSA BUFFALO";
                    statusText.color = idleColor;
                    infoText.text = "Bolitas: " + bolitas + " / " + buffaloCost;
                    break;
            }

            if (showMessage) infoText.text = message;
        }

        float ratio;
        Color c;
        switch (state)
        {
            case State.Active:
                ratio = buffaloDuration > 0f ? remaining / buffaloDuration : 0f;
                c = activeColor;
                break;
            case State.Ready:
                ratio = 1f;
                c = Color.Lerp(readyColor, Color.white, (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.25f);
                break;
            default:
                ratio = buffaloCost > 0 ? (float)bolitas / buffaloCost : 1f;
                c = barColor;
                break;
        }

        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        fill.color = c;
    }

    // ----------------------------------------------------------- Helpers
    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Texto centrado, anclado arriba dentro del panel.
    private static Text NewText(string name, RectTransform parent, int size, Color color, float y, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = color;
        t.raycastTarget = false;

        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(520f, height);
        r.anchoredPosition = new Vector2(0f, y);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return f;
    }
}
