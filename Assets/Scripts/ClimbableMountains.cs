using UnityEngine;

// Montañas de pure de patata que SI se pueden subir: bolas con collider,
// apiladas en capas de mas ancha a mas estrecha, dentro del arena.
// Va en un objeto vacio en el centro del mapa.
//
// Las bolas son de escala uniforme a proposito. Un SphereCollider con escala
// no uniforme usa el eje mayor, asi que una bola achatada tendria un collider
// esferico entero: andarias sobre una pelota invisible mas grande que lo que ves.
public class ClimbableMountains : MonoBehaviour
{
    public enum Layout { Rectangulo, Anillo }

    [Header("Cuantas y donde")]
    public int mountainCount = 30;

    [Tooltip("Rectangulo: repartidas por todo el suelo. Anillo: en un aro alrededor del centro.")]
    public Layout layout = Layout.Rectangulo;

    [Tooltip("Zona libre alrededor del centro, donde empieza el jugador. Vale en los dos modos.")]
    public float innerRadius = 15f;

    [Tooltip("Separacion minima entre dos montañas. Si lo subes mucho dejan de caber.")]
    public float minSeparation = 14f;

    [Header("Reparto en rectangulo")]
    [Tooltip("Ancho (X) y fondo (Z) de la zona donde se reparten. " +
             "Un Plane de escala S mide 10*S, asi que dejale algo de margen.")]
    public Vector2 areaSize = new Vector2(120f, 120f);

    [Header("Reparto en anillo")]
    [Tooltip("Hasta donde llegan en modo Anillo.")]
    public float outerRadius = 11f;

    [Tooltip("Cambia este numero para generar otro paisaje. El mismo numero da siempre el mismo.")]
    public int seed = 7;

    public float groundY = 0f;

    [Tooltip("Copia aqui el Ring Radius del WaveSpawner. Solo sirve para dibujar el gizmo rojo " +
             "y comprobar de un vistazo que las montañas no invaden la zona de aparicion.")]
    public float spawnerRingRadius = 13f;

    [Header("Forma de cada montaña")]
    [Tooltip("Radio de la bola de la base.")]
    public Vector2 baseRadius = new Vector2(4f, 6f);

    [Tooltip("Capas apiladas. Mas capas = mas alta.")]
    public Vector2Int layers = new Vector2Int(3, 5);

    [Tooltip("Cuanto encoge el radio en cada capa. 0.7 es una silueta de colina.")]
    [Range(0.5f, 0.9f)] public float radiusFalloff = 0.72f;

    [Tooltip("Pendiente maxima en el borde. DEBE ser igual o menor que el Max Slope Angle del jugador.")]
    [Range(25f, 65f)] public float maxSlopeAngle = 55f;

    [Header("Bolas sueltas de adorno")]
    [Tooltip("Bolas extra alrededor de la base, para que no parezca un cono.")]
    public Vector2Int skirtBlobs = new Vector2Int(4, 8);

    [Header("Aspecto")]
    public Color potatoColor = new Color(0.94f, 0.89f, 0.66f);
    public bool castShadows = true;
    [Range(0f, 0.15f)] public float colorVariation = 0.05f;

    [Header("Diagnostico")]
    public int generatedBlobs = 0;
    public float tallestPeak = 0f;

    private const string ContainerName = "MontanasEscalables";

    private static Mesh sphereMesh;
    private readonly System.Collections.Generic.Dictionary<Color, Material> materials =
        new System.Collections.Generic.Dictionary<Color, Material>();

    private void Start()
    {
        if (transform.Find(ContainerName) == null) Generate();
    }

    [ContextMenu("Generar ahora")]
    public void Generate()
    {
        Clear();

        Transform container = new GameObject(ContainerName).transform;
        container.SetParent(transform, false);

        Random.State saved = Random.state;
        Random.InitState(seed);

        Mesh mesh = GetSphereMesh();
        generatedBlobs = 0;
        tallestPeak = 0f;

        // sink = cos(pendiente): es la proporcion del radio que la bola
        // tiene que quedar enterrada para que su borde no supere ese angulo.
        float sink = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);

        var centers = new System.Collections.Generic.List<Vector3>();

        for (int m = 0; m < mountainCount; m++)
        {
            if (!FindSpot(centers, out Vector3 center))
            {
                Debug.LogWarning($"[ClimbableMountains] Solo caben {centers.Count} de {mountainCount}. " +
                                 $"Baja Min Separation (ahora {minSeparation}) o agranda Area Size.", this);
                break;
            }
            centers.Add(center);

            Material mat = GetMaterial(Tint());
            float radius = Random.Range(baseRadius.x, baseRadius.y);
            int layerCount = Random.Range(layers.x, layers.y + 1);

            float surface = groundY;

            for (int i = 0; i < layerCount; i++)
            {
                // Centro enterrado bajo la superficie en la que se apoya.
                float cy = surface - sink * radius;

                // Las capas altas se desplazan un poco: cumbres torcidas, no conos.
                Vector2 wobble = i == 0 ? Vector2.zero : Random.insideUnitCircle * (radius * 0.18f);

                BuildBlob(container, mesh, mat,
                          new Vector3(center.x + wobble.x, cy, center.z + wobble.y), radius);

                surface = cy + radius;          // la nueva cima pasa a ser el suelo de la siguiente
                radius *= radiusFalloff;
            }

            tallestPeak = Mathf.Max(tallestPeak, surface - groundY);

            // Faldas: bolas sueltas alrededor de la base.
            float skirtRadius = Random.Range(baseRadius.x, baseRadius.y);
            int skirt = Random.Range(skirtBlobs.x, skirtBlobs.y + 1);

            for (int s = 0; s < skirt; s++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = skirtRadius * Random.Range(0.8f, 1.3f);
                float r = skirtRadius * Random.Range(0.3f, 0.55f);

                Vector3 position = new Vector3(
                    center.x + Mathf.Cos(angle) * distance,
                    groundY - sink * r,
                    center.z + Mathf.Sin(angle) * distance);

                BuildBlob(container, mesh, mat, position, r);
            }
        }

        Random.state = saved;
    }

    [ContextMenu("Borrar")]
    public void Clear()
    {
        Transform existing = transform.Find(ContainerName);
        if (existing == null) return;

        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);

        generatedBlobs = 0;
        tallestPeak = 0f;
    }

    // ------------------------------------------------------------ Internos

    private bool FindSpot(System.Collections.Generic.List<Vector3> taken, out Vector3 spot)
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            spot = layout == Layout.Rectangulo ? RandomInRectangle() : RandomInRing();
            spot.y = groundY;

            // Zona libre en el centro: ahi empieza el jugador.
            Vector3 toCenter = spot - transform.position;
            toCenter.y = 0f;
            if (toCenter.magnitude < innerRadius) continue;

            bool clear = true;
            foreach (Vector3 other in taken)
            {
                Vector3 diff = spot - other;
                diff.y = 0f;
                if (diff.magnitude < minSeparation) { clear = false; break; }
            }

            if (clear) return true;
        }

        spot = Vector3.zero;
        return false;
    }

    private Vector3 RandomInRectangle()
    {
        return transform.position + new Vector3(
            Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
            0f,
            Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f));
    }

    private Vector3 RandomInRing()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        // sqrt reparte por area, no por radio: sin el se amontonarian en el centro.
        float distance = Mathf.Lerp(innerRadius, outerRadius, Mathf.Sqrt(Random.value));
        return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
    }

    private void BuildBlob(Transform parent, Mesh mesh, Material mat, Vector3 position, float radius)
    {
        GameObject blob = new GameObject("Monte", typeof(MeshFilter), typeof(MeshRenderer));
        blob.transform.SetParent(parent, false);
        blob.transform.position = position;

        // Escala uniforme: imprescindible para que el SphereCollider sea exacto.
        blob.transform.localScale = Vector3.one * (radius * 2f);
        blob.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        blob.GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer mr = blob.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = castShadows
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;

        // El collider va en escala local: la malla de Unity tiene radio 0.5.
        SphereCollider col = blob.AddComponent<SphereCollider>();
        col.radius = 0.5f;

        generatedBlobs++;
    }

    private Color Tint()
    {
        float v = Random.Range(-colorVariation, colorVariation);
        return new Color(
            Mathf.Clamp01(potatoColor.r + v),
            Mathf.Clamp01(potatoColor.g + v * 0.8f),
            Mathf.Clamp01(potatoColor.b + v * 0.5f),
            1f);
    }

    private Material GetMaterial(Color color)
    {
        if (materials.TryGetValue(color, out Material cached) && cached != null) return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;
        materials[color] = mat;
        return mat;
    }

    private static Mesh GetSphereMesh()
    {
        if (sphereMesh != null) return sphereMesh;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;

        if (Application.isPlaying) Destroy(temp);
        else DestroyImmediate(temp);

        return sphereMesh;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        DrawCircle(innerRadius);         // zona libre del centro

        Gizmos.color = Color.yellow;
        if (layout == Layout.Rectangulo)
            Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, 0.1f, areaSize.y));
        else
            DrawCircle(outerRadius);

        Gizmos.color = Color.red;
        DrawCircle(spawnerRingRadius);   // anillo de aparicion del spawner
    }

    private void DrawCircle(float radius)
    {
        Vector3 previous = transform.position + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48f;
            Vector3 current = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }
}