using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum SauceType { Buffalo, Honey, Cheese, Mayo, HotSauce, BlackPepper }

[System.Serializable]
public class SauceInfo
{
    public string name = "SALSA";
    [Tooltip("Bolitas que cuesta activarla.")]
    public int cost = 30;
    [Tooltip("Segundos que dura el poder.")]
    public float duration = 30f;
    public Color color = Color.white;
    public string description = "";

    public SauceInfo() { }

    public SauceInfo(string name, int cost, float duration, Color color, string description)
    {
        this.name = name;
        this.cost = cost;
        this.duration = duration;
        this.color = color;
        this.description = description;
    }
}

// Cuenta las bolitas que recoge el jugador y gestiona las salsas (superpoderes).
//   E            abre el selector de salsas
//   Rueda        elige salsa
//   E (otra vez) la activa (gasta las bolitas); Q cierra sin activar
// Solo hay una salsa activa a la vez y dura "duration" segundos.
// Se crea solo al dar a Play en la escena del juego (no hay que ponerlo en ningun objeto).
public class SauceInventory : MonoBehaviour
{
    public static SauceInventory Instance { get; private set; }

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

    [Header("Seleccion")]
    public Key menuKey = Key.E;
    public Key cancelKey = Key.Q;
    [Tooltip("Por defecto, rueda hacia arriba = salsa anterior. Marcalo para invertirlo.")]
    public bool invertScroll;
    [Tooltip("Segundos minimos entre un cambio de salsa y el siguiente (evita saltarse varias de golpe).")]
    public float scrollCooldown = 0.12f;

    [Header("Salsas (orden: Buffalo, Miel, Queso, Mayonesa, Hot Sauce, Pimienta negra)")]
    public SauceInfo[] sauces = new SauceInfo[]
    {
        new SauceInfo("BUFFALO",        30, 30f, new Color(1f, 0.3f, 0.05f),     "Un disparo prende fuego al enemigo"),
        new SauceInfo("MIEL",           60, 30f, new Color(0.98f, 0.62f, 0.08f), "Ralentiza al enemigo 10 s"),
        new SauceInfo("QUESO",         100, 30f, new Color(1f, 0.9f, 0.25f),     "Pega al enemigo al suelo 10 s"),
        new SauceInfo("MAYONESA",      150, 30f, new Color(0.97f, 0.95f, 0.82f), "Parte del daño que haces te cura"),
        new SauceInfo("HOT SAUCE",     210, 30f, new Color(0.8f, 0.05f, 0.08f),  "Explota y daña a los enemigos de al lado"),
        new SauceInfo("PIMIENTA NEGRA", 280, 30f, new Color(0.35f, 0.35f, 0.4f), "Las balas rebotan entre enemigos")
    };

    [Header("Buffalo (fuego)")]
    [Tooltip("Segundos que arde un enemigo despues de un disparo.")]
    public float burnDuration = 5f;
    [Tooltip("Daño que hace el fuego en cada golpe de quemadura.")]
    public int burnDamagePerTick = 1;
    [Tooltip("Segundos entre un golpe de quemadura y el siguiente.")]
    public float burnTickInterval = 0.8f;

    [Header("Miel (ralentizar)")]
    [Tooltip("Velocidad del enemigo ralentizado: 0.35 = al 35 %.")]
    [Range(0.05f, 1f)] public float honeySlowFactor = 0.35f;
    [Tooltip("Segundos que dura la ralentizacion tras un disparo.")]
    public float honeyEffectDuration = 10f;

    [Header("Queso (pegar al suelo)")]
    [Tooltip("Segundos que el enemigo queda pegado tras un disparo.")]
    public float cheeseEffectDuration = 10f;

    [Header("Mayonesa (curacion)")]
    [Tooltip("Vida que recuperas por cada punto de daño que haces. 0.25 = 1 de vida cada 4 de daño.")]
    public float mayoHealPerDamage = 0.25f;

    [Header("Hot Sauce (explosion)")]
    public float explosionRadius = 3.5f;
    public int explosionDamage = 2;

    [Header("Pimienta negra (rebote)")]
    [Tooltip("Cuantas veces rebota la bala en otros enemigos.")]
    public int pepperBounces = 3;
    [Tooltip("Distancia maxima a la que busca el siguiente enemigo.")]
    public float pepperRange = 8f;

    [Header("Aspecto del HUD")]
    public Color idleColor = new Color(1f, 1f, 1f, 0.9f);
    public Color barColor = new Color(1f, 0.55f, 0.1f);
    public Color readyColor = new Color(1f, 0.85f, 0.2f);

    [Header("Estado (solo lectura)")]
    public int bolitas;

    public Transform PlayerTransform => player;

    // -------------------------------------------------------- API estatica

    public static bool IsActive(SauceType type)
    {
        return Instance != null && Instance.sauceActive && Instance.activeSauce == type;
    }

    // Las lee Bullet al nacer.
    public static int PepperBounces => IsActive(SauceType.BlackPepper) ? Instance.pepperBounces : 0;
    public static float PepperRange => Instance != null ? Instance.pepperRange : 0f;

    public static bool TryGetActiveColor(out Color color)
    {
        if (Instance != null && Instance.sauceActive)
        {
            SauceInfo info = Instance.InfoOf(Instance.activeSauce);
            if (info != null)
            {
                color = info.color;
                return true;
            }
        }

        color = Color.white;
        return false;
    }

    // La llama Bullet cada vez que una bala acierta a un enemigo.
    // damageDealt = daño real que ha hecho (para la mayonesa).
    public static void OnBulletHit(EnemyHealth target, Vector3 point, Vector3 direction, int damageDealt)
    {
        SauceInventory inv = Instance;
        if (inv == null || !inv.sauceActive || target == null) return;

        switch (inv.activeSauce)
        {
            case SauceType.Buffalo:
                if (!target.IsDead)
                    EnemyStatus.Burn(target, inv.burnDuration, inv.burnDamagePerTick, inv.burnTickInterval);
                break;

            case SauceType.Honey:
                if (!target.IsDead)
                    EnemyStatus.Slow(target, inv.honeySlowFactor, inv.honeyEffectDuration);
                break;

            case SauceType.Cheese:
                if (!target.IsDead)
                    EnemyStatus.Root(target, inv.cheeseEffectDuration);
                break;

            case SauceType.Mayo:
                inv.HealFromDamage(damageDealt);
                break;

            case SauceType.HotSauce:
                inv.Explode(target, point);
                break;

            // Pimienta negra: la gestiona Bullet (rebota).
        }
    }

    // La llama DebrisPickup al recoger una bolita.
    public void Collect(int amount)
    {
        bolitas += amount;
    }

    // --------------------------------------------------------------- Estado
    private Transform player;

    private bool sauceActive;
    private SauceType activeSauce;
    private float remaining;

    private int selected;
    private bool menuOpen;
    private float nextScrollTime;

    private float healBuffer;
    private readonly List<EnemyHealth> scratch = new List<EnemyHealth>();

    // HUD
    private class Card
    {
        public RectTransform rect;
        public Image bg;
        public Image swatch;
        public Outline outline;
        public Text nameText;
        public Text costText;
    }

    private Text statusText, infoText, descText, hintText;
    private Image fill;
    private RectTransform selectorRoot;
    private readonly List<Card> cards = new List<Card>();

    private bool lastActive, lastMenuOpen;
    private int lastBolitas = -1, lastSecs = -1, lastSelected = -1;
    private bool lastShowingMessage;
    private bool messageDirty = true;
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

    private SauceInfo InfoOf(SauceType type)
    {
        int i = (int)type;
        return (sauces != null && i >= 0 && i < sauces.Length) ? sauces[i] : null;
    }

    // ---------------------------------------------------------------- Ciclo
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildHud();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (sauces == null || sauces.Length == 0) return;
        selected = Mathf.Clamp(selected, 0, sauces.Length - 1);

        // En pausa y en Game Over el tiempo esta a 0: ni corre la salsa ni se leen las teclas.
        if (Time.timeScale <= 0f)
        {
            menuOpen = false;
        }
        else
        {
            if (sauceActive)
            {
                remaining -= Time.deltaTime;
                if (remaining <= 0f) EndSauce();
            }

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[menuKey].wasPressedThisFrame)
                {
                    if (!menuOpen) OpenMenu();
                    else ConfirmSelection();
                }
                else if (menuOpen && kb[cancelKey].wasPressedThisFrame)
                {
                    menuOpen = false;
                }
            }

            if (menuOpen) HandleScroll();
        }

        UpdateHud();
    }

    // ------------------------------------------------------------ Seleccion
    private void OpenMenu()
    {
        menuOpen = true;
        RefreshCardTexts();
    }

    // Rueda del raton: arriba = anterior, abajo = siguiente.
    private void HandleScroll()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || Time.unscaledTime < nextScrollTime) return;

        float dy = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(dy) < 0.01f) return;

        int dir = dy > 0f ? -1 : 1;
        if (invertScroll) dir = -dir;

        selected = (selected + dir + sauces.Length) % sauces.Length;
        nextScrollTime = Time.unscaledTime + scrollCooldown;
    }

    private void ConfirmSelection()
    {
        SauceInfo info = sauces[selected];

        if (sauceActive)
        {
            ShowMessage("Ya hay una salsa activa");
            menuOpen = false;
            return;
        }

        if (bolitas < info.cost)
        {
            // El selector sigue abierto para poder elegir otra.
            ShowMessage("Te faltan " + (info.cost - bolitas) + " bolitas");
            return;
        }

        bolitas -= info.cost;
        activeSauce = (SauceType)selected;
        remaining = info.duration;
        sauceActive = true;
        menuOpen = false;

        WaveBanner.Show("SALSA " + info.name, info.description);
    }

    private void EndSauce()
    {
        SauceInfo info = InfoOf(activeSauce);
        sauceActive = false;
        remaining = 0f;
        healBuffer = 0f;
        ShowMessage("Se acabó la salsa " + (info != null ? info.name : ""));
    }

    private void ShowMessage(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 1.8f;
        messageDirty = true;
    }

    // ------------------------------------------------------------- Efectos

    // Mayonesa: parte del daño que haces se convierte en vida. La fraccion se acumula
    // hasta sumar 1 punto entero (la vida del jugador es un numero entero).
    private void HealFromDamage(int damageDealt)
    {
        if (damageDealt <= 0 || GameManager.instance == null) return;

        healBuffer += damageDealt * mayoHealPerDamage;
        int whole = Mathf.FloorToInt(healBuffer);
        if (whole <= 0) return;

        healBuffer -= whole;
        int healed = GameManager.instance.Heal(whole);
        if (healed < whole) healBuffer = 0f;   // ya estaba al maximo: no se guarda para mas tarde
    }

    // Hot Sauce: explota en el punto de impacto y daña a los enemigos cercanos (no al que ha recibido el disparo).
    private void Explode(EnemyHealth hit, Vector3 point)
    {
        SauceFX.Explosion(point, explosionRadius);

        // Copia de la lista: matar enemigos podria modificarla mientras se recorre.
        scratch.Clear();
        scratch.AddRange(EnemyHealth.All);

        foreach (EnemyHealth e in scratch)
        {
            if (e == null || e == hit || e.IsDead) continue;

            // Distancia al punto mas cercano de su collider: los enemigos grandes cuentan por su tamaño.
            Collider c = e.GetComponent<Collider>();
            if (c == null) c = e.GetComponentInChildren<Collider>();

            Vector3 closest = c != null ? c.bounds.ClosestPoint(point) : e.transform.position;
            if ((closest - point).sqrMagnitude > explosionRadius * explosionRadius) continue;

            // El empujon sale desde el centro de la explosion.
            e.TakeDamage(explosionDamage, e.transform.position - point);
        }
    }

    // ------------------------------------------------------------------ HUD
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

        // Panel de estado, abajo en el centro.
        Image panel = NewImage("Panel", canvasGO.transform, new Color(0f, 0f, 0f, 0.45f));
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.sizeDelta = new Vector2(620f, 120f);
        prt.anchoredPosition = new Vector2(0f, 40f);

        statusText = NewText("Estado", prt, 32, idleColor, -8f, 40f, 580f);
        infoText = NewText("Info", prt, 24, idleColor, -86f, 34f, 580f);

        // Barra: fondo oscuro + relleno que se estira de izquierda a derecha.
        Image barBg = NewImage("BarraFondo", prt, new Color(0.1f, 0.1f, 0.1f, 0.85f));
        RectTransform brt = barBg.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.sizeDelta = new Vector2(580f, 24f);
        brt.anchoredPosition = new Vector2(0f, -54f);

        fill = NewImage("BarraRelleno", brt, barColor);
        RectTransform frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0f, 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;

        BuildSelector(canvasGO.transform);
    }

    // Fila de tarjetas, una por salsa, encima del panel. Solo se ve con el selector abierto.
    private void BuildSelector(Transform canvasTransform)
    {
        const float cardW = 156f, cardH = 124f, gap = 8f;
        int n = sauces.Length;
        float totalW = n * cardW + (n - 1) * gap;

        GameObject rootGO = new GameObject("Selector", typeof(RectTransform));
        rootGO.transform.SetParent(canvasTransform, false);
        selectorRoot = (RectTransform)rootGO.transform;
        selectorRoot.anchorMin = selectorRoot.anchorMax = new Vector2(0.5f, 0f);
        selectorRoot.pivot = new Vector2(0.5f, 0f);
        selectorRoot.sizeDelta = new Vector2(totalW, 230f);
        selectorRoot.anchoredPosition = new Vector2(0f, 175f);

        hintText = NewBottomText("Ayuda", selectorRoot, 24, new Color(1f, 1f, 1f, 0.75f), 192f, 32f, totalW);
        hintText.text = "Rueda: elegir     E: activar     Q: cerrar";

        descText = NewBottomText("Descripcion", selectorRoot, 30, readyColor, 140f, 40f, totalW);

        for (int i = 0; i < n; i++)
        {
            Card card = new Card();

            Image bg = NewImage("Salsa" + i, selectorRoot, new Color(0f, 0f, 0f, 0.6f));
            card.bg = bg;
            card.rect = bg.rectTransform;
            card.rect.anchorMin = card.rect.anchorMax = new Vector2(0.5f, 0f);
            card.rect.pivot = new Vector2(0.5f, 0f);
            card.rect.sizeDelta = new Vector2(cardW, cardH);
            card.rect.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * (cardW + gap), 0f);

            card.outline = bg.gameObject.AddComponent<Outline>();
            card.outline.effectColor = Color.white;
            card.outline.effectDistance = new Vector2(4f, -4f);
            card.outline.enabled = false;

            card.swatch = NewImage("Color", card.rect, Color.white);
            RectTransform srt = card.swatch.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(44f, 44f);
            srt.anchoredPosition = new Vector2(0f, -10f);

            card.nameText = NewText("Nombre", card.rect, 18, Color.white, -62f, 28f, cardW);
            card.costText = NewText("Coste", card.rect, 22, readyColor, -90f, 30f, cardW);

            cards.Add(card);
        }

        RefreshCardTexts();
        selectorRoot.gameObject.SetActive(false);
    }

    // Nombre y coste de cada tarjeta (por si se han cambiado en el Inspector).
    private void RefreshCardTexts()
    {
        for (int i = 0; i < cards.Count && i < sauces.Length; i++)
        {
            cards[i].nameText.text = sauces[i].name;
            cards[i].costText.text = sauces[i].cost + " bolitas";
        }
    }

    private void UpdateHud()
    {
        if (statusText == null) return;

        SauceInfo sel = sauces[selected];
        bool ready = bolitas >= sel.cost;
        int secs = sauceActive ? Mathf.CeilToInt(remaining) : 0;
        bool showMessage = Time.unscaledTime < messageUntil;

        // Los textos solo se reescriben cuando algo cambia (evita basura en cada fotograma).
        if (sauceActive != lastActive || bolitas != lastBolitas || secs != lastSecs ||
            selected != lastSelected || showMessage != lastShowingMessage || messageDirty)
        {
            lastActive = sauceActive;
            lastBolitas = bolitas;
            lastSecs = secs;
            lastSelected = selected;
            lastShowingMessage = showMessage;
            messageDirty = false;

            if (sauceActive)
            {
                SauceInfo act = InfoOf(activeSauce);
                statusText.text = "SALSA " + act.name + "  " + secs + " s";
                statusText.color = act.color;
                infoText.text = act.description;
            }
            else
            {
                statusText.text = ready ? "SALSA " + sel.name + " LISTA" : "SALSA " + sel.name;
                statusText.color = ready ? readyColor : idleColor;
                infoText.text = "Bolitas: " + bolitas + " / " + sel.cost + "     [" + KeyLabel(menuKey) + "] elegir salsa";
            }

            if (showMessage) infoText.text = message;

            descText.text = sel.description;
        }

        // Barra: tiempo que queda de la salsa, o lo cerca que estas de poder activar la elegida.
        float ratio;
        Color c;
        if (sauceActive)
        {
            SauceInfo act = InfoOf(activeSauce);
            ratio = act.duration > 0f ? remaining / act.duration : 0f;
            c = act.color;
        }
        else if (ready)
        {
            ratio = 1f;
            c = Color.Lerp(readyColor, Color.white, (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.25f);
        }
        else
        {
            ratio = sel.cost > 0 ? (float)bolitas / sel.cost : 1f;
            c = barColor;
        }

        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        fill.color = c;

        // Selector.
        if (menuOpen != lastMenuOpen)
        {
            lastMenuOpen = menuOpen;
            selectorRoot.gameObject.SetActive(menuOpen);
        }
        if (menuOpen) RefreshCards();
    }

    // Resalta la elegida y apaga las que aun no te puedes permitir.
    private void RefreshCards()
    {
        for (int i = 0; i < cards.Count && i < sauces.Length; i++)
        {
            Card card = cards[i];
            bool isSelected = i == selected;
            bool isActive = sauceActive && (int)activeSauce == i;
            bool canAfford = bolitas >= sauces[i].cost;
            float a = (canAfford || isActive) ? 1f : 0.4f;

            card.bg.color = isSelected ? new Color(0.2f, 0.2f, 0.2f, 0.92f) : new Color(0f, 0f, 0f, 0.6f);
            card.outline.enabled = isSelected;
            card.rect.localScale = Vector3.one * (isSelected ? 1.12f : 1f);

            Color sc = sauces[i].color;
            card.swatch.color = new Color(sc.r, sc.g, sc.b, a);
            card.nameText.color = new Color(1f, 1f, 1f, a);
            card.costText.color = canAfford ? readyColor : new Color(1f, 0.35f, 0.3f, 0.8f);
        }
    }

    private static string KeyLabel(Key key)
    {
        return key.ToString().Replace("Digit", "");
    }

    // -------------------------------------------------------------- Helpers
    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Texto centrado, anclado arriba dentro de su contenedor (y = distancia al borde de arriba, negativa).
    private static Text NewText(string name, RectTransform parent, int size, Color color, float y, float height, float width)
    {
        Text t = BaseText(name, parent, size, color);

        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(width, height);
        r.anchoredPosition = new Vector2(0f, y);
        return t;
    }

    // Texto centrado, anclado abajo dentro de su contenedor (y = distancia al borde de abajo).
    private static Text NewBottomText(string name, RectTransform parent, int size, Color color, float y, float height, float width)
    {
        Text t = BaseText(name, parent, size, color);

        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(width, height);
        r.anchoredPosition = new Vector2(0f, y);
        return t;
    }

    private static Text BaseText(string name, Transform parent, int size, Color color)
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
