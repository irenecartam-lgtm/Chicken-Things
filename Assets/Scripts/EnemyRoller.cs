using UnityEngine;

// CROQUETA: se acerca despacio, se para a apuntar (tiembla) y sale rodando en linea recta
// hacia donde estaba el jugador. Rebota contra paredes y enemigos y, al acabar la carga,
// se queda mareada un momento (es cuando es mas facil darle).
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyRoller : MonoBehaviour
{
    public enum State { Approach, Aim, Charge, Dizzy }

    [Header("Acercarse")]
    public float speed = 2f;
    public float acceleration = 15f;
    public float turnSpeed = 8f;
    [Tooltip("Distancia a la que se para y empieza a apuntar.")]
    public float chargeRange = 9f;

    [Header("Apuntar")]
    public float aimTime = 0.8f;
    [Tooltip("Cuanto tiembla el modelo mientras apunta (aviso para el jugador).")]
    public float shakeAmount = 0.08f;

    [Header("Carga rodando")]
    public float chargeSpeed = 14f;
    public float maxChargeTime = 2.5f;
    [Tooltip("Rebotes contra paredes antes de pararse.")]
    public int maxBounces = 2;
    [Tooltip("Radio de la croqueta: decide lo rapido que gira el modelo al rodar.")]
    public float rollRadius = 0.4f;

    [Header("Mareada")]
    public float dizzyTime = 1.2f;

    [Header("Daño")]
    public int contactDamage = 1;
    public int chargeDamage = 2;
    public float damageCooldown = 1f;

    [Header("Visual")]
    [Tooltip("Hijo con el modelo 3D. Si se deja vacio se usa el primer hijo.")]
    public Transform model;

    [Header("Diagnostico")]
    public State state = State.Approach;

    private Rigidbody rb;
    private Transform player;
    private float stateTimer;
    private Vector3 chargeDir;
    private int bounces;
    private float lastDamageTime = -999f;
    private Vector3 modelStartPos;
    private Quaternion modelStartRot;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        // Sin rozamiento, igual que el chaser, para que no se enganche al rodar.
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            PhysicsMaterial slippery = new PhysicsMaterial("CroquetaSinRoce");
            slippery.dynamicFriction = 0f;
            slippery.staticFriction = 0f;
            slippery.frictionCombine = PhysicsMaterialCombine.Minimum;
            slippery.bounciness = 0f;
            slippery.bounceCombine = PhysicsMaterialCombine.Minimum;
            col.material = slippery;
        }

        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        if (model != null)
        {
            modelStartPos = model.localPosition;
            modelStartRot = model.localRotation;
        }

        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        else Debug.LogWarning("[EnemyRoller] No hay objeto con tag Player en la escena.");
    }

    // Si el queso lo desactiva a media carga, al volver empieza de cero.
    private void OnDisable()
    {
        ResetModel();
        state = State.Approach;
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        float dt = Time.fixedDeltaTime;
        stateTimer += dt;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        Vector3 dirToPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;

        switch (state)
        {
            case State.Approach:
                MoveHorizontal(dirToPlayer * speed);
                Face(dirToPlayer);
                if (toPlayer.magnitude <= chargeRange) SetState(State.Aim);
                break;

            case State.Aim:
                MoveHorizontal(Vector3.zero);
                Face(dirToPlayer);
                if (model != null)
                    model.localPosition = modelStartPos + Random.insideUnitSphere * shakeAmount;

                if (stateTimer >= aimTime)
                {
                    chargeDir = dirToPlayer;
                    bounces = 0;
                    ResetModel();
                    SetState(State.Charge);
                }
                break;

            case State.Charge:
                // Velocidad fija: los disparos no la frenan mientras rueda.
                Vector3 v = rb.linearVelocity;
                rb.linearVelocity = new Vector3(chargeDir.x * chargeSpeed, v.y, chargeDir.z * chargeSpeed);
                transform.rotation = Quaternion.LookRotation(chargeDir, Vector3.up);

                if (model != null)
                {
                    float degrees = chargeSpeed / Mathf.Max(0.05f, rollRadius) * Mathf.Rad2Deg * dt;
                    model.Rotate(transform.right, degrees, Space.World);
                }

                if (stateTimer >= maxChargeTime) SetState(State.Dizzy);
                break;

            case State.Dizzy:
                MoveHorizontal(Vector3.zero);
                if (model != null)
                    model.localRotation = Quaternion.Slerp(model.localRotation, modelStartRot, 5f * dt);

                if (stateTimer >= dizzyTime)
                {
                    ResetModel();
                    SetState(State.Approach);
                }
                break;
        }
    }

    private void SetState(State next)
    {
        state = next;
        stateTimer = 0f;
    }

    private void MoveHorizontal(Vector3 target)
    {
        Vector3 current = rb.linearVelocity;
        Vector3 horizontal = new Vector3(current.x, 0f, current.z);
        horizontal = Vector3.MoveTowards(horizontal, target, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(horizontal.x, current.y, horizontal.z);
    }

    private void Face(Vector3 dir)
    {
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.fixedDeltaTime);
    }

    private void ResetModel()
    {
        if (model == null) return;
        model.localPosition = modelStartPos;
        model.localRotation = modelStartRot;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            HitPlayer(state == State.Charge ? chargeDamage : contactDamage);
            if (state == State.Charge) SetState(State.Dizzy);
            return;
        }

        if (state != State.Charge) return;

        // Solo cuentan los choques laterales (paredes, rocas, otros enemigos), no el suelo.
        Vector3 normal = collision.GetContact(0).normal;
        if (Mathf.Abs(normal.y) > 0.5f) return;

        if (bounces >= maxBounces)
        {
            SetState(State.Dizzy);
            return;
        }

        normal.y = 0f;
        chargeDir = Vector3.Reflect(chargeDir, normal.normalized);
        chargeDir.y = 0f;
        chargeDir.Normalize();
        bounces++;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player")) HitPlayer(contactDamage);
    }

    private void HitPlayer(int amount)
    {
        if (Time.time - lastDamageTime < damageCooldown) return;
        if (GameManager.instance == null) return;

        lastDamageTime = Time.time;
        GameManager.instance.TakeDamage(amount);
    }
}