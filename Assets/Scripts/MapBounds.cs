using UnityEngine;

// Muros invisibles alrededor del suelo para que nadie pueda salirse del mapa.
// Se crea solo al dar a Play en cualquier escena que tenga un objeto llamado "Plane".
// Tambien rescata al jugador si aun asi cayese al vacio, y elimina enemigos caidos.
public class MapBounds : MonoBehaviour
{
    [Header("Zona jugable")]
    [Tooltip("Si esta activo, el tamaño se lee solo del objeto Plane (aunque cambies su escala).")]
    public bool autoFromPlane = true;
    public string groundName = "Plane";
    public Vector2 center = Vector2.zero;
    public Vector2 halfSize = new Vector2(95f, 70f);

    [Header("Muros invisibles")]
    [Tooltip("Cuanto se meten los muros hacia dentro desde el borde del suelo.")]
    public float inset = 1.5f;
    public float wallHeight = 80f;
    public float wallThickness = 4f;

    [Header("Red de seguridad")]
    [Tooltip("Si algo cae por debajo de esta altura, se rescata (jugador) o se elimina (enemigo).")]
    public float fallLimitY = -15f;

    private Transform player;
    private Rigidbody playerBody;
    private Vector3 startPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<MapBounds>() != null) return;
        if (GameObject.Find("Plane") == null) return;   // p. ej. el menu principal no tiene suelo
        new GameObject("MapBounds").AddComponent<MapBounds>();
    }

    private void Awake()
    {
        if (autoFromPlane) ReadGround();
        BuildWalls();
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerBody = p.GetComponent<Rigidbody>();
            startPosition = p.transform.position;
        }

        InvokeRepeating(nameof(RemoveFallenEnemies), 1f, 1f);
    }

    private void FixedUpdate()
    {
        if (player == null || player.position.y >= fallLimitY) return;

        // Red de seguridad: vuelve al punto de inicio, sin la velocidad de la caida.
        player.position = startPosition;
        if (playerBody != null)
        {
            playerBody.position = startPosition;
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }
    }

    // Un enemigo caido dejaria la oleada sin terminar para siempre, asi que se elimina.
    private void RemoveFallenEnemies()
    {
        foreach (EnemyHealth e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            if (e.transform.position.y < fallLimitY) Destroy(e.gameObject);
    }

    private void ReadGround()
    {
        GameObject ground = GameObject.Find(groundName);
        if (ground == null) return;

        Bounds b;
        Renderer r = ground.GetComponent<Renderer>();
        Collider c = ground.GetComponent<Collider>();
        if (r != null) b = r.bounds;
        else if (c != null) b = c.bounds;
        else return;

        if (b.size.x < 0.01f || b.size.z < 0.01f) return;

        center = new Vector2(b.center.x, b.center.z);
        halfSize = new Vector2(b.extents.x, b.extents.z);
    }

    private void BuildWalls()
    {
        float hx = Mathf.Max(1f, halfSize.x - inset);
        float hz = Mathf.Max(1f, halfSize.y - inset);
        float t = wallThickness;
        float y = wallHeight * 0.5f - 5f;   // empiezan un poco por debajo del suelo

        // Sin rozamiento: asi el personaje se desliza por el muro en vez de quedarse pegado.
        PhysicsMaterial slippery = new PhysicsMaterial("MuroSinRozamiento")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        float lengthX = hx * 2f + t * 2f;   // los muros de los lados largos cubren tambien las esquinas
        float lengthZ = hz * 2f + t * 2f;

        MakeWall("Muro +X", new Vector3(center.x + hx + t * 0.5f, y, center.y), new Vector3(t, wallHeight, lengthZ), slippery);
        MakeWall("Muro -X", new Vector3(center.x - hx - t * 0.5f, y, center.y), new Vector3(t, wallHeight, lengthZ), slippery);
        MakeWall("Muro +Z", new Vector3(center.x, y, center.y + hz + t * 0.5f), new Vector3(lengthX, wallHeight, t), slippery);
        MakeWall("Muro -Z", new Vector3(center.x, y, center.y - hz - t * 0.5f), new Vector3(lengthX, wallHeight, t), slippery);
    }

    private void MakeWall(string wallName, Vector3 worldPosition, Vector3 size, PhysicsMaterial material)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(transform, false);
        wall.transform.position = worldPosition;

        BoxCollider box = wall.AddComponent<BoxCollider>();
        box.size = size;
        box.sharedMaterial = material;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(center.x, 0f, center.y),
                            new Vector3((halfSize.x - inset) * 2f, 0.1f, (halfSize.y - inset) * 2f));
    }
}
