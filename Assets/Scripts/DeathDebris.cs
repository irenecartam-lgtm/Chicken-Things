using System.Collections.Generic;
using UnityEngine;

// Deja restos en el suelo al morir el enemigo. Va en el prefab del Enemy.
// Lo llama EnemyHealth.Die().
public class DeathDebris : MonoBehaviour
{
    [Header("Modo")]
    [Tooltip("ON: los restos aparecen pegados al suelo, sin fisica. " +
             "OFF: salen despedidos con impulso y caen rodando.")]
    public bool dropToGround = true;

    [Header("Trozos")]
    public int pieceCount = 12;
    public float pieceSize = 0.18f;
    [Range(0f, 0.9f)] public float sizeVariation = 0.5f;
    public PrimitiveType shape = PrimitiveType.Sphere;

    [Tooltip("Achatamiento contra el suelo. 1 = bolitas enteras, 0.3 = manchas planas.")]
    [Range(0.15f, 1f)] public float flatten = 0.45f;

    [Header("Restos en el suelo (con Drop To Ground en ON)")]
    [Tooltip("Radio del charco de restos alrededor del punto de muerte.")]
    public float scatterRadius = 0.7f;

    [Tooltip("Capas que cuentan como suelo.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Separacion del suelo para que no se incrusten.")]
    public float groundOffset = 0.02f;

    [Header("Impulso (solo con Drop To Ground en OFF)")]
    public float burstForce = 3.5f;
    public float upwardForce = 2.5f;
    public float spin = 8f;
    public float settleSpeed = 0.25f;
    public float maxRollTime = 6f;

    [Header("Permanencia")]
    [Tooltip("ON: los restos se quedan para siempre. OFF: desaparecen tras Lifetime.")]
    public bool persistent = true;
    public float lifetime = 3f;
    public float shrinkTime = 0.8f;

    [Tooltip("Tope global de restos. Al pasarse se borran los mas antiguos.")]
    public int maxPiecesInScene = 250;

    [Header("Color")]
    public bool useEnemyColor = true;
    public Color fallbackColor = new Color(0.85f, 0.55f, 0.3f);

    private static readonly Queue<GameObject> registry = new Queue<GameObject>();
    private static readonly Dictionary<PrimitiveType, Mesh> meshCache = new Dictionary<PrimitiveType, Mesh>();
    private static Material sharedDebrisMaterial;
    private static Color sharedDebrisColor;
    private static Collider playerCollider;

    private Color bodyColor;
    private Vector3 originOffset;
    private float feetOffset;
    private float bodyRadius = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registry.Clear();
        meshCache.Clear();
        sharedDebrisMaterial = null;
        playerCollider = null;
    }

    private void Awake()
    {
        // El color se guarda ANTES de que EnemyHealth lo ponga en blanco al recibir daño.
        Renderer r = GetComponentInChildren<Renderer>();

        bodyColor = fallbackColor;
        if (useEnemyColor && r != null && r.sharedMaterial != null)
            bodyColor = r.sharedMaterial.color;

        if (r != null)
        {
            originOffset = r.bounds.center - transform.position;
            feetOffset = r.bounds.min.y - transform.position.y;
            bodyRadius = r.bounds.extents.magnitude * 0.5f;
        }
        else
        {
            originOffset = Vector3.up * 0.8f;
            feetOffset = 0f;
        }
    }

    public void Burst()
    {
        Material mat = GetDebrisMaterial(bodyColor);

        if (dropToGround)
        {
            Vector3 center = transform.position;
            for (int i = 0; i < pieceCount; i++) SpawnGrounded(center, mat);
        }
        else
        {
            CachePlayerCollider();
            Vector3 origin = transform.position + originOffset;
            for (int i = 0; i < pieceCount; i++) SpawnPhysical(origin, mat);
        }
    }

    // ---------------------------------------------- Restos pegados al suelo

    private void SpawnGrounded(Vector3 center, Material mat)
    {
        Vector2 disc = Random.insideUnitCircle * scatterRadius;
        Vector3 spot = center + new Vector3(disc.x, 0f, disc.y);

        float groundY = FindGroundY(spot);
        float size = pieceSize * Random.Range(1f - sizeVariation, 1f + sizeVariation);
        float height = size * flatten;

        GameObject piece = NewPiece(mat, size, height);
        piece.transform.position = new Vector3(spot.x, groundY + height * 0.5f + groundOffset, spot.z);
        piece.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        AttachLifetime(piece);
        Register(piece);
    }

    // Se lanza el rayo desde arriba, ignorando el propio cuerpo del enemigo
    // (que aun existe: Destroy no se aplica hasta el final del frame).
    private float FindGroundY(Vector3 spot)
    {
        Ray ray = new Ray(spot + Vector3.up * 2.5f, Vector3.down);
        float closest = float.MaxValue;
        float result = transform.position.y + feetOffset;

        foreach (RaycastHit hit in Physics.RaycastAll(ray, 8f, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;              // su propio cuerpo
            if (hit.collider.GetComponentInParent<EnemyHealth>() != null) continue; // otros enemigos
            if (playerCollider != null && hit.collider == playerCollider) continue;

            if (hit.distance < closest)
            {
                closest = hit.distance;
                result = hit.point.y;
            }
        }
        return result;
    }

    // ------------------------------------------------- Restos con fisica

    private void SpawnPhysical(Vector3 origin, Material mat)
    {
        GameObject piece = GameObject.CreatePrimitive(shape);
        piece.name = "Resto";

        float size = pieceSize * Random.Range(1f - sizeVariation, 1f + sizeVariation);
        piece.transform.localScale = Vector3.one * size;
        piece.transform.position = origin + Random.insideUnitSphere * bodyRadius;
        piece.transform.rotation = Random.rotation;

        Renderer pr = piece.GetComponent<Renderer>();
        pr.sharedMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Collider pc = piece.GetComponent<Collider>();
        if (pc != null && playerCollider != null) Physics.IgnoreCollision(pc, playerCollider);

        Rigidbody rb = piece.AddComponent<Rigidbody>();
        rb.mass = 0.05f;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        Vector3 dir = (Random.insideUnitSphere + Vector3.up * 0.8f).normalized;
        rb.AddForce(dir * burstForce + Vector3.up * upwardForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * spin, ForceMode.Impulse);

        DebrisPiece dp = piece.AddComponent<DebrisPiece>();
        dp.physical = true;
        dp.persistent = persistent;
        dp.lifetime = lifetime * Random.Range(0.8f, 1.2f);
        dp.shrinkTime = shrinkTime;
        dp.settleSpeed = settleSpeed;
        dp.maxRollTime = maxRollTime;
        dp.flatten = flatten;

        Register(piece);
    }

    // ------------------------------------------------------------ Comunes

    // Sin CreatePrimitive: asi el trozo nace sin Collider y no hay que destruirlo despues.
    private GameObject NewPiece(Material mat, float size, float height)
    {
        GameObject piece = new GameObject("Resto", typeof(MeshFilter), typeof(MeshRenderer));
        piece.transform.localScale = new Vector3(size, height, size);

        piece.GetComponent<MeshFilter>().sharedMesh = GetMesh(shape);

        MeshRenderer mr = piece.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return piece;
    }

    private void AttachLifetime(GameObject piece)
    {
        if (persistent) return;   // permanente: ni siquiera necesita componente

        DebrisPiece dp = piece.AddComponent<DebrisPiece>();
        dp.physical = false;
        dp.persistent = false;
        dp.lifetime = lifetime * Random.Range(0.8f, 1.2f);
        dp.shrinkTime = shrinkTime;
    }

    private void CachePlayerCollider()
    {
        if (playerCollider != null) return;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerCollider = p.GetComponent<Collider>();
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
        if (meshCache.TryGetValue(type, out Mesh cached) && cached != null) return cached;

        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);

        meshCache[type] = mesh;
        return mesh;
    }

    // Un unico material para todas las muertes: crear uno por enemigo seria una fuga.
    private static Material GetDebrisMaterial(Color color)
    {
        if (sharedDebrisMaterial != null && sharedDebrisColor == color)
            return sharedDebrisMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");