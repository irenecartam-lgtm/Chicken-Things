using System.Collections.Generic;
using UnityEngine;

// Deja restos pegados al suelo al morir el enemigo, sin fisica ninguna.
// Va en el prefab del Enemy. Lo llama EnemyHealth.Die().
public class DeathDebris : MonoBehaviour
{
    [Header("Trozos")]
    public int pieceCount = 12;
    public float pieceSize = 0.18f;
    [Range(0f, 0.9f)] public float sizeVariation = 0.5f;
    public PrimitiveType shape = PrimitiveType.Sphere;

    [Tooltip("Achatamiento contra el suelo. 1 = bolitas enteras, 0.3 = manchas planas.")]
    [Range(0.15f, 1f)] public float flatten = 0.45f;

    [Header("Colocacion")]
    [Tooltip("Radio del charco de restos alrededor del punto de muerte.")]
    public float scatterRadius = 0.7f;

    [Tooltip("Capas que cuentan como suelo.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Separacion del suelo para que no se incrusten.")]
    public float groundOffset = 0.02f;

    [Header("Permanencia")]
    [Tooltip("Tope global de restos. Al pasarse se borran los mas antiguos.")]
    public int maxPiecesInScene = 250;

    [Header("Color")]
    public bool useEnemyColor = true;
    public Color fallbackColor = new Color(0.85f, 0.55f, 0.3f);

    private static readonly Queue<GameObject> registry = new Queue<GameObject>();
    private static Mesh cachedMesh;
    private static PrimitiveType cachedShape;
    private static Material cachedMaterial;
    private static Color cachedColor;

    private Color bodyColor;
    private float feetOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registry.Clear();
        cachedMesh = null;
        cachedMaterial = null;
    }

    private void Awake()
    {
        // El color se guarda ANTES de que EnemyHealth lo ponga en blanco al recibir un disparo.
        Renderer r = GetComponentInChildren<Renderer>();

        bodyColor = fallbackColor;
        if (useEnemyColor && r != null && r.sharedMaterial != null)
            bodyColor = r.sharedMaterial.color;

        feetOffset = r != null ? r.bounds.min.y - transform.position.y : 0f;
    }

    public void Burst()
    {
        Material mat = GetMaterial(bodyColor);
        Mesh mesh = GetMesh(shape);
        Vector3 center = transform.position;

        for (int i = 0; i < pieceCount; i++)
        {
            Vector2 disc = Random.insideUnitCircle * scatterRadius;
            Vector3 spot = center + new Vector3(disc.x, 0f, disc.y);

            float size = pieceSize * Random.Range(1f - sizeVariation, 1f + sizeVariation);
            float height = size * flatten;

            GameObject piece = new GameObject("Resto", typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.localScale = new Vector3(size, height, size);
            piece.transform.position = new Vector3(spot.x, FindGroundY(spot) + height * 0.5f + groundOffset, spot.z);
            piece.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            piece.GetComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = piece.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Register(piece);
        }
    }

    // Rayo hacia abajo ignorando el propio cuerpo del enemigo: aun existe,
    // porque Destroy no surte efecto hasta el final del frame.
    private float FindGroundY(Vector3 spot)
    {
        Ray ray = new Ray(spot + Vector3.up * 2.5f, Vector3.down);
        float closest = float.MaxValue;
        float result = transform.position.y + feetOffset;

        RaycastHit[] hits = Physics.RaycastAll(ray, 8f, groundMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;

            if (c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;
            if (c.CompareTag("Player")) continue;

            if (hits[i].distance < closest)
            {
                closest = hits[i].distance;
                result = hits[i].point.y;
            }
        }
        return result;
    }

    // Cola FIFO: al pasarse del tope, los restos mas antiguos se van borrando.
    private void Register(GameObject piece)
    {
        registry.Enqueue(piece);

        while (registry.Count > Mathf.Max(1, maxPiecesInScene))
        {
            GameObject old = registry.Dequeue();
            if (old != null) Destroy(old);
        }
    }

    // La malla de un primitivo es un asset interno de Unity: sobrevive al objeto temporal.
    private static Mesh GetMesh(PrimitiveType type)
    {
        if (cachedMesh != null && cachedShape == type) return cachedMesh;

        GameObject temp = GameObject.CreatePrimitive(type);
        cachedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        cachedShape = type;
        Destroy(temp);

        return cachedMesh;
    }

    // Un unico material para todas las muertes: crear uno por enemigo seria una fuga.
    private static Material GetMaterial(Color color)
    {
        if (cachedMaterial != null && cachedColor == color) return cachedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        cachedMaterial = new Material(shader);
        cachedMaterial.color = color;
        cachedColor = color;

        return cachedMaterial;
    }
}