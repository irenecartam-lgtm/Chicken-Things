
using UnityEngine;
 
// Enemigo volador: orbita sobre el jugador y se lanza en picado cada pocos segundos.
// Va en el prefab de la alita, junto a EnemyHealth.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyFlyer : MonoBehaviour
{
    public enum State { Approach, Orbit, Dive, Climb }
 
    [Header("Vuelo")]
    [Tooltip("Altura a la que se mantiene por encima del jugador.")]
    public float cruiseHeight = 4f;
    public float flySpeed = 5.5f;
    public float acceleration = 14f;
    public float turnSpeed = 8f;
 
    [Header("Orbita")]
    [Tooltip("Radio del circulo que describe alrededor del jugador.")]
    public float orbitRadius = 5f;
    [Tooltip("Grados por segundo que avanza en la orbita.")]
    public float orbitSpeed = 55f;
    [Tooltip("Segundos orbitando antes de lanzarse. Se sortea entre los dos valores.")]
    public Vector2 timeBeforeDive = new Vector2(1.5f, 3.5f);
 
    [Header("Picado")]
    public float diveSpeed = 15f;
    [Tooltip("Distancia a la que cuenta como impacto y remonta.")]
    public float diveHitDistance = 1.3f;
    [Tooltip("Si no acierta en este tiempo, aborta y sube igual.")]
    public float diveTimeout = 2.5f;
 
    [Header("Aleteo")]
    public float bobAmplitude = 0.3f;
    public float bobSpeed = 7f;
    [Tooltip("Inclinacion lateral al girar. Da sensacion de vuelo.")]
    public float bankAngle = 35f;
 
    [Header("Daño por contacto")]
    public int damageAmount = 1;
    public float damageCooldown = 1f;
 
    [Header("Diagnostico")]
    public State currentState = State.Approach;
 
    private Rigidbody rb;
    private Transform player;
    private float orbitAngle;
    private int orbitDirection = 1;
    private float stateTimer;
    private float lastDamageTime = -999f;
    private float bobOffset;
    private Vector3 diveTarget;
 
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
 
        // Un volador no cae: la altura la gobierna el script, no la gravedad.
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.freezeRotation = true;          // la rotacion la fijamos nosotros
        rb.linearDamping = 1.5f;           // absorbe el empujon de los disparos
 
        // Barra de vida automatica, igual que en EnemyChaser.
        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();
 
        orbitDirection = Random.value < 0.5f ? -1 : 1;
        orbitAngle = Random.Range(0f, 360f);
        bobOffset = Random.Range(0f, 10f);  // que no aleteen todas a la vez
    }
 
    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        else Debug.LogWarning("[EnemyFlyer] No hay objeto con tag Player en la escena.");
 
        stateTimer = Random.Range(timeBeforeDive.x, timeBeforeDive.y);
    }
 
    private void FixedUpdate()
    {
        if (player == null) return;
 
        switch (currentState)
        {
            case State.Approach: DoApproach(); break;
            case State.Orbit:    DoOrbit();    break;
            case State.Dive:     DoDive();     break;
            case State.Climb:    DoClimb();    break;
        }
    }
 
    // ------------------------------------------------------------ Estados
 
    private void DoApproach()
    {
        Vector3 target = OrbitPoint();
        SeekTo(target, flySpeed);
 
        if (Horizontal(target - transform.position) < orbitRadius * 0.6f)
            EnterOrbit();
    }
 
    private void DoOrbit()
    {
        orbitAngle += orbitSpeed * orbitDirection * Time.fixedDeltaTime;
        SeekTo(OrbitPoint(), flySpeed);
 
        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f) EnterDive();
    }
 
    private void DoDive()
    {
        // El objetivo se fija al empezar el picado: asi el jugador puede esquivar.
        SeekTo(diveTarget, diveSpeed);
 
        stateTimer -= Time.fixedDeltaTime;
 
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= diveHitDistance)
        {
            Hit();
            EnterClimb();
            return;
        }
 
        if (stateTimer <= 0f || transform.position.y < player.position.y - 0.5f)
            EnterClimb();
    }
 
    private void DoClimb()
    {
        Vector3 target = OrbitPoint();
        SeekTo(target, flySpeed * 1.2f);
 
        if (transform.position.y >= player.position.y + cruiseHeight - 0.8f)
            EnterOrbit();
    }
 
    private void EnterOrbit()
    {
        currentState = State.Orbit;
        stateTimer = Random.Range(timeBeforeDive.x, timeBeforeDive.y);
 
        // Se recalcula el angulo desde donde esta, para no dar un tiron al entrar.
        Vector3 offset = transform.position - player.position;
        orbitAngle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
    }
 
    private void EnterDive()
    {
        currentState = State.Dive;
        stateTimer = diveTimeout;
        diveTarget = player.position;
    }
 
    private void EnterClimb()
    {
        currentState = State.Climb;
    }
 
    // ---------------------------------------------------------- Movimiento
 
    private Vector3 OrbitPoint()
    {
        float rad = orbitAngle * Mathf.Deg2Rad;
        Vector3 ring = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * orbitRadius;
 
        float bob = Mathf.Sin((Time.time + bobOffset) * bobSpeed) * bobAmplitude;
        return player.position + ring + Vector3.up * (cruiseHeight + bob);
    }
 
    private void SeekTo(Vector3 target, float speed)
    {
        Vector3 toTarget = target - transform.position;
        float distance = toTarget.magnitude;
        if (distance < 0.05f) { Brake(); return; }
 
        Vector3 desired = (toTarget / distance) * speed;
 
        // Frena al llegar, para no orbitar dando bandazos.
        if (distance < 1.5f) desired *= distance / 1.5f;
 
        rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, desired,
                                                acceleration * Time.fixedDeltaTime);
        FaceVelocity();
    }
 
    private void Brake()
    {
        rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero,
                                                acceleration * Time.fixedDeltaTime);
    }
 
    // Mira hacia donde vuela y se inclina en las curvas.
    private void FaceVelocity()
    {
        Vector3 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.04f) return;
 
        Quaternion look = Quaternion.LookRotation(velocity.normalized, Vector3.up);
 
        // El alabeo sale de cuanta velocidad es lateral respecto a donde mira.
        float lateral = Vector3.Dot(transform.right, velocity.normalized);
        look *= Quaternion.Euler(0f, 0f, -lateral * bankAngle);
 
        transform.rotation = Quaternion.Slerp(transform.rotation, look,
                                              turnSpeed * Time.fixedDeltaTime);
    }
 
    private float Horizontal(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }
 
    // ----------------------------------------------------------- Daño
 
    private void Hit()
    {
        if (Time.time - lastDamageTime < damageCooldown) return;
        if (GameManager.instance == null) return;
 
        lastDamageTime = Time.time;
        GameManager.instance.TakeDamage(damageAmount);
    }
 
    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        Hit();
    }
 
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || player == null) return;
 
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(player.position + Vector3.up * cruiseHeight, orbitRadius);
 
        if (currentState == State.Dive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, diveTarget);
        }
    }
}
 