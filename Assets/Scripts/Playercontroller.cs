using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Velocidades")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float acceleration = 40f;

    [Tooltip("Aceleracion en el aire. Mas baja que en suelo: menos control cayendo.")]
    public float airAcceleration = 10f;

    [Header("Suelo y pendientes")]
    public LayerMask groundMask = ~0;

    [Tooltip("Pendiente maxima que se puede subir. Por encima, se resbala.")]
    [Range(20f, 70f)] public float maxSlopeAngle = 55f;

    [Tooltip("Radio de la esfera que busca el suelo. Menor que el ancho del collider.")]
    public float groundProbeRadius = 0.35f;

    [Tooltip("Cuanto se busca por debajo de los pies.")]
    public float groundProbeExtra = 0.3f;

    [Tooltip("Pegado al suelo. Evita salir volando al coronar una loma.")]
    public float stickToGround = 3f;

    [Header("Salto")]
    [Tooltip("Altura maxima del salto en metros (manteniendo la tecla).")]
    public float jumpHeight = 1.6f;

    [Tooltip("Gravedad extra al caer. Con 1 el salto es flotante; mas alto, mas seco.")]
    public float fallGravityMultiplier = 1.8f;

    [Tooltip("Gravedad extra si sueltas el salto antes de tiempo: salto corto.")]
    public float shortHopGravityMultiplier = 2.5f;

    [Tooltip("Margen para saltar justo despues de dejar de pisar el suelo.")]
    public float coyoteTime = 0.12f;

    [Tooltip("Margen para pulsar salto un instante antes de aterrizar.")]
    public float jumpBufferTime = 0.12f;

    [Tooltip("Pendiente maxima desde la que aun se puede saltar. Sirve para salir de las grietas entre montañas.")]
    [Range(30f, 85f)] public float maxJumpSlope = 75f;

    [Header("Camara")]
    [Tooltip("Si se asigna: movimiento relativo a la camara y el nugget mira hacia donde mira ella. " +
             "Si se deja vacio: comportamiento cenital antiguo (ejes del mundo + apuntado al cursor).")]
    public CameraController cameraRig;

    [Header("Apuntado (solo modo cenital, sin cameraRig)")]
    public Transform visual;
    [Tooltip("ON: gira hacia el cursor. OFF: gira hacia donde se mueve.")]
    public bool aimWithMouse = true;
    public float turnSpeed = 25f;

    [Header("Diagnostico")]
    public bool isGrounded;
    public float currentSlope;

    private Rigidbody rb;
    private Collider col;
    private Camera cam;
    private Vector3 inputDirection;
    private Vector3 groundNormal = Vector3.up;
    private bool isSprinting;

    private bool jumpHeld;
    private float jumpBufferedUntil = -1f;
    private float lastGroundedTime = -999f;
    private const float LeaveGroundSpeed = 2f;   // si te alejas del suelo a mas de esto, estas saltando

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.freezeRotation = true;
        // Sin interpolacion la camara pegada al jugador tiembla, sobre todo en primera persona.
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (visual == null) visual = transform;

        // Sin rozamiento: el movimiento lo gobierna el script, y la friccion
        // solo haria que el collider se enganchase en las laderas.
        if (col != null)
        {
            PhysicsMaterial slippery = new PhysicsMaterial("JugadorSinRoce");
            slippery.dynamicFriction = 0f;
            slippery.staticFriction = 0f;
            slippery.frictionCombine = PhysicsMaterialCombine.Minimum;
            slippery.bounciness = 0f;
            slippery.bounceCombine = PhysicsMaterialCombine.Minimum;
            col.material = slippery;
        }

        cam = Camera.main;
        if (cam == null)
            Debug.LogWarning("[PlayerController] No hay camara con tag MainCamera.");

        if (rb.isKinematic)
            Debug.LogWarning("[PlayerController] Rigidbody Kinematic: ignorara la velocidad.");
    }

    private void Update()
    {
        ReadInput();
        Turn();
    }

    private void FixedUpdate()
    {
        CheckGround();
        TryJump();
        Move();
    }

    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f;
        float v = 0f;

        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;

        Vector3 raw = new Vector3(h, 0f, v);
        if (raw.sqrMagnitude > 1f) raw.Normalize();

        // Con camara en 1a/3a persona, W es "hacia donde miro", no "hacia el norte del mapa".
        inputDirection = cameraRig != null
            ? Quaternion.Euler(0f, cameraRig.Yaw, 0f) * raw
            : raw;

        isSprinting = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        // El salto se "apunta" unos milisegundos: asi no se pierde si lo pulsas justo antes de aterrizar.
        if (kb.spaceKey.wasPressedThisFrame) jumpBufferedUntil = Time.time + jumpBufferTime;
        jumpHeld = kb.spaceKey.isPressed;
    }

    // Busca el suelo con una esfera hacia abajo y guarda su inclinacion.
    private void CheckGround()
    {
        isGrounded = false;
        groundNormal = Vector3.up;
        currentSlope = 0f;

        if (col == null) { isGrounded = true; lastGroundedTime = Time.time; return; }

        Bounds bounds = col.bounds;
        float halfHeight = bounds.extents.y;
        float distance = halfHeight - groundProbeRadius + groundProbeExtra;

        RaycastHit[] hits = Physics.SphereCastAll(bounds.center, groundProbeRadius, Vector3.down,
                                                  Mathf.Max(0.05f, distance), groundMask,
                                                  QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform)) continue;   // su propio cuerpo
            if (hits[i].distance >= closest) continue;
            if (hits[i].normal.y <= 0.01f) continue;                         // paredes y techos no

            closest = hits[i].distance;
            groundNormal = hits[i].normal;
        }

        if (closest == float.MaxValue) return;

        currentSlope = Vector3.Angle(groundNormal, Vector3.up);

        // Si ya nos estamos separando del suelo (saltando), no cuenta como pisarlo.
        if (Vector3.Dot(rb.linearVelocity, groundNormal) > LeaveGroundSpeed) return;

        isGrounded = currentSlope <= maxSlopeAngle;

        // Tambien se puede saltar desde una cuesta demasiado empinada para andar (hasta Max Jump Slope).
        if (isGrounded || currentSlope <= maxJumpSlope) lastGroundedTime = Time.time;
    }

    private void TryJump()
    {
        bool wantsJump = Time.time <= jumpBufferedUntil;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        if (!wantsJump || !canJump) return;

        // v = raiz(2 * g * h): la velocidad justa para llegar a la altura pedida.
        float jumpSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);

        Vector3 velocity = rb.linearVelocity;
        velocity.y = jumpSpeed;
        rb.linearVelocity = velocity;

        jumpBufferedUntil = -1f;
        lastGroundedTime = -999f;   // evita un segundo salto en el aire
        isGrounded = false;
    }

    private void Move()
    {
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;

        if (isGrounded)
        {
            // Proyectar sobre el plano del suelo: asi al andar en una ladera
            // se sube por ella en vez de empujar contra ella.
            Vector3 direction = Vector3.ProjectOnPlane(inputDirection, groundNormal);
            if (direction.sqrMagnitude > 0.0001f) direction = direction.normalized * inputDirection.magnitude;

            Vector3 target = direction * targetSpeed;
            Vector3 velocity = Vector3.MoveTowards(rb.linearVelocity, target,
                                                   acceleration * Time.fixedDeltaTime);

            // Empujoncito contra el suelo: sin esto sales despedido al coronar una loma.
            rb.linearVelocity = velocity - groundNormal * stickToGround;
        }
        else
        {
            // En el aire (o en pendiente imposible) manda la gravedad.
            Vector3 current = rb.linearVelocity;
            Vector3 horizontal = new Vector3(current.x, 0f, current.z);
            Vector3 target = new Vector3(inputDirection.x, 0f, inputDirection.z) * targetSpeed;

            horizontal = Vector3.MoveTowards(horizontal, target, airAcceleration * Time.fixedDeltaTime);

            // Gravedad extra: caida mas seca, y salto corto si sueltas la tecla.
            float extra = 0f;
            if (current.y < 0f) extra = fallGravityMultiplier - 1f;
            else if (current.y > 0f && !jumpHeld) extra = shortHopGravityMultiplier - 1f;
            float y = current.y + Physics.gravity.y * extra * Time.fixedDeltaTime;

            rb.linearVelocity = new Vector3(horizontal.x, y, horizontal.z);
        }
    }

    private void Turn()
    {
        if (cameraRig != null)
        {
            // Giro instantaneo: en primera persona cualquier retraso se nota como mareo.
            visual.rotation = Quaternion.Euler(0f, cameraRig.Yaw, 0f);
            return;
        }

        Vector3 lookDirection;

        if (aimWithMouse)
        {
            if (!TryGetAimPoint(out Vector3 aimPoint)) return;
            lookDirection = aimPoint - transform.position;
        }
        else
        {
            lookDirection = inputDirection;
        }

        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(lookDirection, Vector3.up);
        visual.rotation = Quaternion.Slerp(visual.rotation, target, turnSpeed * Time.deltaTime);
    }

    // Modo cenital: proyecta el cursor sobre un plano horizontal a la altura del nugget.
    private bool TryGetAimPoint(out Vector3 point)
    {
        point = Vector3.zero;

        Mouse mouse = Mouse.current;
        if (mouse == null || cam == null) return false;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.up, transform.position);

        if (ground.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }
        return false;
    }
}