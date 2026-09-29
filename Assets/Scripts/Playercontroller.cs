using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Velocidades")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float acceleration = 40f;

    [Header("Apuntado")]
    public Transform visual;
    [Tooltip("ON: gira hacia el cursor. OFF: gira hacia donde se mueve.")]
    public bool aimWithMouse = true;
    public float turnSpeed = 25f;

    private Rigidbody rb;
    private Camera cam;
    private Vector3 inputDirection;
    private bool isSprinting;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        if (visual == null) visual = transform;

        cam = Camera.main;
        if (cam == null)
            Debug.LogWarning("[PlayerController] No hay camara con tag MainCamera. El apuntado con raton no funcionara.");

        if (rb.isKinematic)
            Debug.LogWarning("[PlayerController] Rigidbody Kinematic: ignorara la velocidad.");
    }

    private void Update()
    {
        ReadInput();
        Turn();          // el giro es visual, no fisico: va en Update para que responda al instante
    }

    private void FixedUpdate()
    {
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

        inputDirection = new Vector3(h, 0f, v);
        if (inputDirection.sqrMagnitude > 1f) inputDirection.Normalize();

        isSprinting = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
    }

    private void Move()
    {
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = inputDirection * targetSpeed;

        Vector3 current = rb.linearVelocity;
        Vector3 horizontal = new Vector3(current.x, 0f, current.z);

        horizontal = Vector3.MoveTowards(horizontal, targetVelocity, acceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(horizontal.x, current.y, horizontal.z);
    }

    private void Turn()
    {
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

    // Proyecta el cursor sobre un plano horizontal a la altura del nugget.
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