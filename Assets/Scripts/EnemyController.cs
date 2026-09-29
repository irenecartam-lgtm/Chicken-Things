using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    public enum State { Patrol, Chase, Search, Return }

    [Header("Ruta de patrulla")]
    public Transform pointA;
    public Transform pointB;
    [Tooltip("Segundos parado en cada extremo de la ruta.")]
    public float waitAtPoint = 1f;

    [Header("Velocidades")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float acceleration = 20f;
    public float turnSpeed = 8f;

    [Header("Deteccion")]
    public float detectionRange = 8f;
    [Tooltip("Angulo total del cono de vision, en grados.")]
    public float viewAngle = 110f;
    [Tooltip("Radio en el que te detecta aunque estes a su espalda.")]
    public float awarenessRadius = 2.5f;
    [Tooltip("Capas que bloquean la vision. Marca aqui el suelo y las paredes.")]
    public LayerMask obstacleMask;
    public float eyeHeight = 0.5f;

    [Header("Memoria")]
    [Tooltip("Segundos persiguiendo sin verte antes de pasar a buscar.")]
    public float memoryDuration = 2f;
    [Tooltip("Segundos buscando en tu ultima posicion conocida.")]
    public float searchDuration = 3f;

    [Header("Daño por contacto")]
    public int damageAmount = 1;
    public float damageCooldown = 1f;

    [Header("Diagnostico")]
    public State currentState = State.Patrol;

    private Rigidbody rb;
    private Transform player;
    private Transform currentPoint;
    private Vector3 lastKnownPosition;
    private Vector3 homePosition;

    private float waitTimer;
    private float memoryTimer;
    private float searchTimer;
    private float lastDamageTime = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        homePosition = transform.position;
    }

    private void Start()
    {
        currentPoint = pointA;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        else Debug.LogWarning("[EnemyController] No hay objeto con tag Player en la escena.");

        if (pointA == null || pointB == null)
            Debug.LogWarning($"[EnemyController] {name}: faltan puntos de patrulla. Se quedara quieto.");
    }

    private void FixedUpdate()
    {
        bool canSeePlayer = CanSeePlayer();

        if (canSeePlayer)
        {
            lastKnownPosition = player.position;
            memoryTimer = memoryDuration;
            currentState = State.Chase;
        }

        switch (currentState)
        {
            case State.Patrol: DoPatrol();  break;
            case State.Chase:  DoChase();   break;
            case State.Search: DoSearch();  break;
            case State.Return: DoReturn();  break;
        }
    }

    // ---------- Deteccion ----------

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = (player.position + Vector3.up * eyeHeight) - eye;
        float distance = toPlayer.magnitude;

        if (distance > detectionRange) return false;

        // A quemarropa te detecta aunque estes detras: nadie ignora lo que le respira en la nuca.
        bool insideCone = Vector3.Angle(transform.forward, toPlayer) <= viewAngle * 0.5f;
        if (!insideCone && distance > awarenessRadius) return false;

        // Linea de vista: si hay algo solido en medio, no te ve.
        if (Physics.Raycast(eye, toPlayer.normalized, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    // ---------- Estados ----------

    private void DoPatrol()
    {
        if (currentPoint == null) { Brake(); return; }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            Brake();
            return;
        }

        MoveTowards(currentPoint.position, patrolSpeed);

        if (HorizontalDistance(transform.position, currentPoint.position) < 0.3f)
        {
            currentPoint = (currentPoint == pointA) ? pointB : pointA;
            waitTimer = waitAtPoint;
        }
    }

    private void DoChase()
    {
        memoryTimer -= Time.fixedDeltaTime;

        if (memoryTimer <= 0f)
        {
            currentState = State.Search;
            searchTimer = searchDuration;
            return;
        }

        MoveTowards(lastKnownPosition, chaseSpeed);
    }

    private void DoSearch()
    {
        searchTimer -= Time.fixedDeltaTime;

        if (HorizontalDistance(transform.position, lastKnownPosition) > 0.5f)
        {
            MoveTowards(lastKnownPosition, patrolSpeed);
        }
        else
        {
            // Llegado al ultimo punto conocido, gira sobre si mismo mirando alrededor.
            Brake();
            transform.Rotate(Vector3.up, 120f * Time.fixedDeltaTime);
        }

        if (searchTimer <= 0f) currentState = State.Return;
    }

    private void DoReturn()
    {
        Vector3 target = (currentPoint != null) ? currentPoint.position : homePosition;
        MoveTowards(target, patrolSpeed);

        if (HorizontalDistance(transform.position, target) < 0.4f)
            currentState = State.Patrol;
    }

    // ---------- Movimiento ----------

    private void MoveTowards(Vector3 target, float speed)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f) { Brake(); return; }
        direction.Normalize();

        Vector3 current = rb.linearVelocity;   // Unity 2022 o anterior: rb.velocity
        Vector3 horizontal = new Vector3(current.x, 0f, current.z);
        horizontal = Vector3.MoveTowards(horizontal, direction * speed, acceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(horizontal.x, current.y, horizontal.z);

        Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.fixedDeltaTime);
    }

    private void Brake()
    {
        Vector3 current = rb.linearVelocity;
        Vector3 horizontal = Vector3.MoveTowards(
            new Vector3(current.x, 0f, current.z), Vector3.zero, acceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(horizontal.x, current.y, horizontal.z);
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------- Daño por contacto ----------

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (Time.time - lastDamageTime < damageCooldown) return;
        if (GameManager.instance == null) return;

        lastDamageTime = Time.time;
        GameManager.instance.TakeDamage(damageAmount);
    }

    // ---------- Gizmos ----------

    private void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(eye, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(eye, awarenessRadius);

        Gizmos.color = Color.cyan;
        Vector3 left  = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f,  viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(eye, left  * detectionRange);
        Gizmos.DrawRay(eye, right * detectionRange);
    }
}