using UnityEngine;
using UnityEngine.SceneManagement;

// Dibuja una barrera VISIBLE (valla luminosa con barrotes) justo donde estan los
// muros invisibles de MapBounds, para que se vea claramente que no se puede pasar.
// Se crea sola al dar a Play: no hay que ponerla en ninguna escena.
// Es solo visual (sin colliders): quien bloquea sigue siendo MapBounds.
public class VisibleBarrier : MonoBehaviour
{
    [Header("Tamano")]
    [Tooltip("Altura de la valla visible (los muros invisibles siguen siendo mucho mas altos).")]
    public float height = 6f;

    [Header("Aspecto")]
    public Color color = Color.white;
    [Tooltip("Transparencia del panel entre los barrotes (0 = solo barrotes, 1 = muro solido).")]
    [Range(0f, 1f)] public float fillAlpha = 0.22f;
    [Tooltip("Metros entre barrote y barrote.")]
    public float barSpacing = 2f;
    [Tooltip("La valla late suavemente para que se note que es una barrera de energia.")]
    public bool pulse = true;

    private Material material;

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
        if (FindFirstObjectByType<VisibleBarrier>() != null) return;
        if (GameObject.Find("Plane") == null) return;   // p. ej. el menu principal no tiene suelo
        new GameObject("VisibleBarrier").AddComponent<VisibleBarrier>();
    }

    // En Start (no en Awake) para que MapBounds ya haya leido el tamano del Plane.
    private void Start()
    {
        MapBounds bounds = FindFirstObjectByType<MapBounds>();
        if (bounds == null)
        {
            // MapBounds tiene el mismo limite que yo (solo se autocrea en la primera escena),
            // asi que al venir del menu puede faltar. Se crea aqui: tambien activa los muros.
            if (GameObject.Find("Plane") == null) return;
            bounds = new GameObject("MapBounds").AddComponent<MapBounds>();
        }

        // Mismos limites que los muros invisibles: su cara interior.
        float hx = Mathf.Max(1f, bounds.halfSize.x - bounds.inset);
        float hz = Mathf.Max(1f, bounds.halfSize.y - bounds.inset);
        Vector2 c = bounds.center;

        float groundY = 0f;
        GameObject ground = GameObject.Find(bounds.groundName);
        if (ground != null)
        {
            Renderer r = ground.GetComponent<Renderer>();
            if (r != null) groundY = r.bounds.max.y;
        }

        // Sprites/Default viene siempre incluido en el juego compilado, soporta transparencia
        // y se ve igual en URP que en el pipeline normal.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[VisibleBarrier] No se encontro el shader Sprites/Default.");
            return;
        }

        material = new Material(shader) { color = color, mainTexture = MakeTexture(fillAlpha) };

        // Los muros de +X / -X recorren el eje Z, y los de +Z / -Z recorren el eje X.
        MakeWall("Barrera +X", new Vector3(c.x + hx, groundY, c.y), Quaternion.Euler(0f, 90f, 0f), hz * 2f);
        MakeWall("Barrera -X", new Vector3(c.x - hx, groundY, c.y), Quaternion.Euler(0f, 90f, 0f), hz * 2f);
        MakeWall("Barrera +Z", new Vector3(c.x, groundY, c.y + hz), Quaternion.identity, hx * 2f);
        MakeWall("Barrera -Z", new Vector3(c.x, groundY, c.y - hz), Quaternion.identity, hx * 2f);

        Debug.Log($"[VisibleBarrier] Barrera creada: {hx * 2f} x {hz * 2f} m, a altura y={groundY}.");
    }

    private void Update()
    {
        if (!pulse || material == null) return;

        float a = Mathf.Lerp(0.65f, 1f, (Mathf.Sin(Time.time * 2f) + 1f) * 0.5f);
        material.color = new Color(color.r, color.g, color.b, a);
    }

    private void MakeWall(string wallName, Vector3 position, Quaternion rotation, float length)
    {
        GameObject go = new GameObject(wallName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(position, rotation);

        // Un panel con las UV repetidas: un barrote cada "barSpacing" metros.
        float tiles = Mathf.Max(1f, length / Mathf.Max(0.1f, barSpacing));
        Mesh mesh = new Mesh { name = wallName };
        mesh.vertices = new[]
        {
            new Vector3(-length * 0.5f, 0f, 0f),
            new Vector3( length * 0.5f, 0f, 0f),
            new Vector3(-length * 0.5f, height, 0f),
            new Vector3( length * 0.5f, height, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(tiles, 0f),
            new Vector2(0f, 1f),
            new Vector2(tiles, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();

        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    // Textura de una "celda": barrote vertical a la izquierda, rieles arriba y abajo,
    // y el resto casi transparente.
    private static Texture2D MakeTexture(float fillAlpha)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        byte fill = (byte)(Mathf.Clamp01(fillAlpha) * 255f);
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool bar = x < 6;
                bool rail = y < 4 || y >= size - 4;
                px[y * size + x] = new Color32(255, 255, 255, (bar || rail) ? (byte)255 : fill);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
