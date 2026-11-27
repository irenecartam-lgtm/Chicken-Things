using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Panel fijo arriba a la izquierda con la informacion de la partida:
// oleada actual, enemigos que quedan y puntos.
// Se crea solo al dar a Play en cualquier escena que tenga un WaveSpawner.
public class WaveHUD : MonoBehaviour
{
    [Header("Posicion")]
    public Vector2 margin = new Vector2(40f, 40f);
    public Vector2 panelSize = new Vector2(460f, 200f);

    [Header("Aspecto")]
    public int waveSize = 60;
    public int infoSize = 36;
    public Color waveColor = new Color(1f, 0.78f, 0.2f);
    public Color textColor = Color.white;
    public Color panelColor = new Color(0f, 0f, 0f, 0.45f);

    private WaveSpawner spawner;
    private Text waveLabel, enemiesLabel, scoreLabel;

    // Para no reescribir el texto (y generar basura) en cada fotograma.
    private string lastWave = "", lastEnemies = "", lastScore = "";

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
        if (FindFirstObjectByType<WaveHUD>() != null) return;

        // Escena de juego = la que tiene GameManager o WaveSpawner (el menu no tiene ninguno).
        if (FindFirstObjectByType<GameManager>() == null &&
            FindFirstObjectByType<WaveSpawner>() == null) return;

        new GameObject("WaveHUD").AddComponent<WaveHUD>();
    }

    private void Awake()
    {
        Build();
        Debug.Log("[WaveHUD] Panel creado.");
    }

    private void Update()
    {
        if (waveLabel == null) return;

        if (spawner == null)
        {
            spawner = FindFirstObjectByType<WaveSpawner>();
            if (spawner == null)
            {
                // El panel sale igual para que se vea que funciona, y avisa de lo que falta.
                if (lastWave != "NO WAVESPAWNER")
                {
                    lastWave = "NO WAVESPAWNER";
                    waveLabel.text = "SIN OLEADAS";
                    enemiesLabel.text = "Falta el WaveSpawner";
                }
                int sc = GameManager.instance != null ? GameManager.instance.score : 0;
                scoreLabel.text = "Puntos: " + sc;
                return;
            }
        }

        // WaveToResume es la oleada actual si hay una en curso, o la siguiente si no.
        bool inProgress = spawner.WaveToResume == spawner.currentWave;

        string wave;
        string enemies;
        if (inProgress)
        {
            int left = spawner.EnemiesAlive + spawner.RemainingToSpawn;
            wave = "OLEADA " + spawner.currentWave;
            enemies = "Enemigos: " + left + " / " + spawner.totalThisWave;
        }
        else
        {
            wave = "OLEADA " + (spawner.currentWave + 1);
            enemies = "Preparate...";
        }

        int score = GameManager.instance != null ? GameManager.instance.score : 0;
        string scoreText = "Puntos: " + score;

        if (wave != lastWave) { lastWave = wave; waveLabel.text = wave; }
        if (enemies != lastEnemies) { lastEnemies = enemies; enemiesLabel.text = enemies; }
        if (scoreText != lastScore) { lastScore = scoreText; scoreLabel.text = scoreText; }
    }

    // ------------------------------------------------------------------ UI
    private void Build()
    {
        GameObject canvasGO = new GameObject("WaveHUD_Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;   // por debajo del cartel (90) y del HUD de vida/pausa (100)

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Panel arriba a la izquierda.
        GameObject panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);

        Image panel = panelGO.GetComponent<Image>();
        panel.color = panelColor;
        panel.raycastTarget = false;

        RectTransform rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = panelSize;
        rt.anchoredPosition = new Vector2(margin.x, -margin.y);

        waveLabel = NewText("Oleada", rt, waveSize, waveColor, -10f, 80f);
        enemiesLabel = NewText("Enemigos", rt, infoSize, textColor, -90f, 50f);
        scoreLabel = NewText("Puntos", rt, infoSize, textColor, -140f, 50f);
    }

    // Texto alineado a la izquierda, anclado arriba dentro del panel.
    private Text NewText(string name, RectTransform parent, int size, Color color, float y, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = color;
        t.raycastTarget = false;

        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(panelSize.x - 40f, height);
        r.anchoredPosition = new Vector2(20f, y);

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