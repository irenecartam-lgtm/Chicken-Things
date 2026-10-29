using UnityEngine;

// Enemigo que persigue SIEMPRE al jugador (sin patrulla ni cono de vision).
// Sabe subir las montañas: proyecta su avance sobre la pendiente en vez de
// empujar contra ella, igual que hace el jugador.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyChaser : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 3.5f;
    public float acceleration = 20f;
    public float turnSpeed = 10f;

    [Header("Suelo y pendientes")]
    public LayerMask groundMask = ~0;
    [Range(20f, 70f)] public float maxSlopeAngle = 55f;
    public float groundProbeRadius = 0.3f;
    public float groundProbeExtra = 0.3f;
    public float stickToGround = 3f;

    [Header("Daño por contacto")]
    public int damageAmount = 1;
    public float damageCooldown = 1f;

    [Header("Diagnostico")]
    public bool isGrounded;

    private Rigidbody rb;
    private Collider col;
    private Transform player;
    private Vector3 groundNormal = Vector3.up;
    private float lastDamageTime = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.freezeRotation = true;

        // Sin rozamiento, para no engancharse en las laderas.
        if (col != null)
        {
            PhysicsMaterial slippery = new PhysicsMaterial("EnemigoSinRoce");
            slippery.dynamicFriction = 0f;
            slippery.staticFriction = 0f;
            slippery.frictionCombine = PhysicsMaterialCombine.Minimum;
            slippery.bounciness = 0f;
            slippery.bounceCombine = PhysicsMaterialCombine.Minimum;
            col.material = slippery;
        }

        // Barra de vida automatica: no hace falta añadirla a mano al prefab.
        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        else Debug.LogWarning("[EnemyChaser] No hay objeto con tag Player en la escena.");
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        CheckGround();
        Move();
    }

    private void CheckGround()
    {
        isGrounded = false;
        groundNormal = Vector3.up;

        if (col == null) { isGrounded = true; return; }

        Bounds bounds = col.bounds;
        float distance = bounds.extents.y - groundProbeRadius + groundProbeExtra;

        RaycastHit[] hits = Physics.SphereCastAll(bounds.center, groundProbeRadius, Vector3.down,
                                                  Mathf.Max(0.05f, distance), groundMask,
                                                  QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform)) continue;
            if (hits[i].collider.CompareTag("Player")) continue;      // no se apoya en el jugador
            if (hits[i].distance >= closest) continue;
            if (hits[i].normal.y <= 0.01f) continue;

            closest = hits[i].distance;
            groundNormal = hits[i].normal;
        }

        if (closest == float.MaxValue) return;

        isGrounded = Vector3.Angle(groundNormal, Vector3.up) <= maxSlopeAngle;
    }

    private void Move()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) return;

        Vector3 direction = toPlayer.normalized;

        if (isGrounded)
        {
            // Avanza siguiendo la pendiente: asi sube la ladera en vez de atascarse.
            Vector3 onSlope = Vector3.ProjectOnPlane(direction, groundNormal);
            if (onSlope.sqrMagnitude > 0.0001f) onSlope = onSlope.normalized;

            Vector3 velocity = Vector3.MoveTowards(rb.linearVelocity, onSlope * speed,
                                                   acceleration * Time.fixedDeltaTime);

            rb.linearVelocity = velocity - groundNormal * stickToGround;
        }
        else
        {
            Vector3 current = rb.linearVelocity;
            Vector3 horizontal = new Vector3(current.x, 0f, current.z);
            horizontal = Vector3.MoveTowards(horizontal, direction * speed,
                                             acceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(horizontal.x, current.y, horizontal.z);
        }

        // Mira al jugador en horizontal: no se inclina con la cuesta.
        Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.fixedDeltaTime);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (Time.time - lastDamageTime < damageCooldown) return;
        if (GameManager.instance == null) return;

        lastDamageTime = Time.time;
        GameManager.instance.TakeDamage(damageAmount);
    }
}